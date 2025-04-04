using Core.Models;
using Core.Results;

namespace Core.ServiceInterfaces;

public interface ICsvImportService
{
    Task ProcessCSVFileAsync(
        Stream fileStream,
        TableImportRequestModel importRequest,
        string userName,
        ImportResult result,
        CancellationToken cancellationToken);
}