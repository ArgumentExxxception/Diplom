using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Enums;

public class ColumnTypesConverter : JsonConverter<ColumnTypes>
{
    public override ColumnTypes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Получаем строковое значение из JSON
        var value = reader.GetString();

        // Ищем соответствующий enum по значению Display.Name
        foreach (var field in typeToConvert.GetFields())
        {
            if (field.GetCustomAttributes(typeof(DisplayAttribute), false) is DisplayAttribute[] attributes
                && attributes.Length > 0
                && attributes[0].Name == value)
            {
                return (ColumnTypes)field.GetValue(null);
            }
        }

        // Если не нашли, выбрасываем исключение
        throw new JsonException($"Неизвестное значение: {value}");
    }

    public override void Write(Utf8JsonWriter writer, ColumnTypes value, JsonSerializerOptions options)
    {
        // Получаем поле enum
        var field = value.GetType().GetField(value.ToString());

        // Получаем атрибут Display
        if (field.GetCustomAttributes(typeof(DisplayAttribute), false) is DisplayAttribute[] attributes
            && attributes.Length > 0)
        {
            // Записываем значение Display.Name в JSON
            writer.WriteStringValue(attributes[0].Name);
        }
        else
        {
            // Если атрибут Display отсутствует, записываем стандартное имя enum
            writer.WriteStringValue(value.ToString());
        }
    }
}