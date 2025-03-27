using Core.Errors;

namespace Core.Results;

public class ImportResult: BaseResult
{
    /// <summary>
    /// Имя таблицы, в которую происходил импорт
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Обработано строк
    /// </summary>
    public int RowsProcessed { get; set; }

    /// <summary>
    /// Добавлено новых записей
    /// </summary>
    public int RowsInserted { get; set; }

    /// <summary>
    /// Обновлено существующих записей
    /// </summary>
    public int RowsUpdated { get; set; }

    /// <summary>
    /// Пропущено записей
    /// </summary>
    public int RowsSkipped { get; set; }

    /// <summary>
    /// Количество ошибок
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Время выполнения операции
    /// </summary>
    public double ElapsedTimeMs { get; set; }

    /// <summary>
    /// Детали ошибок при импорте
    /// </summary>
    public List<ImportError> Errors { get; set; } = [];

    public List<Dictionary<string,object>> DuplicatedRows { get; set; } = [];
}