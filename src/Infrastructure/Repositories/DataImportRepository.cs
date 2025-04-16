using System.Data;
using System.Text;
using Core;
using Core.Models;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class DataImportRepository: IDataImportRepository
{
    private readonly Context _dbContext;
    private readonly IDatabaseService _databaseService;
    private readonly IUnitOfWork _unitOfWork;

    public DataImportRepository(Context dbContext, IDatabaseService databaseService, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _databaseService = databaseService;
        _unitOfWork = unitOfWork;
    }
    
    public async Task DeleteDuplicatesAsync(
        string tableName,
        List<Dictionary<string, object>> filters,
        CancellationToken cancellationToken = default)
    {
        foreach (var filter in filters)
        {
            var whereClauses = new List<string>();
            var parameters = new List<object>();
            int paramIndex = 0;

            foreach (var kv in filter)
            {
                string paramName = $"@p{paramIndex}";
                object normalizedValue = NormalizeValue(kv.Value);
                // Используем двойные кавычки для экранирования имен колонок в PostgreSQL
                whereClauses.Add($"\"{kv.Key}\" = {paramName}");
                parameters.Add(normalizedValue ?? DBNull.Value);
                paramIndex++;
            }

            string whereClause = string.Join(" AND ", whereClauses);
            // Аналогично для имени таблицы
            string deleteSql = $"DELETE FROM \"{tableName}\" WHERE {whereClause}";

            await _dbContext.Database.ExecuteSqlRawAsync(deleteSql, parameters.ToArray(), cancellationToken);
        }
    }

    
    private object NormalizeValue(object value)
    {
        // Если значение уже не словарь, просто возвращаем его.
        if (!(value is Dictionary<string, object> nested))
            return value;
    
        // Если вложенный словарь содержит ровно один элемент,
        // можно считать, что нас интересует его значение.
        if (nested.Count == 1)
        {
            return nested.Values.First();
        }
    
        // Если структура более сложная, либо выбрасываем исключение, либо возвращаем строковое представление.
        // Здесь можно настроить логику под конкретный сценарий.
        return nested.ToString(); // или throw new InvalidOperationException("Не удалось нормализовать значение фильтра.");
    }
    
    public async Task ImportDataBatchAsync(string tableName, List<Dictionary<string, object>> rows, TableModel schema, CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
            return;
        cancellationToken.ThrowIfCancellationRequested();
        
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            List<TableModel> tables = await _databaseService.GetPublicTablesAsync();

            if (tables.All(table => table.TableName != tableName))
            {
                await _databaseService.CreateTableAsync(schema);
                schema.Columns.Add(new ColumnInfo
                {
                    Name = "lastmodifiedon",
                    Type = (int)ColumnTypes.Date,
                    IsRequired = false
                });

                schema.Columns.Add(new ColumnInfo
                {
                    Name = "lastmodifiedby",
                    Type = (int)ColumnTypes.Text,
                    IsRequired = false
                });
                await SaveColumnMetadataAsync(tableName, schema.Columns);
            }

            // Подготавливаем SQL запрос для вставки данных
            StringBuilder insertSql = new StringBuilder();
            insertSql.Append($"INSERT INTO \"{tableName}\" (");

            // Получаем имена колонок
            var columnNames = schema.Columns.Select(c => $"\"{c.Name}\"").ToList();
            
            insertSql.Append(string.Join(", ", columnNames));
            insertSql.Append(") VALUES ");

            // Добавляем параметры для каждой строки
            List<object> parameters = new List<object>();
            for (int i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (i > 0)
                    insertSql.Append(", ");

                insertSql.Append("(");
                List<string> rowParams = new List<string>();

                // Добавляем параметры для каждой колонки
                foreach (ColumnInfo column in schema.Columns)
                {
                    string paramName = $"@p{parameters.Count}";
                    rowParams.Add(paramName);

                    // Получаем значение или NULL, если его нет
                    object value = rows[i].TryGetValue(column.Name, out var v) ? v : null;
                    parameters.Add(value);
                }
                
                insertSql.Append(string.Join(", ", rowParams));
                insertSql.Append(")");
            }

            // Выполняем запрос
            await _dbContext.Database.ExecuteSqlRawAsync(insertSql.ToString(), parameters.ToArray());
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new Exception($"Ошибка при импорте данных в таблицу {tableName}: {ex.Message}", ex);
        }
    }

    private async Task SaveColumnMetadataAsync(string tableName, List<ColumnInfo> columns,
        CancellationToken cancellationToken = default)
    {
        foreach (var col in columns)
        {
            var metadata = new ImportColumnMetadataEntity()
            {
                TableName = tableName,
                ColumnName = col.Name,
                IsRequired = col.IsRequired,
                IsPrimaryKey = col.IsPrimaryKey,
                IsGeoTag = col.IsGeoTag,
                SearchInDuplicates = col.SearchInDuplicates
            };

            await _unitOfWork.ImportColumnMetadatas.Add(metadata);
        }
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ClearTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sql = $"DELETE FROM \"{tableName}\"";
        await _dbContext.Database.ExecuteSqlRawAsync(sql,cancellationToken);
    }
    
    public async Task<List<Dictionary<string, object>>> GetExistingDataAsync(string tableName, CancellationToken cancellationToken = default)
    {
        var existingData = new List<Dictionary<string, object>>();
        
        var columns = await _databaseService.GetColumnInfoAsync(tableName);
        cancellationToken.ThrowIfCancellationRequested();
        
        if (!columns.Any())
        {
            throw new InvalidOperationException($"Таблица '{tableName}' не найдена или не содержит столбцов.");
        }

        // Формируем SQL-запрос для выборки данных
        var columnNames = columns.Select(c => $"\"{c.Name}\"");
        var query = $"SELECT {string.Join(", ", columnNames)} FROM \"{tableName}\";";

        // Выполняем запрос
        await using var command = _dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = query;
        if (_dbContext.Database.GetDbConnection().State != ConnectionState.Open)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var rowData = new Dictionary<string, object>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var value = reader.GetValue(i);

                // Преобразуем DBNull в null
                rowData[columnName] = value == DBNull.Value ? null : value;
            }

            existingData.Add(rowData);
        }

        return existingData;
    }
}