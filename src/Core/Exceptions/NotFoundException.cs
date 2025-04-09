namespace Core.Exceptions;

/// <summary>
/// Исключение не найденного ресурса
/// </summary>
public class NotFoundException : AppException
{
    public NotFoundException(string message = "Ресурс не найден") : base(message, 404)
    {
    }
}