namespace App.Models;

/// <summary>
/// Модель ответа с ошибкой от API
/// </summary>
public class ApiErrorResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; }
}