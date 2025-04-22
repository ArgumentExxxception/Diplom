using System.Net;
using System.Text.Json;
using App.Models;
using MudBlazor;

namespace App.Services;

/// <summary>
/// Сервис для централизованной обработки ошибок на клиенте
/// </summary>
public class ErrorHandlingService
{
    private readonly ISnackbar _snackbar;
    private readonly JsonSerializerOptions _jsonOptions;

    public ErrorHandlingService(ISnackbar snackbar)
    {
        _snackbar = snackbar;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Обработка HTTP ответа с ошибкой
    /// </summary>
    public async Task HandleHttpErrorResponse(HttpResponseMessage response)
    {
        try
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            
            try
            {
                // Пытаемся десериализовать как ApiResponse
                var apiResponse = JsonSerializer.Deserialize<ApiErrorResponse>(errorContent, _jsonOptions);
                
                if (apiResponse != null)
                {
                    ShowErrorMessage(apiResponse.Message);
                    
                    // Если есть список ошибок, показываем их
                    if (apiResponse.Errors != null && apiResponse.Errors.Count > 0)
                    {
                        foreach (var error in apiResponse.Errors)
                        {
                            ShowErrorMessage(error);
                        }
                    }
                    
                    return;
                }
            }
            catch
            {
            }

            if (string.IsNullOrEmpty(errorContent))
            {
                ShowErrorMessage(GetDefaultErrorMessage(response.StatusCode));
            }
            else
            {
                ShowErrorMessage(errorContent);
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Произошла ошибка при обработке ответа сервера: {ex.Message}");
        }
    }

    /// <summary>
    /// Обработка исключения
    /// </summary>
    public void HandleException(Exception exception)
    {
        if (exception is HttpRequestException httpException)
        {
            string errorMessage = httpException.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Требуется авторизация. Войдите в систему.",
                HttpStatusCode.Forbidden => "У вас нет доступа к запрошенному ресурсу.",
                HttpStatusCode.NotFound => "Запрошенный ресурс не найден.",
                HttpStatusCode.BadRequest => "Некорректный запрос.",
                HttpStatusCode.InternalServerError => "Внутренняя ошибка сервера.",
                _ => $"Ошибка HTTP: {httpException.StatusCode}"
            };
            
            ShowErrorMessage(errorMessage);
        }
        else
        {
            ShowErrorMessage($"Произошла ошибка: {exception.Message}");
        }
    }

    /// <summary>
    /// Обработка ошибок валидации
    /// </summary>
    public void HandleValidationErrors(Dictionary<string, List<string>> validationErrors)
    {
        foreach (var fieldErrors in validationErrors.Values)
        {
            foreach (var error in fieldErrors)
            {
                ShowErrorMessage(error);
            }
        }
    }

    /// <summary>
    /// Показ сообщения об ошибке с использованием Snackbar
    /// </summary>
    public void ShowErrorMessage(string message)
    {
        _snackbar.Add(message, Severity.Error, config =>
        {
            config.ShowCloseIcon = true;
            config.VisibleStateDuration = 8000;
        });
    }

    /// <summary>
    /// Получение стандартного сообщения об ошибке для статусного кода HTTP
    /// </summary>
    private string GetDefaultErrorMessage(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "Некорректный запрос. Проверьте правильность введенных данных.",
            HttpStatusCode.Unauthorized => "Необходима авторизация для выполнения данного действия.",
            HttpStatusCode.Forbidden => "У вас нет прав доступа к запрашиваемому ресурсу.",
            HttpStatusCode.NotFound => "Запрашиваемый ресурс не найден.",
            HttpStatusCode.RequestTimeout => "Превышено время ожидания ответа от сервера.",
            HttpStatusCode.Conflict => "Конфликт данных. Возможно, объект с такими данными уже существует.",
            HttpStatusCode.InternalServerError => "Внутренняя ошибка сервера. Попробуйте повторить запрос позже.",
            HttpStatusCode.ServiceUnavailable => "Сервис временно недоступен. Попробуйте повторить запрос позже.",
            _ => $"Произошла ошибка со статусом {(int)statusCode}"
        };
    }
}