using System.Text;
using Core;
using Core.DTOs;
using Core.Models;
using Domain.Enums;
using Infrastructure.DTOs;

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
    SELECT 
        t.table_name as table_name,
        d.description as table_comment
    FROM 
        information_schema.tables t
    LEFT JOIN 
        pg_catalog.pg_class c ON c.relname = t.table_name
    LEFT JOIN 
        pg_catalog.pg_namespace n ON n.oid = c.relnamespace AND n.nspname = t.table_schema
    LEFT JOIN 
        pg_catalog.pg_description d ON d.objoid = c.oid AND d.objsubid = 0
    WHERE 
        t.table_schema = 'public';";

        // Выполняем запрос и получаем список имен таблиц
        var tableNames = await _unitOfWork.ExecuteQueryAsync<TableInfoDto>(query);

        // Создаем список TableModel для каждой таблицы
        var tables = new List<TableModel>();

        foreach (var tableInfo in tableNames)
        {
            List<ColumnInfo> columnInfos = await GetColumnInfoAsync(tableInfo.TableName);
            // Создаем TableModel
            var tableModel = new TableModel
            {
                TableName = tableInfo.TableName,
                Columns = columnInfos,
                TableData = new List<string>(), // Можно добавить логику для получения данных таблицы
                PrimaryKey = "",
                TableComment = tableInfo.TableComment ?? string.Empty
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
        // Получаем метаданные из нашей новой сущности, сохранённой в appschema
        // Предполагается, что репозиторий корректно настроен для работы с ImportColumnMetadataEntity
        var metadataList = await _unitOfWork.ImportColumnMetadatas.GetByTableNameAsync(tableName);
    
        // Объединяем базовую информацию с данными из метаданных.
        // Если для колонки найдены метаданные, используем их; иначе, задаём значения по умолчанию.
        var columnInfos = columns.Select(c =>
        {
            var meta = metadataList.FirstOrDefault(m => 
                m.ColumnName.Equals(c.ColumnName, StringComparison.OrdinalIgnoreCase));
        
            return new ColumnInfo
            {
                Name = c.ColumnName,
                Type = MapDataType(c.DataType),
                // Приоритет: если есть метаданные, то брать IsRequired оттуда, иначе из information_schema
                IsRequired = meta != null ? meta.IsRequired : (c.IsNullable.Equals("NO", StringComparison.OrdinalIgnoreCase)),
                IsPrimaryKey = meta?.IsPrimaryKey ?? false,
                IsGeoTag = meta?.IsGeoTag ?? false,
                SearchInDuplicates = meta?.SearchInDuplicates ?? false
            };
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
            query.Append(column.IsRequired ? "NOT NULL" : "NULL");
            query.Append(", ");
        }
        
        query.Append("LastModifiedOn TIMESTAMPTZ NULL DEFAULT NOW(), ");
        query.Append("LastModifiedBy TEXT NULL, ");

        if (!string.IsNullOrEmpty(tableModel.PrimaryKey))
        {
            query.Append($"PRIMARY KEY ({EscapeIdentifier(tableModel.PrimaryKey)}), ");
        }

        query.Length -= 2;
        query.Append(");");
        
        if (!string.IsNullOrWhiteSpace(tableModel.TableComment))
        {
            query.Append($" COMMENT ON TABLE {EscapeIdentifier(tableModel.TableName)} IS '{tableModel.TableComment}';");
        }

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