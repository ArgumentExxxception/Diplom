using Core;
using Core.Models;
using Core.ServiceInterfaces;

namespace Infrastructure.Services;

public class DuplicatesUpdateService: IDuplicatesUpdateService
{
    private readonly IDataImportRepository _dataImportRepository;

    public DuplicatesUpdateService(IDataImportRepository dataImportRepository)
    {
        _dataImportRepository = dataImportRepository;
    }

    public async Task UpdateDuplicatesAsync(string tableName, List<Dictionary<string, object>> duplicatedRows, List<ColumnInfo> columns, string userEmail,
        CancellationToken cancellationToken = default)
    {
        if (duplicatedRows == null || duplicatedRows.Count == 0)
            return;

        var searchColumns = columns
            .Where(c => c.SearchInDuplicates)
            .Select(c => c.Name)
            .ToList();

        var filters = duplicatedRows.Select(row =>
            searchColumns.ToDictionary(col => col, col => row[col])
        ).ToList();

        await _dataImportRepository.DeleteDuplicatesAsync(tableName, filters, cancellationToken);

        await _dataImportRepository.ImportDataBatchAsync(tableName, duplicatedRows, new TableModel
        {
            TableName = tableName,
            Columns = columns
        }, userEmail, cancellationToken);
    }
}