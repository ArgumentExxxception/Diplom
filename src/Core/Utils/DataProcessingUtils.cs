using System.Globalization;
using Core.Models;
using Domain.Enums;

namespace Core.Utils;

public static class DataProcessingUtils
{
    public const string MODIFIED_BY_COLUMN = "lastmodifiedby";
    public const string MODIFIED_DATE_COLUMN = "lastmodifiedon";
    
    /// <summary>
    /// Конвертирует строковое значение в целевой тип данных согласно перечислению ColumnTypes
    /// </summary>
    public static object ConvertToTargetType(string value, int dataTypeInt)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DBNull.Value;

        value = value.Trim();

        // Пытаемся преобразовать строковое представление типа в ColumnTypes
        ColumnTypes dataType = (ColumnTypes)dataTypeInt;

        try
        {
            switch (dataType)
            {
                case ColumnTypes.Integer:
                    // Для целых чисел
                    return int.Parse(value, CultureInfo.InvariantCulture);

                case ColumnTypes.Double:
                    // Для дробных чисел, заменяем запятые на точки для международного формата
                    value = value.Replace(',', '.');
                    return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

                case ColumnTypes.Boolean:
                    // Для логических значений
                    if (bool.TryParse(value, out bool boolResult))
                        return boolResult;
                    
                    // Проверяем различные варианты записи
                    value = value.ToLowerInvariant();
                    if (value == "1" || value == "yes" || value == "y" || value == "да" || value == "true" || value == "t")
                        return true;
                    if (value == "0" || value == "no" || value == "n" || value == "нет" || value == "false" || value == "f")
                        return false;
                    
                    throw new FormatException($"Невозможно преобразовать '{value}' в логический тип");

                case ColumnTypes.Date:
                    // Для дат
                    string[] dateFormats = new[]
                    {
                        "dd.MM.yyyy", // Например, 15.06.2021
                        "yyyy-MM-dd",  // Например, 2021-06-15
                        "MM/dd/yyyy",  // Например, 06/15/2021
                        "dd/MM/yyyy",  // Например, 15/06/2021
                        "yyyy/MM/dd",   // Например, 2021/06/15
                        "yyyyMMdd",
                        "yyyyddMM"// Например, 20230315
                    };
            
                    if (DateTime.TryParseExact(value, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
                    {
                        if (dateValue.Kind == DateTimeKind.Unspecified)
                        {
                            dateValue = DateTime.SpecifyKind(dateValue, DateTimeKind.Utc);
                        }
                        return dateValue;
                    }
                    throw new FormatException("Некорректный формат даты.");
                
                case ColumnTypes.Text:
                default:
                    // Для текстовых типов просто возвращаем строку
                    return value;
            }
        }
        catch (Exception ex)
        {
            throw new FormatException($"Невозможно преобразовать '{value}' в тип {dataType}: {ex.Message}", ex);
        }
    }
}