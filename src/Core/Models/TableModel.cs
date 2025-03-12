namespace Core.Models;

public class TableModel
{
    public string TableName { get; set; }
    public string Description { get; set; }
    public List<ColumnInfo> Columns { get; set; } = new();
}