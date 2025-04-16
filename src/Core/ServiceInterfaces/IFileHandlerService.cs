using Core.Models;
using Core.Results;

namespace Core;

public interface IFileHandlerService
{
    Task<ImportResult> ImportDataAsync(Stream stream, string fileName, string contentType, TableImportRequestModel importRequest, CancellationToken cancellationToken);

    Task UpdateDuplicatesAsync(
        string tableName,
        List<Dictionary<string, object>> duplicatedRows,
        List<ColumnInfo> columns,
        CancellationToken cancellationToken = default);
}