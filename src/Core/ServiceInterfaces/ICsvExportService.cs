using Core.Models;

namespace Core.ServiceInterfaces;

public interface ICsvExportService
{
    Task<ExportResult> ExportToCsvAsync(
        TableExportRequestModel exportRequest,
        Stream outputStream,
        CancellationToken cancellationToken = default);
}