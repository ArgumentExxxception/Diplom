using System.Text;
using Core;
using Core.Enums;
using Core.Models;
using Infrastructure.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class DatabaseService: IDatabaseService
{
    private readonly IUnitOfWork _unitOfWork;
    public DatabaseService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    public async Task<List<TableModel>> GetPublicTablesAsync()
    {
        // Запрос для получения списка публичных таблиц
        var query = @"
        SELECT table_name 
        FROM information_schema.tables 
        WHERE table_schema = 'public';";

        // Выполняем запрос и получаем список имен таблиц
        var tableNames = await _unitOfWork.ExecuteQueryAsync<string>(query);

        // Создаем список TableModel для каждой таблицы
        var tables = new List<TableModel>();

        foreach (string tableName in tableNames)
        {
            List<ColumnInfo> columnInfos = await GetColumnInfoAsync(tableName);
            // Создаем TableModel
            var tableModel = new TableModel
            {
                TableName = tableName,
                Columns = columnInfos,
                TableData = new List<string>(), // Можно добавить логику для получения данных таблицы
                PrimaryKey = "" // Можно добавить логику для определения первичного ключа
            };

            tables.Add(tableModel);
        }

        return tables;
    }

    public async Task<List<ColumnInfo>> GetColumnInfoAsync(string tableName)
    {
        // Получаем информацию о колонках для каждой таблицы
        var columnsQuery = $@"
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns 
            WHERE table_name = '{tableName}' 
            AND table_schema = 'public';";
        
        var columns = await _unitOfWork.ExecuteQueryAsync<ColumnInfoDto>(columnsQuery);
        // Преобразуем ColumnInfoDto в ColumnInfo
        var columnInfos = columns.Select(c => new ColumnInfo
        {
            Name = c.ColumnName,
            Type = MapDataType(c.DataType),
            IsRequired = c.IsNullable == "NO",
            IsPrimaryKey = false // Можно добавить логику для определения первичного ключа
        }).ToList();
        
        return columnInfos;
    }

    private int MapDataType(string dataType)
    {
        return dataType.ToLower() switch
        {
            "text" => 0,
            "integer" => 1,
            "date" => 2,
            "boolean" => 3,
            "double" => 4,
            "timestamp with time zone" => 2,
            _ => throw new NotSupportedException($"Тип данных {dataType} не поддерживается.")
        };
    }
    
    public async Task CreateTableAsync(TableModel tableModel)
    {
        if (tableModel == null)
        {
            throw new ArgumentNullException(nameof(tableModel), "Модель таблицы не может быть null.");
        }

        if (string.IsNullOrWhiteSpace(tableModel.TableName))
        {
            throw new ArgumentException("Название таблицы не может быть пустым.", nameof(tableModel.TableName));
        }

        if (tableModel.Columns == null || tableModel.Columns.Count == 0)
        {
            throw new ArgumentException("Таблица должна содержать хотя бы одну колонку.", nameof(tableModel.Columns));
        }
        
        // var tableExistsQuery = $"SELECT 1 FROM information_schema.tables WHERE table_name = '{tableModel.TableName.ToLower()}';";
        // var tableExists = await _unitOfWork.ExecuteScalarAsync<int>(tableExistsQuery);
        //
        // if (tableExists== 1)
        // {
        //     throw new InvalidOperationException($"Таблица с именем '{tableModel.TableName}' уже существует.");
        // }
        
        if (!string.IsNullOrEmpty(tableModel.PrimaryKey))
        {
            var primaryKeyColumn = tableModel.Columns.FirstOrDefault(c => c.Name == tableModel.PrimaryKey);
            if (primaryKeyColumn == null)
            {
                throw new ArgumentException($"Колонка '{tableModel.PrimaryKey}', указанная как первичный ключ, не найдена в списке колонок.", nameof(tableModel.PrimaryKey));
            }

            if (!primaryKeyColumn.IsRequired)
            {
                throw new ArgumentException($"Колонка '{tableModel.PrimaryKey}', указанная как первичный ключ, должна быть обязательной (NOT NULL).", nameof(tableModel.PrimaryKey));
            }
        }
        
        var query = new StringBuilder();
        query.Append($"CREATE TABLE IF NOT EXISTS {EscapeIdentifier(tableModel.TableName)} (");

        foreach (var column in tableModel.Columns)
        {
            query.Append($"{EscapeIdentifier(column.Name)} {(ColumnTypes)column.Type} ");
            query.Append(column.IsRequired ? "NULL" : "NOT NULL");
            query.Append(", ");
        }
        
        query.Append("LastModifiedOn TIMESTAMPTZ NOT NULL DEFAULT NOW(), ");
        query.Append("LastModifiedBy TEXT NOT NULL, ");

        if (!string.IsNullOrEmpty(tableModel.PrimaryKey))
        {
            query.Append($"PRIMARY KEY ({EscapeIdentifier(tableModel.PrimaryKey)}), ");
        }

        query.Length -= 2;
        query.Append(");");

        try
        {
            await _unitOfWork.ExecuteQueryAsync<string>(query.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при создании таблицы: {ex.Message}");
            throw new InvalidOperationException("Не удалось создать таблицу. Подробности см. в логах.", ex);
        }

    }
    
    private object ConvertValue(string value)
    {
        if (int.TryParse(value, out var intValue))
        {
            return intValue;
        }
        if (DateTime.TryParse(value, out var dateValue))
        {
            return dateValue;
        }
        return value;
    }

    private async Task AddEntityToDatabase(Dictionary<string, object> entity)
    {
        var tableName = "YourTableName"; // Имя таблицы (можно сделать динамическим)
        var sqlQuery = $"INSERT INTO {tableName} ({string.Join(", ", entity.Keys)}) VALUES ({string.Join(", ", entity.Values)})";
        await _unitOfWork.ExecuteQueryAsync<string>(sqlQuery);
    }
    
    private string EscapeIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"")}\"";
    
}