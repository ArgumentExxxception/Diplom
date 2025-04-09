namespace App.Models;

/// <summary>
/// Модель ответа от API
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public int StatusCode { get; set; }
    public T Data { get; set; }
    public List<string> Errors { get; set; }
}