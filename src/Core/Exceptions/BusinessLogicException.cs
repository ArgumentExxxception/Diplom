namespace Core.Exceptions;

/// <summary>
/// Исключение для ошибок бизнес-логики
/// </summary>
public class BusinessLogicException : AppException
{
    public BusinessLogicException(string message) : base(message, 422)
    {
    }
}