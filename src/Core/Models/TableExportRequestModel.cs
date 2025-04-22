namespace Core.Models;

public class TableExportRequestModel
{
    /// <summary>
    /// Имя таблицы для экспорта
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Формат экспорта (CSV, XML)
    /// </summary>
    public string ExportFormat { get; set; }

    /// <summary>
    /// Включать заголовки для CSV (по умолчанию - да)
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;

    /// <summary>
    /// Разделитель для CSV (по умолчанию - запятая)
    /// </summary>
    public string Delimiter { get; set; } = ",";

    /// <summary>
    /// Кодировка файла (по умолчанию - UTF-8)
    /// </summary>
    public string Encoding { get; set; } = "UTF-8";

    /// <summary>
    /// Корневой элемент для XML
    /// </summary>
    public string XmlRootElement { get; set; } = "root";

    /// <summary>
    /// Элемент строки для XML
    /// </summary>
    public string XmlRowElement { get; set; } = "row";

    /// <summary>
    /// Email пользователя, выполняющего экспорт
    /// </summary>
    public string UserEmail { get; set; }

    /// <summary>
    /// Фильтрация для экспорта (SQL условие WHERE)
    /// </summary>
    public string FilterCondition { get; set; }

    /// <summary>
    /// Максимальное количество экспортируемых строк (0 - без ограничений)
    /// </summary>
    public int MaxRows { get; set; } = 0;

    /// <summary>
    /// Список колонок для экспорта (если пустой - экспортировать все)
    /// </summary>
    public List<string> Columns { get; set; } = new List<string>();
}