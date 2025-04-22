using Core.Models;

namespace Core.RepoInterfaces;

public interface IDataExportRepository
{
    Task<(List<Dictionary<string, object>> Rows, List<ColumnInfo> Columns)> GetDataForExportAsync(
        string tableName,
        List<string> columns = null,
        string filterCondition = null,
        int maxRows = 0,
        CancellationToken cancellationToken = default);
}