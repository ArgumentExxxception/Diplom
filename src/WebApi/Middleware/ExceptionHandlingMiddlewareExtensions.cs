namespace WebApi.Middleware;

/// <summary>
/// Расширение для добавления middleware в конвейер обработки запросов
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}