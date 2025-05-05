using Core.Models;

namespace Core.ServiceInterfaces;

public interface IDuplicatesUpdateService
{
    Task UpdateDuplicatesAsync(
        string tableName,
        List<Dictionary<string, object>> duplicatedRows,
        List<ColumnInfo> columns,
        string userEmail,
        CancellationToken cancellationToken = default);
}