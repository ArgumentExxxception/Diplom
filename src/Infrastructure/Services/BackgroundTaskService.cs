using Core;
using Core.Enums;
using Core.Logging;
using Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Services;

public class BackgroundTaskService : IBackgroundTaskService
{
    private readonly Dictionary<Guid, BackgroundTask> _tasks = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _cancellationTokens = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxConcurrentTasks;
    private readonly IServiceScopeFactory _serviceScopeFactory; // Добавляем IServiceScopeFactory

    public event EventHandler<BackgroundTask> TaskStatusChanged;
    public event EventHandler<BackgroundTask> TaskCompleted;

    public BackgroundTaskService(
        IServiceScopeFactory serviceScopeFactory, // Инжектируем IServiceScopeFactory вместо IFileHandlerService
        int maxConcurrentTasks = 3)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _maxConcurrentTasks = maxConcurrentTasks;
        _semaphore = new SemaphoreSlim(maxConcurrentTasks, maxConcurrentTasks);
    }

    public async Task<BackgroundTask> EnqueueImportTaskAsync(
        string fileName,
        long fileSize,
        TableImportRequestModel importRequest,
        Stream fileStream,
        string contentType,
        string userName)
    {
        var tempFilePath = Path.GetTempFileName();
        using (var tempFile = File.Create(tempFilePath))
        {
            await fileStream.CopyToAsync(tempFile);
        }

        var task = new BackgroundTask
        {
            Name = $"Import {fileName}",
            Description = $"Importing {fileName} to table {importRequest.TableName}",
            Status = BackgroundTaskStatus.Pending,
            TaskType = BackgroundTaskType.Import,
            UserId = userName,
            TaskData = new Dictionary<string, object>
            {
                { "FileName", fileName },
                { "FileSize", fileSize },
                { "TableName", importRequest.TableName },
                { "ImportMode", importRequest.ImportMode }
            }
        };

        _tasks[task.Id] = task;
        var cts = new CancellationTokenSource();
        _cancellationTokens[task.Id] = cts;

        // Start the task in the background
        _ = Task.Run(async () =>
        {
            try
            {
                using (var tempFileStream = File.OpenRead(tempFilePath))
                {
                    await ExecuteImportTaskAsync(task, tempFileStream, fileName, 
                        contentType, importRequest, userName);
                }
            }
            finally
            {
                File.Delete(tempFilePath);
            }
        });

        return task;
    }

    private async Task ExecuteImportTaskAsync(
        BackgroundTask task,
        Stream fileStream,
        string fileName,
        string contentType,
        TableImportRequestModel importRequest,
        string userName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Wait for a slot to become available
            await _semaphore.WaitAsync(cancellationToken);

            // Update task status
            task.Status = BackgroundTaskStatus.Running;
            task.StartedAt = DateTime.UtcNow;
            OnTaskStatusChanged(task);

            // Create a memory stream to store the file content
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            // Создаем область видимости для scoped-сервисов
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                // Получаем FileHandlerService из DI в рамках этой области видимости
                var fileHandlerService = scope.ServiceProvider.GetRequiredService<IFileHandlerService>();

                // Execute the import
                var result = await fileHandlerService.ImportDataAsync(
                    memoryStream,
                    fileName,
                    contentType,
                    importRequest,
                    cancellationToken);

                // Update task with results
                task.CompletedAt = DateTime.UtcNow;
                task.Status = result.Success ? BackgroundTaskStatus.Completed : BackgroundTaskStatus.Failed;
                task.Progress = 100;
                task.Result = result;

                if (!result.Success)
                {
                    task.ErrorMessage = result.Message;
                }
            }

            OnTaskStatusChanged(task);
            OnTaskCompleted(task);
        }
        catch (OperationCanceledException)
        {
            task.Status = BackgroundTaskStatus.Cancelled;
            task.CompletedAt = DateTime.UtcNow;
            task.ErrorMessage = "Task was cancelled";
            OnTaskStatusChanged(task);
            OnTaskCompleted(task);
        }
        catch (Exception ex)
        {
            task.Status = BackgroundTaskStatus.Failed;
            task.CompletedAt = DateTime.UtcNow;
            task.ErrorMessage = ex.Message;
            OnTaskStatusChanged(task);
            OnTaskCompleted(task);
        }
        finally
        {
            // Release the semaphore slot
            _semaphore.Release();
        }
    }

    public List<BackgroundTask> GetActiveTasks()
    {
        return _tasks.Values
            .Where(t => t.Status == BackgroundTaskStatus.Pending || t.Status == BackgroundTaskStatus.Running)
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
    }

    public BackgroundTask GetTaskById(Guid taskId)
    {
        return _tasks.TryGetValue(taskId, out var task) ? task : null;
    }

    public List<BackgroundTask> GetTasksByUser(string userId)
    {
        return _tasks.Values
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
    }

    public async Task<bool> CancelTaskAsync(Guid taskId)
    {
        if (_cancellationTokens.TryGetValue(taskId, out var cts) && _tasks.TryGetValue(taskId, out var task))
        {
            if (task.Status == BackgroundTaskStatus.Pending || task.Status == BackgroundTaskStatus.Running)
            {
                cts.Cancel();
                return true;
            }
        }
        return false;
    }

    public void CleanupOldTasks(TimeSpan olderThan)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        var oldTaskIds = _tasks.Values
            .Where(t => (t.Status == BackgroundTaskStatus.Completed || 
                         t.Status == BackgroundTaskStatus.Failed || 
                         t.Status == BackgroundTaskStatus.Cancelled) &&
                        t.CompletedAt.HasValue && 
                        t.CompletedAt.Value < cutoff)
            .Select(t => t.Id)
            .ToList();

        foreach (var id in oldTaskIds)
        {
            _tasks.Remove(id);
            if (_cancellationTokens.ContainsKey(id))
            {
                _cancellationTokens.Remove(id);
            }
        }
    }

    protected virtual void OnTaskStatusChanged(BackgroundTask task)
    {
        TaskStatusChanged?.Invoke(this, task);
    }

    protected virtual void OnTaskCompleted(BackgroundTask task)
    {
        TaskCompleted?.Invoke(this, task);
    }
}