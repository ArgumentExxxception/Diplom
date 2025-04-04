namespace Core.Models;

public class ColumnInfo
{
    public string Name { get; set; }
    public int Type { get; set; }
    public bool IsRequired { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsGeoTag { get; set; }
    public bool SearchInDuplicates { get; set; }
    
}