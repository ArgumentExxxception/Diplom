namespace Core.Exceptions;

/// <summary>
/// Исключение для конфликта данных
/// </summary>
public class ConflictException : AppException
{
    public ConflictException(string message = "Конфликт данных") : base(message, 409)
    {
    }
}