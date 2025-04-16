namespace Core.Models;

public class ColumnInfo
{
    public string Name { get; set; }
    public int Type { get; set; }
    public bool IsRequired { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsGeoTag { get; set; }
    public bool SearchInDuplicates { get; set; }
    
    public ColumnInfo Clone() => new ColumnInfo
    {
        Name = Name,
        Type = Type,
        IsPrimaryKey = IsPrimaryKey,
        IsRequired = IsRequired,
        SearchInDuplicates = SearchInDuplicates,
        IsGeoTag = IsGeoTag
    };
    
}