using System.Data;
using System.Globalization;
using Core;
using Core.Models;
using Core.ServiceInterfaces;
using Core.Utils;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

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
    
    public async Task ImportDataBatchAsync(string tableName, List<Dictionary<string, object>> rows, TableModel schema,
        CancellationToken cancellationToken = default)
    {
        if (await _databaseService.GetTableAsync(schema.TableName) == null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await _databaseService.CreateTableAsync(schema);

            schema.Columns.Add(new ColumnInfo
            {
                Name = DataProcessingUtils.MODIFIED_BY_COLUMN,
                Type = (int)ColumnTypes.Text,
                IsRequired = false
            });
            // schema.Columns.Add(new ColumnInfo
            // {
            //     Name = DataProcessingUtils.MODIFIED_DATE_COLUMN,
            //     Type = (int)ColumnTypes.Date,
            //     IsRequired = false
            // });
            await SaveColumnMetadataAsync(tableName, schema.Columns, cancellationToken);
        }
        var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();
         if (connection.State != ConnectionState.Open)
         {
             await connection.OpenAsync(cancellationToken);
         }
    
         string columnList = string.Join(", ", schema.Columns.Select(c => $"\"{c.Name}\""));
         string copyCommand = $"COPY \"{tableName}\" ({columnList}) FROM STDIN (FORMAT BINARY)";
    
         await using var writer = await connection.BeginBinaryImportAsync(copyCommand, cancellationToken);
         
         foreach (var row in rows)
         { 
             await writer.StartRowAsync(cancellationToken);
            foreach (var column in schema.Columns)
            {
                object? value = row.TryGetValue(column.Name, out var v) ? v : null;
                
                if (value == null || value is DBNull)
                    await writer.WriteNullAsync(cancellationToken);
                else
                    await ConvertToNpgsqlType(value, column, writer);
            }
         }
    
         await writer.CompleteAsync(cancellationToken);
    }

    private async Task ConvertToNpgsqlType(object? value, ColumnInfo column, NpgsqlBinaryImporter writer)
    {
        switch ((ColumnTypes)column.Type)
        {
            case ColumnTypes.Text:
                await writer.WriteAsync(value?.ToString(), NpgsqlDbType.Text);
                break;
            case ColumnTypes.Integer:
                await writer.WriteAsync(Convert.ToInt32(value), NpgsqlDbType.Integer);
                break;
            case ColumnTypes.Double:
                if (value is double d)
                    await writer.WriteAsync(d, NpgsqlDbType.Double);
                else if (value is float f)
                    await writer.WriteAsync((double)f, NpgsqlDbType.Double);
                else if (value is decimal dec)
                    await writer.WriteAsync(dec, NpgsqlDbType.Numeric);
                else
                    await writer.WriteAsync(Convert.ToDouble(value), NpgsqlDbType.Double);
                break;
            case ColumnTypes.Date:
            {
                // Null-значения уже обработаны выше
                DateTime dateValue;

                switch (value)
                {
                    case DateTime dt:
                        dateValue = dt.Date;
                        break;

                    case DateTimeOffset dto:
                        dateValue = dto.Date; 
                        break;

                    case string s:
                        // Попытаться спарсить как ISO-8601 или общий формат
                        if (!DateTime.TryParse(
                                s,
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                out dateValue))
                        {
                            throw new FormatException($"Не удалось распознать дату из строки: '{s}'");
                        }
                        dateValue = dateValue.Date;
                        break;

                    case long l:
                        // Unix-время в секундах
                        dateValue = DateTimeOffset.FromUnixTimeSeconds(l)
                                                  .UtcDateTime
                                                  .Date;
                        break;

                    case double dbl:
                        // интерпретируем как Unix-время (если больше 1_000_000_000) 
                        // или как OLE Automation Date (если меньше)
                        if (Math.Abs(dbl) > 1_000_000_000)
                        {
                            dateValue = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(dbl))
                                                      .UtcDateTime
                                                      .Date;
                        }
                        else
                        {
                            dateValue = DateTime.FromOADate(dbl).Date;
                        }
                        break;

                    case float f:
                        dateValue = DateTime.FromOADate(f).Date;
                        break;

                    case decimal dec:
                        dateValue = DateTime.FromOADate((double)dec).Date;
                        break;

                    default:
                        // Попытаться конвертировать через ToString()
                        if (DateTime.TryParse(
                                value.ToString(),
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                out dateValue))
                        {
                            dateValue = dateValue.Date;
                        }
                        else
                        {
                            throw new InvalidCastException(
                                $"Невозможно сконвертировать значение '{value}' в дату.");
                        }
                        break;
                }

                await writer.WriteAsync(dateValue, NpgsqlDbType.Date);
                break;
            }
            case ColumnTypes.Boolean:
                await writer.WriteAsync(Convert.ToBoolean(value), NpgsqlDbType.Boolean);
                break;
            default:
                await writer.WriteAsync(value!.ToString(), NpgsqlDbType.Text);
                break;
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