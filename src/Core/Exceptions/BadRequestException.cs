namespace Core.Exceptions;

/// <summary>
/// Исключение некорректных входных данных
/// </summary>
public class BadRequestException : AppException
{
    public BadRequestException(string message = "Некорректные данные запроса") : base(message, 400)
    {
    }
}