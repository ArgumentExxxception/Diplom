namespace Core.Models;

public class ColumnInfo
{
    public string Name { get; set; } 
    public string Type { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsRequired { get; set; }
    public bool IsUnique { get; set; }
    public string Description { get; set; }
}