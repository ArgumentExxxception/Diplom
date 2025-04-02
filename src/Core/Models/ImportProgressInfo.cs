namespace Core.Models;

public class ImportProgressInfo
{
    public int ProcessedRows { get; set; }
    public int TotalRows { get; set; } = -1; // -1 означает, что общее количество строк неизвестно
    public int ImportedRows { get; set; }
    public int SkippedRows { get; set; }
    public int ErrorRows { get; set; }
    public string CurrentOperation { get; set; } = "Подготовка к импорту...";
    public double ProgressPercentage => TotalRows > 0 ? (double)ProcessedRows / TotalRows * 100 : 0;
    public bool IsCompleted { get; set; }
    public bool HasError { get; set; }
    public string ErrorMessage { get; set; }
    public List<Core.Errors.ImportError> Errors { get; set; } = new List<Core.Errors.ImportError>();
    public List<Dictionary<string, object>> DuplicatedRows { get; set; } = new List<Dictionary<string, object>>();
}