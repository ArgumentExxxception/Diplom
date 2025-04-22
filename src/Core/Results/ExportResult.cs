using Core;

public class ExportResult : BaseResult
{
    /// <summary>
    /// Имя таблицы, из которой происходил экспорт
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Экспортировано строк
    /// </summary>
    public int RowsExported { get; set; }

    /// <summary>
    /// Время выполнения операции в миллисекундах
    /// </summary>
    public double ElapsedTimeMs { get; set; }

    /// <summary>
    /// Имя сгенерированного файла
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Формат экспорта
    /// </summary>
    public string ExportFormat { get; set; }

    /// <summary>
    /// Размер файла в байтах
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// MIME-тип файла
    /// </summary>
    public string ContentType { get; set; }
}