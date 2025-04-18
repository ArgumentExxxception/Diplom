using Core.Commands;
using MediatR;

namespace Core.Handlers;

public class UpdateDublicatesCommandHandler: IRequestHandler<UpdateDuplicatesCommand, string>
{
    private readonly IFileHandlerService _fileHandlerService;

    public UpdateDublicatesCommandHandler(IFileHandlerService fileHandlerService)
    {
        _fileHandlerService = fileHandlerService;
    }
    
    public async Task<string> Handle(UpdateDuplicatesCommand request, CancellationToken cancellationToken)
    {
        await _fileHandlerService.UpdateDuplicatesAsync(request.TableName, request.Duplicates, request.ColumnInfoList, request.UserEmail, cancellationToken);
        return "Дубликаты успешно обновлены";
    }
}