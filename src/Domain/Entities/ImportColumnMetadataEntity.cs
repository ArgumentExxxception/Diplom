using System.Security.Cryptography;

namespace Domain.Entities;

public class ImportColumnMetadataEntity
{
    public int Id { get; set; }
    public string TableName { get; set; }
    public string ColumnName { get; set; }
    public bool IsRequired { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsGeoTag { get; set; }
    public bool SearchInDuplicates { get; set; }
}