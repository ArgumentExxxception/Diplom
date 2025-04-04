namespace Core.Models;

public class TableImportRequestModel
{
    /// <summary>
    /// Имя таблицы для импорта
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Определения колонок таблицы
    /// </summary>
    public List<ColumnInfo> Columns { get; set; }

    public string Delimiter { get; set; }

    public bool HasHeaderRow { get; set; }

    public string? Encoding { get; set; }

    public string XmlRootElement { get; set; }
    public string XmlRowElement { get; set; }
    
    public int SkipRows { get; set; }

    public string UserEmail { get; set; }
    public string TableComment { get; set; }
    
    

    /// <summary>
    /// Перезаписать существующую таблицу или обновить данные
    /// </summary>
    public int ImportMode { get; set; }

    public bool IsNewTable { get; set; }
}