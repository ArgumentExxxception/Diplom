namespace Core.Errors;

public class ImportError
{
    /// <summary>
    /// Номер строки с ошибкой
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// Имя колонки с ошибкой
    /// </summary>
    public string Column { get; set; }

    /// <summary>
    /// Описание ошибки
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Исходное значение, вызвавшее ошибку
    /// </summary>
    public string OriginalValue { get; set; }
}