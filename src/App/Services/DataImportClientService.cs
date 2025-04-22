using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using App.Interfaces;
using Core.Models;
using Core.Results;
using Microsoft.AspNetCore.Components.Forms;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace App.Services;

public class DataImportClientService : HttpClientBase, IDataImportClientService
{
    public DataImportClientService(
        HttpClient httpClient,
        ErrorHandlingService errorHandler)
        : base(httpClient, errorHandler)
    {
    }

    public async Task UpdateDuplicate(string tableName, List<Dictionary<string, object>> duplicates,
        List<ColumnInfo> columns)
    {
        try
        {
            // Преобразуем дубликаты, нормализуя значения.
            var normalizedDuplicates = NormalizeDuplicates(duplicates);
            var duplicatesJson = JsonConvert.SerializeObject(normalizedDuplicates);

            using var content = new MultipartFormDataContent();

            var duplicatesContent = new StringContent(duplicatesJson, Encoding.UTF8, "application/json");
            content.Add(duplicatesContent, "duplicates");

            var tableNameContent = new StringContent(tableName, Encoding.UTF8, "text/plain");
            content.Add(tableNameContent, "tableName");

            var columnsJson = JsonConvert.SerializeObject(columns);
            var columnsContent = new StringContent(columnsJson, Encoding.UTF8, "application/json");
            content.Add(columnsContent, "columns");

            var response = await _httpClient.PostAsync("api/File/update-duplicates", content);

            if (!response.IsSuccessStatusCode)
            {
                await _errorHandler.HandleHttpErrorResponse(response);
                _errorHandler.ShowErrorMessage($"Ошибка при обновлении дубликатов: {response.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            _errorHandler.HandleException(ex);
            _errorHandler.ShowErrorMessage($"Произошла ошибка при обновлении дубликатов: {ex.Message}");
        }
    }

    private List<Dictionary<string, object>> NormalizeDuplicates(List<Dictionary<string, object>> duplicates)
    {
        return duplicates.Select(dict =>
            dict.ToDictionary(
                kvp => kvp.Key,
                kvp => NormalizeValue(kvp.Value)
            )
        ).ToList();
    }

    private object NormalizeValue(object value)
    {
        // Если значение не является JsonElement — возвращаем как есть.
        if (!(value is JsonElement jsonElement))
            return value;

        // В зависимости от типа JsonElement извлекаем нужное значение
        switch (jsonElement.ValueKind)
        {
            case JsonValueKind.Number:
                // Пробуем вернуть число как int или double 
                if (jsonElement.TryGetInt32(out int intValue))
                    return intValue;
                if (jsonElement.TryGetDouble(out double doubleValue))
                    return doubleValue;
                return jsonElement.ToString(); // fallback
            case JsonValueKind.String:
                return jsonElement.GetString();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
            // Если объект или массив — можно конвертировать в строку или оставить по необходимости:
            default:
                return jsonElement.ToString();
        }
    }

    public async Task<ImportResult> ImportData(IBrowserFile file, TableImportRequestModel importRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Проверка файла
            if (file == null || file.Size == 0)
            {
                _errorHandler.ShowErrorMessage("Файл не был предоставлен или пуст");
                return new ImportResult { Success = false, Message = "Файл не был предоставлен или пуст" };
            }

            // Подготовка содержимого для multipart-запроса
            using var content = new MultipartFormDataContent();

            // Добавляем файл
            var fileContent = new StreamContent(file.OpenReadStream(long.MaxValue));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "file", file.Name);

            // Добавляем параметры импорта
            var importRequestJson = JsonSerializer.Serialize(importRequest);
            content.Add(new StringContent(importRequestJson, Encoding.UTF8, "application/json"), "importRequestJson");

            // Отправляем запрос
            var response = await _httpClient.PostAsync("api/File/import", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await _errorHandler.HandleHttpErrorResponse(response);
                return new ImportResult { Success = false, Message = $"Ошибка импорта: {response.ReasonPhrase}" };
            }

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                return new ImportResult { Success = true, Message = "Импорт успешно запущен в фоновом процессе" };
            }
            
            return await response.Content.ReadFromJsonAsync<ImportResult>(cancellationToken: cancellationToken)
                   ?? new ImportResult { Success = false, Message = "Не удалось обработать ответ сервера" };
        }
        catch (Exception ex)
        {
            _errorHandler.HandleException(ex);
            return new ImportResult { Success = false, Message = $"Ошибка при импорте: {ex.Message}" };
        }
    }
}