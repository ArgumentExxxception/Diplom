using Core.Models;
using Core.Results;

namespace Core.ServiceInterfaces;

public interface IXmlImportService
{
    Task ProcessXMLFileAsync(Stream fileStream, TableImportRequestModel importRequest, string userName, ImportResult result, CancellationToken cancellationToken);
}