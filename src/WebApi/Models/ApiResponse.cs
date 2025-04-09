namespace WebApi.Models;

/// <summary>
/// Представляет единый формат ответа API
/// </summary>
/// <typeparam name="T">Тип возвращаемых данных</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Признак успешности операции
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Сообщение (как правило, для ошибок)
    /// </summary>
    public string Message { get; set; }
    
    /// <summary>
    /// Код статуса HTTP
    /// </summary>
    public int StatusCode { get; set; }
    
    /// <summary>
    /// Возвращаемые данные
    /// </summary>
    public T Data { get; set; }
    
    /// <summary>
    /// Список ошибок (для валидации и других случаев)
    /// </summary>
    public List<string> Errors { get; set; } = new List<string>();
    
    /// <summary>
    /// Вспомогательный метод для создания успешного ответа
    /// </summary>
    public static ApiResponse<T> SuccessBuild(T data, string message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message ?? "Операция выполнена успешно",
            StatusCode = 200,
            Data = data
        };
    }
    
    /// <summary>
    /// Вспомогательный метод для создания ответа с ошибкой
    /// </summary>
    public static ApiResponse<T> Fail(string message, int statusCode = 400, List<string> errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            StatusCode = statusCode,
            Errors = errors ?? new List<string>()
        };
    }
}

/// <summary>
/// Расширение для работы с ответами без данных
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Successed(string message = null)
    {
        var response = new ApiResponse();
        response.Success = true;
        response.Message = message ?? "Операция выполнена успешно";
        response.StatusCode = 200;
        return response;
    }
    
    public static ApiResponse Fail(string message, int statusCode = 400, List<string> errors = null)
    {
        var response = new ApiResponse();
        response.Success = false;
        response.Message = message;
        response.StatusCode = statusCode;
        response.Errors = errors ?? new List<string>();
        return response;
    }
}