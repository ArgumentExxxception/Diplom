using Core.Models;

namespace Core;

public interface IDataImportRepository
{
    Task UpdateDuplicatedRows(string tableName, List<Dictionary<string, object>> newDataList, List<string> primaryKeys,
        Dictionary<string, object> duplicateRow);
    Task ImportDataBatchAsync(string tableName, List<Dictionary<string, object>> rows, TableModel schema);
    Task ClearTableAsync(string tableName);
    Task<List<Dictionary<string, object>>> GetExistingDataAsync(string tableName);
}