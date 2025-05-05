using Core.Models;
using Core.Results;

namespace Core.ServiceInterfaces;

public interface IXmlExportService
{
    Task<ExportResult> ExportToXmlAsync(
        TableExportRequestModel exportRequest,
        Stream outputStream,
        CancellationToken cancellationToken = default);
}