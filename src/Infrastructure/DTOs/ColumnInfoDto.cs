using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.DTOs;

public class ColumnInfoDto
{
    [Column("column_name")]
    public string ColumnName { get; set; }
    [Column("data_type")] 
    public string DataType { get; set; }
    [Column("is_nullable")] 
    public string IsNullable { get; set; }
}