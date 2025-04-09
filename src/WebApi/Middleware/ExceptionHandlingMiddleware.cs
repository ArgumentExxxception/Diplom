using System.Text.Json;
using Core.Exceptions;
using Core.Logging;
using WebApi.Models;

namespace WebApi.Middleware;

/// <summary>
/// Middleware для глобальной обработки исключений
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    
    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }
    
    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(httpContext, ex);
        }
    }
    
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        var response = new ApiResponse();
        
        switch (exception)
        {
            case AccessDeniedException accessException:
                response = ApiResponse.Fail(accessException.Message, StatusCodes.Status403Forbidden);
                // _logger.LogWarning($"Доступ запрещен: {accessException.Message}");
                break;
                
            case NotFoundException notFoundException:
                response = ApiResponse.Fail(notFoundException.Message, StatusCodes.Status404NotFound);
                // _logger.LogWarning($"Ресурс не найден: {notFoundException.Message}");
                break;
                
            case BadRequestException badRequestException:
                response = ApiResponse.Fail(badRequestException.Message, StatusCodes.Status400BadRequest);
                // _logger.LogWarning($"Некорректный запрос: {badRequestException.Message}");
                break;
                
            case ValidationException validationException:
                response = ApiResponse.Fail(
                    validationException.Message, 
                    StatusCodes.Status400BadRequest,
                    validationException.Errors);
                // _logger.LogWarning($"Ошибка валидации: {validationException.Message}");
                break;
                
            case ConflictException conflictException:
                response = ApiResponse.Fail(conflictException.Message, StatusCodes.Status409Conflict);
                // _logger.LogWarning($"Конфликт данных: {conflictException.Message}");
                break;
                
            case BusinessLogicException businessLogicException:
                response = ApiResponse.Fail(businessLogicException.Message, StatusCodes.Status422UnprocessableEntity);
                // _logger.LogWarning($"Ошибка бизнес-логики: {businessLogicException.Message}");
                break;
                
            case AppException appException:
                response = ApiResponse.Fail(appException.Message, appException.StatusCode);
                // _logger.LogError($"Ошибка приложения: {appException.Message}");
                break;
                
            default:
                // Для необработанных исключений
                var message = _environment.IsDevelopment() 
                    ? exception.Message 
                    : "Произошла внутренняя ошибка сервера";
                
                response = ApiResponse.Fail(message, StatusCodes.Status500InternalServerError);
                // _logger.LogError(exception, $"Необработанное исключение: {exception.Message}");
                break;
        }
        
        context.Response.StatusCode = response.StatusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}