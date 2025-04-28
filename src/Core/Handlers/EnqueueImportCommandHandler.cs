using Core.Commands;
using Core.Models;
using MediatR;

namespace Core.Handlers;


public class EnqueueImportCommandHandler : IRequestHandler<EnqueueImportCommand, BackgroundTask>
{
    private readonly IBackgroundTaskService _backgroundTaskService;

    public EnqueueImportCommandHandler(IBackgroundTaskService backgroundTaskService)
    {
        _backgroundTaskService = backgroundTaskService;
    }

    public async Task<BackgroundTask> Handle(EnqueueImportCommand request, CancellationToken cancellationToken)
    {
        return await _backgroundTaskService.EnqueueImportTaskAsync(
            request.FileName,
            request.FileSize,
            request.Request,
            request.FileStream,
            request.ContentType,
            request.UserEmail);
    }
}