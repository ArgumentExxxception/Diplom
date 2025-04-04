using Core.Models;
using Core.Results;

namespace Core;

public interface IFileHandlerService
{
    Task<ImportResult> ImportDataAsync(Stream stream, string fileName, string contentType, TableImportRequestModel importRequest, CancellationToken cancellationToken);
    // Task UpdateDublicates(string tableName, List<Dictionary<string, object>> dublicates);
}