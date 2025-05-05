using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using App.Models;
using Blazored.LocalStorage;

namespace App.Services;

/// <summary>
/// Базовый класс для HTTP клиентов с обработкой ошибок
/// </summary>
public class HttpClientBase
{
    protected readonly HttpClient _httpClient;
    protected readonly ErrorHandlingService _errorHandler;
    protected readonly JsonSerializerOptions _jsonOptions;

    public HttpClientBase(
        HttpClient httpClient, 
        ErrorHandlingService errorHandler)
    {
        _httpClient = httpClient;
        _errorHandler = errorHandler;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Выполнение GET запроса
    /// </summary>
    protected async Task<T> GetAsync<T>(string uri)
    {
        try
        {
            var response = await _httpClient.GetAsync(uri);
            await HandleResponseAsync(response);
            
            return await DeserializeResponseAsync<T>(response);
        }
        catch (Exception ex)
        {
            _errorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Выполнение POST запроса
    /// </summary>
    protected async Task<T> PostAsync<T>(string uri, object data = null)
    {
        try
        {
            var content = data != null 
                ? new StringContent(JsonSerializer.Serialize(data, _jsonOptions), Encoding.UTF8, "application/json") 
                : null;
            
            var response = await _httpClient.PostAsync(uri, content);
            await HandleResponseAsync(response);
            
            return await DeserializeResponseAsync<T>(response);
        }
        catch (Exception ex)
        {
            _errorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Выполнение POST запроса без возврата результата
    /// </summary>
    protected async Task PostAsync(string uri, object data = null)
    {
        try
        {
            
            var content = data != null 
                ? new StringContent(JsonSerializer.Serialize(data, _jsonOptions), Encoding.UTF8, "application/json") 
                : null;
            
            var response = await _httpClient.PostAsync(uri, content);
            await HandleResponseAsync(response);
        }
        catch (Exception ex)
        {
            _errorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Выполнение PUT запроса
    /// </summary>
    protected async Task<T> PutAsync<T>(string uri, object data)
    {
        try
        {
            
            var content = new StringContent(JsonSerializer.Serialize(data, _jsonOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(uri, content);
            await HandleResponseAsync(response);
            
            return await DeserializeResponseAsync<T>(response);
        }
        catch (Exception ex)
        {
            _errorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Выполнение DELETE запроса
    /// </summary>
    protected async Task DeleteAsync(string uri)
    {
        try
        {
            
            var response = await _httpClient.DeleteAsync(uri);
            await HandleResponseAsync(response);
        }
        catch (Exception ex)
        {
            _errorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Обработка ответа от сервера
    /// </summary>
    private async Task HandleResponseAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            await _errorHandler.HandleHttpErrorResponse(response);
            throw new HttpRequestException($"HTTP-запрос завершился с ошибкой: {response.StatusCode}");
        }
    }

    /// <summary>
    /// Десериализация ответа
    /// </summary>
    private async Task<T> DeserializeResponseAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        
        if (string.IsNullOrEmpty(content))
        {
            return default;
        }
        
        try
        {
            // Проверяем, является ли ответ ApiResponse
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(content, _jsonOptions);
            
            if (apiResponse != null)
            {
                if (!apiResponse.Success)
                {
                    _errorHandler.ShowErrorMessage(apiResponse.Message);
                    throw new Exception(apiResponse.Message);
                }
                
                return apiResponse.Data;
            }
            else
            {
                // Если ответ не в формате ApiResponse, десериализуем напрямую
                return JsonSerializer.Deserialize<T>(content, _jsonOptions);
            }
        }
        catch
        {
            // Если не удалось десериализовать как ApiResponse, пробуем десериализовать напрямую
            return JsonSerializer.Deserialize<T>(content, _jsonOptions);
        }
    }
}