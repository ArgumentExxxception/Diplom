using System.Text.Json.Serialization;

namespace Core.Models;

public class TableModel
{
    public string TableName { get; set; }
    public List<ColumnInfo> Columns { get; set; } = [];
    public List<string> TableData { get; set; } = [];
    public string PrimaryKey { get; set; }
}