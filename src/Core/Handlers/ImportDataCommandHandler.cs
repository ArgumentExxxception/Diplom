using Core.Commands;
using Core.Results;
using MediatR;

namespace Core.Handlers;

public class ImportDataCommandHandler: IRequestHandler<ImportDataCommand, ImportResult>
{
    private readonly IFileHandlerService _fileHandlerService;

    public ImportDataCommandHandler(IFileHandlerService fileHandlerService)
    {
        _fileHandlerService = fileHandlerService;
    }
    
    public async Task<ImportResult> Handle(ImportDataCommand request, CancellationToken cancellationToken)
    {
        await using var stream = request.FileStream;
        return await _fileHandlerService.ImportDataAsync(
            stream, 
            request.FileName, 
            request.ContentType,
            request.ImportRequest,
            cancellationToken);
    }
}