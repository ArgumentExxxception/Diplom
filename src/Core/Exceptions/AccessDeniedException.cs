namespace Core.Exceptions;

/// <summary>
/// Исключение доступа (авторизация, аутентификация)
/// </summary>
public class AccessDeniedException : AppException
{
    public AccessDeniedException(string message = "Доступ запрещен") : base(message, 403)
    {
    }
}