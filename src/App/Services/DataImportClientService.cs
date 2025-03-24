using System.Net.Http.Headers;
using System.Text.Json;
using App.Interfaces;
using Core.Enums;
using Core.Models;
using Core.Results;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace App.Services;

public class DataImportClientService: IDataImportClientService
{
    private readonly HttpClient _httpClient;

    public DataImportClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task UpdateDuplicate(string tableName, List<Dictionary<string, object>> duplicates)
    {
        try
        {
            // Создаем контейнер для multipart/form-data
            using var content = new MultipartFormDataContent();

            // Сериализуем данные о дубликатах в JSON
            var duplicatesJson = JsonConvert.SerializeObject(duplicates);
            var duplicatesContent = new StringContent(duplicatesJson, System.Text.Encoding.UTF8, "application/json");
            content.Add(duplicatesContent, "duplicates");

            // Добавляем имя таблицы в запрос
            var tableNameContent = new StringContent(tableName, System.Text.Encoding.UTF8, "text/plain");
            content.Add(tableNameContent, "tableName");

            // Отправляем запрос на сервер
            var responseMessage = await _httpClient.PostAsync("/api/File/update-duplicates", content);

            if (!responseMessage.IsSuccessStatusCode)
            {
                var error = await responseMessage.Content.ReadAsStringAsync();
                throw new Exception($"Ошибка при обновлении дубликатов: {responseMessage.StatusCode}, {error}");
            }

            // Если нужно, можно прочитать ответ от сервера
            var response = await responseMessage.Content.ReadAsStringAsync();
            Console.WriteLine($"Ответ от сервера: {response}");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<ImportResult> ImportData(IBrowserFile file, TableImportRequestModel importRequest)
    {
        try
        {
            // Создаем контейнер для multipart/form-data
            using var content = new MultipartFormDataContent();

            // Добавляем файл
            // var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024)); // Ограничение на 50 МБ
            var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize: 1024 * 1024 * 1024)); // Ограничение на 50 МБ
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "file", file.Name);
            
            // Сериализуем модель запроса в JSON
            var importRequestJson = JsonConvert.SerializeObject(importRequest);
            var importRequestContent = new StringContent(importRequestJson, System.Text.Encoding.UTF8, "application/json");
            content.Add(importRequestContent, "importRequest");

            // Отправляем запрос на сервер
            var responseMessage = await _httpClient.PostAsync("/api/File/import", content);
            
            if (!responseMessage.IsSuccessStatusCode)
            {
                var error = await responseMessage.Content.ReadAsStringAsync();
                throw new Exception($"Ошибка при импорте: {responseMessage.StatusCode}, {error}");
            }
            var importResult = await responseMessage.Content.ReadFromJsonAsync<ImportResult>();
            return importResult;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
            // _logger.LogError(ex, "Ошибка при импорте данных");
            // return new ImportResult 
            // { 
            //     Success = false, 
            //     Message = $"Ошибка: {ex.Message}",
            //     TableName = tableName
            // };
        }
    }
}