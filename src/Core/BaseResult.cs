namespace Core;

public abstract class BaseResult
{
    /// <summary>
    /// Успешность операции
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Сообщение о результате операции
    /// </summary>
    public string Message { get; set; }
    
    /// <summary>
    /// Сериализация в JSON для логирования
    /// </summary>
    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(this);
    }
}