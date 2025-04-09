namespace Core.Exceptions;

/// <summary>
/// Исключение для ошибок валидации
/// </summary>
public class ValidationException : AppException
{
    public List<string> Errors { get; }

    public ValidationException(string message, List<string> errors = null) : base(message, 400)
    {
        Errors = errors ?? new List<string>();
    }
}