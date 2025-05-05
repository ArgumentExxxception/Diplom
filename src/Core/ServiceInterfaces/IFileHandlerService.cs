using Core.Models;
using Core.Results;

namespace Core.ServiceInterfaces;

public interface IFileHandlerService
{
    Task<ImportResult> ImportDataAsync(Stream stream, string fileName, string contentType, TableImportRequestModel importRequest, CancellationToken cancellationToken);
}