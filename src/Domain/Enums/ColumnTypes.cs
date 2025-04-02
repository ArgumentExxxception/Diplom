using System.ComponentModel.DataAnnotations;

namespace Domain.Enums;

public enum ColumnTypes
{
    [Display(Name = "Текст")]
    Text = 0,
    [Display(Name = "Целое число")]
    Integer = 1,
    [Display(Name = "Дата")]
    Date = 2,
    [Display(Name = "Логическое выражение")]
    Boolean = 3,
    [Display(Name = "Дробное число")]
    Double = 4,
}