using Core.Models;
using MediatR;

namespace Core.Commands;

public class ExportDataCommand: IRequest<(ExportResult result, Stream dataStream)>
{
    public TableExportRequestModel ExportRequest { get; }
    
    public ExportDataCommand(TableExportRequestModel exportRequest)
    {
        ExportRequest = exportRequest;
    }
}