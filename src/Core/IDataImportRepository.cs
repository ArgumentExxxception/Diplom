using Core.Models;

namespace Core;

public interface IDataImportRepository
{
    Task UpdateDuplicatedRows(string tableName, List<Dictionary<string, object>> newDataList, List<string> primaryKeys,
        Dictionary<string, object> duplicateRow, CancellationToken cancellationToken = default);
    Task ImportDataBatchAsync(string tableName, List<Dictionary<string, object>> rows, TableModel schema, CancellationToken cancellationToken = default);
    Task ClearTableAsync(string tableName, CancellationToken cancellationToken = default);
    Task<List<Dictionary<string, object>>> GetExistingDataAsync(string tableName, CancellationToken cancellationToken = default);
}