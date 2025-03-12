using Core;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class DatabaseService: IDatabaseService
{
    private readonly IUnitOfWork _unitOfWork;
    public DatabaseService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    public async Task<IEnumerable<string>> GetPublicTablesAsync()
    {
        var sqlQuery = @"
            SELECT table_name 
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_type = 'BASE TABLE';";

        var tableNames = await _unitOfWork.ExecuteQueryAsync<string>(sqlQuery);

        return tableNames;
    }
    
    public async Task CreateTableAsync(MappingRequest request)
    {
        // Проверяем входные данные
        if (request.MappedColumns == null || request.FileData == null)
        {
            throw new ArgumentException("Некорректные данные.");
        }

        // Создаём новую таблицу (пример с использованием EF Core)
        foreach (var row in request.FileData)
        {
            var entity = new Dictionary<string, object>();

            foreach (var mapping in request.MappedColumns)
            {
                var fileColumn = mapping.Key; // Название столбца из файла
                var dbField = mapping.Value; // Название поля в таблице

                if (row.ContainsKey(fileColumn))
                {
                    entity[dbField] = ConvertValue(row[fileColumn]); // Преобразуем значение
                }
            }

            // Добавляем запись в таблицу
            await AddEntityToDatabase(entity);
        }

        await _unitOfWork.SaveChangesAsync();
    }
    
    private object ConvertValue(string value)
    {
        // Пример преобразования значений (можно расширить под ваши типы данных)
        if (int.TryParse(value, out var intValue))
        {
            return intValue;
        }
        if (DateTime.TryParse(value, out var dateValue))
        {
            return dateValue;
        }
        return value; // По умолчанию строка
    }

    private async Task AddEntityToDatabase(Dictionary<string, object> entity)
    {
        // Здесь можно использовать рефлексию или динамическое добавление записи
        // Пример для простой таблицы:
        var tableName = "YourTableName"; // Имя таблицы (можно сделать динамическим)
        var sqlQuery = $"INSERT INTO {tableName} ({string.Join(", ", entity.Keys)}) VALUES ({string.Join(", ", entity.Values)})";
        await _unitOfWork.ExecuteQueryAsync<string>(sqlQuery);
    }
    
}