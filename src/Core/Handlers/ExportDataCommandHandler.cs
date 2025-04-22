using Core.Commands;
using Core.ServiceInterfaces;
using MediatR;

namespace Core.Handlers;

public class ExportDataCommandHandler: IRequestHandler<ExportDataCommand, (ExportResult Result, Stream DataStream)>
{
    private readonly IFileExportService _fileExportService;

    public ExportDataCommandHandler(IFileExportService fileExportService)
    {
        _fileExportService = fileExportService;
    }
    
    public async Task<(ExportResult Result, Stream DataStream)> Handle(ExportDataCommand request, CancellationToken cancellationToken)
    {
        return await _fileExportService.ExportDataAsync(request.ExportRequest, cancellationToken);
    }
}