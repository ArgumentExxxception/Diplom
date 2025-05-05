using Core.Models;
using Core.Results;

namespace Core.ServiceInterfaces;

public interface ICsvExportService
{
    Task<ExportResult> ExportToCsvAsync(
        TableExportRequestModel exportRequest,
        Stream outputStream,
        CancellationToken cancellationToken = default);
}