using System.Data;
using System.Text;
using Core.Models;
using Core.RepoInterfaces;
using Core.ServiceInterfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class DataExportRepository(Context dbContext, IDatabaseService databaseService) : IDataExportRepository
{
    public async Task<(List<Dictionary<string, object>> Rows, List<ColumnInfo> Columns)> GetDataForExportAsync(string tableName, List<string> columns = null, string filterCondition = null, int maxRows = 0,
        CancellationToken cancellationToken = default)
    {
        
        cancellationToken.ThrowIfCancellationRequested();
        
        var tableColumns = await databaseService.GetColumnInfoAsync(tableName);
        
        if (tableColumns == null || tableColumns.Count == 0)
        {
            throw new InvalidOperationException($"Таблица '{tableName}' не найдена или не содержит столбцов.");
        }
        
        List<ColumnInfo> selectedColumns;
        if (columns != null && columns.Count > 0)
        {
            selectedColumns = tableColumns
                .Where(c => columns.Contains(c.Name, StringComparer.OrdinalIgnoreCase))
                .ToList();
            
            if (selectedColumns.Count == 0)
            {
                throw new ArgumentException($"Ни одна из указанных колонок не найдена в таблице '{tableName}'");
            }
        }
        else
        {
            selectedColumns = tableColumns;
        }
        
        var columnNames = selectedColumns.Select(c => $"\"{c.Name}\"");
        var sql = new StringBuilder($"SELECT {string.Join(", ", columnNames)} FROM \"{tableName}\"");

        if (!string.IsNullOrWhiteSpace(filterCondition))
        {
            sql.Append($" WHERE {filterCondition}");
        }

        if (maxRows > 0)
        {
            sql.Append($" LIMIT {maxRows}");
        }

        sql.Append(";");

        var connection = dbContext.Database.GetDbConnection();
        try
        {
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = sql.ToString();
            command.CommandType = CommandType.Text;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var result = new List<Dictionary<string, object>>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var value = reader.GetValue(i);

                    row[columnName] = value == DBNull.Value ? null : value;
                }

                result.Add(row);
            }

            return (result, selectedColumns);
        }
        finally
        {
            if (connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }
}