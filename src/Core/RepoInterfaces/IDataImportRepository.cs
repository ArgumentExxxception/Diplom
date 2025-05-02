using Core.Models;

namespace Core;

public interface IDataImportRepository
{
    Task DeleteDuplicatesAsync(
        string tableName,
        List<Dictionary<string, object>> filters,
        CancellationToken cancellationToken = default);
    Task ImportDataBatchAsync(string tableName, List<Dictionary<string, object>> rows, TableModel schema, string userEmail, CancellationToken cancellationToken = default);
    Task ClearTableAsync(string tableName, CancellationToken cancellationToken = default);
    Task<List<Dictionary<string, object>>> GetExistingDataAsync(string tableName, CancellationToken cancellationToken = default);

    Task SaveColumnMetadataAsync(string tableName, List<ColumnInfo> columns,
        CancellationToken cancellationToken = default);
}