using System.ComponentModel.DataAnnotations.Schema;

namespace Core.DTOs;

public class TableInfoDto
{
    [Column("table_name")]
    public string TableName { get; set; }
    [Column("table_comment")]
    public string? TableComment { get; set; } = String.Empty;
}