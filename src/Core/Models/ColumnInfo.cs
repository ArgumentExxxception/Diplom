using System.Text.Json.Serialization;
using Core.Enums;

namespace Core.Models;

public class ColumnInfo
{
    public string Name { get; set; }
    public int Type { get; set; }
    public bool IsRequired { get; set; }
    public bool IsPrimaryKey { get; set; }
}