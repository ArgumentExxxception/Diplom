using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using App.Interfaces;
using Core.Models;
using Core.Results;
using Microsoft.AspNetCore.Components.Forms;
using Newtonsoft.Json;
using JsonException = Newtonsoft.Json.JsonException;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace App.Services;

public class DataImportClientService: IDataImportClientService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public DataImportClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task UpdateDuplicate(string tableName, List<Dictionary<string, object>> duplicates)
    {
        try
        {
            // Создаем контейнер для multipart/form-data
            using var content = new MultipartFormDataContent();

            // Сериализуем данные о дубликатах в JSON
            var duplicatesJson = JsonConvert.SerializeObject(duplicates);
            var duplicatesContent = new StringContent(duplicatesJson, Encoding.UTF8, "application/json");
            content.Add(duplicatesContent, "duplicates");

            // Добавляем имя таблицы в запрос
            var tableNameContent = new StringContent(tableName, Encoding.UTF8, "text/plain");
            content.Add(tableNameContent, "tableName");

            // Отправляем запрос на сервер
            // var responseMessage = await _httpClient.PostAsync("/api/File/update-duplicates", content);
            //
            // if (!responseMessage.IsSuccessStatusCode)
            // {
            //     var error = await responseMessage.Content.ReadAsStringAsync();
            //     throw new Exception($"Ошибка при обновлении дубликатов: {responseMessage.StatusCode}, {error}");
            // }
            //
            // // Если нужно, можно прочитать ответ от сервера
            // var response = await responseMessage.Content.ReadAsStringAsync();
            // Console.WriteLine($"Ответ от сервера: {response}");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    // public async Task<ImportResult> ImportData(IBrowserFile file, TableImportRequestModel importRequest, CancellationToken cancellationToken = default)
    // {
    //     const long maxFileSize = 1_073_741_824; // 1GB
    //     if (file.Size > maxFileSize)
    //     {
    //         throw new InvalidOperationException($"Размер файла превышает максимально допустимый размер {maxFileSize / (1024 * 1024)} МБ");
    //     }
    //         
    //     // Создаем multipart form content
    //     using var content = new MultipartFormDataContent();
    //         
    //     // Читаем файл в память
    //     using var fileStream = file.OpenReadStream(maxAllowedSize: maxFileSize);
    //     using var memoryStream = new MemoryStream();
    //     await fileStream.CopyToAsync(memoryStream, cancellationToken);
    //     memoryStream.Position = 0;
    //         
    //     // Добавляем файл в форму
    //     var fileContent = new ByteArrayContent(memoryStream.ToArray());
    //     fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
    //     content.Add(fileContent, "file", file.Name);
    //         
    //     // Сериализуем и добавляем параметры импорта
    //     var importRequestJson = JsonSerializer.Serialize(importRequest, _jsonOptions);
    //     content.Add(new StringContent(importRequestJson), "importRequest");
    //         
    //     // Отправляем запрос
    //     var response = await _httpClient.PostAsync("api/tables/import", content, cancellationToken);
    //         
    //     // Обрабатываем ответ
    //     if (!response.IsSuccessStatusCode)
    //     {
    //         var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
    //         throw new HttpRequestException($"Ошибка импорта со статусом {response.StatusCode}: {errorContent}");
    //     }
    //         
    //     return await response.Content.ReadFromJsonAsync<ImportResult>(_jsonOptions, cancellationToken) 
    //            ?? throw new JsonException("Не удалось десериализовать ответ");
    // }
    public async Task<ImportResult> ImportData(IBrowserFile file, TableImportRequestModel importRequest, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();

            // Добавляем файл
            var fileContent = new StreamContent(file.OpenReadStream(long.MaxValue));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "file", file.Name);

            // Сериализуем importRequest и добавляем как строку с именем "importRequestJson"
            var importRequestJson = System.Text.Json.JsonSerializer.Serialize(importRequest);
            content.Add(new StringContent(importRequestJson, Encoding.UTF8, "application/json"), "importRequestJson");

            var response = await _httpClient.PostAsync("api/File/import", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"Ошибка импорта: {error}");
            }

            return await response.Content.ReadFromJsonAsync<ImportResult>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при импорте: {ex.Message}");
            throw;
        }
    }
}