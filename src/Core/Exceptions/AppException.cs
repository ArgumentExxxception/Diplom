namespace Core.Exceptions;

/// <summary>
/// Базовое исключение приложения
/// </summary>
public class AppException : Exception
{
    public int StatusCode { get; }

    public AppException(string message, int statusCode = 500) : base(message)
    {
        StatusCode = statusCode;
    }
}