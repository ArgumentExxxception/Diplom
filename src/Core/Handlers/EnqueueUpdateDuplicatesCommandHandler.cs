using Core.Commands;
using MediatR;

namespace Core.Handlers;

public class EnqueueUpdateDuplicatesCommandHandler: IRequestHandler<EnqueueUpdateDuplicatesCommand, string>
{
    private readonly IBackgroundTaskService _backgroundTaskService;

    public EnqueueUpdateDuplicatesCommandHandler(IBackgroundTaskService backgroundTaskService)
    {
        _backgroundTaskService = backgroundTaskService;
    }
    
    public async Task<string> Handle(EnqueueUpdateDuplicatesCommand request, CancellationToken cancellationToken)
    {
        await _backgroundTaskService.EnqueueUpdateDuplicatesTaskAsync(request.TableName, request.Duplicates, request.ColumnInfoList,request.UserEmail);
        return "Обновление дубликатов запущено в фоновом процессе";
    }
}