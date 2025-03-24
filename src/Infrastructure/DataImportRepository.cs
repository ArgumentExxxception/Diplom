using System.Data;
using System.Text;
using Core;
using Core.Enums;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class DataImportRepository: IDataImportRepository
{
    private readonly Context _dbContext;
    private readonly IDatabaseService _databaseService;

    public DataImportRepository(Context dbContext, IDatabaseService databaseService)
    {
        _dbContext = dbContext;
        _databaseService = databaseService;
    }
    
    public async Task UpdateDuplicatedRows(string tableName, List<Dictionary<string, object>> newDataList, List<string> primaryKeys, Dictionary<string, object> duplicateRow)
    {
        // Открываем транзакцию
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            // Проходим по каждому элементу в списке новых данных
            foreach (var newData in newDataList)
            {
                // Формируем SET часть запроса
                var setClause = string.Join(", ", newData
                    .Where(kv => !primaryKeys.Contains(kv.Key)) // Исключаем первичные ключи из обновления
                    .Select(kv => $"\"{kv.Key}\" = @{kv.Key}"));

                // Формируем WHERE часть запроса
                var whereClause = string.Join(" AND ", primaryKeys
                    .Select(pk => $"\"{pk}\" = @{pk}"));

                // Формируем полный запрос
                var query = $"UPDATE \"{tableName}\" SET {setClause} WHERE {whereClause};";

                // Создаем параметры для запроса
                var parameters = new List<object>();
                foreach (var kv in newData)
                {
                    if (!primaryKeys.Contains(kv.Key)) // Параметры для SET
                    {
                        parameters.Add(($"@{kv.Key}", kv.Value ?? DBNull.Value));
                    }
                }
                foreach (var pk in primaryKeys) // Параметры для WHERE
                {
                    parameters.Add(($"@{pk}", duplicateRow[pk] ?? DBNull.Value));
                }

                // Выполняем запрос
                await _dbContext.Database.ExecuteSqlRawAsync(query, parameters.ToArray());
            }

            // Фиксируем транзакцию
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            // Откатываем транзакцию в случае ошибки
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Ошибка при обновлении дубликатов. Транзакция откачена.", ex);
        }
    }

    public async Task ImportDataBatchAsync(string tableName, List<Dictionary<string, object>> rows, TableModel schema)
    {
        if (rows.Count == 0)
            return;

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
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
                    IsRequired = true
                });

                schema.Columns.Add(new ColumnInfo
                {
                    Name = "lastmodifiedby",
                    Type = (int)ColumnTypes.Text,
                    IsRequired = true
                });
            }

            // Подготавливаем SQL запрос для вставки данных
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
                    object value = rows[i].TryGetValue(column.Name, out var v) ? v : DBNull.Value;
                    parameters.Add(value ?? DBNull.Value);
                }
                
                insertSql.Append(string.Join(", ", rowParams));
                insertSql.Append(")");
            }

            // Выполняем запрос
            await _dbContext.Database.ExecuteSqlRawAsync(insertSql.ToString(), parameters.ToArray());
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new Exception($"Ошибка при импорте данных в таблицу {tableName}: {ex.Message}", ex);
        }
    }

    public async Task ClearTableAsync(string tableName)
    {
        var sql = $"DELETE FROM \"{tableName}\"";
        await _dbContext.Database.ExecuteSqlRawAsync(sql);
    }
    
    public async Task<List<Dictionary<string, object>>> GetExistingDataAsync(string tableName)
    {
        var existingData = new List<Dictionary<string, object>>();
        
        var columns = await _databaseService.GetColumnInfoAsync(tableName);

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
        if (_dbContext.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
        {
            await _dbContext.Database.OpenConnectionAsync();
        }

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
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