using Core.Models;

namespace Core
{
    public interface IBackgroundTaskService
    {
        Task<BackgroundTask> EnqueueImportTaskAsync(
            string fileName, 
            long fileSize, 
            TableImportRequestModel importRequest, 
            Stream fileStream,
            string contentType,
            string userName);

        public Task<BackgroundTask> EnqueueExportTaskAsync(
            TableExportRequestModel exportRequest,
            string userName,
            CancellationToken cancellationToken = default);

        Task<List<BackgroundTask>> GetActiveTasksAsync();

        Task<BackgroundTask> GetTaskByIdAsync(Guid taskId);
        
        // Если необходима синхронная версия, можно определить метод-обёртку, возвращающий Task<T>
        Task<BackgroundTask> GetTaskById(Guid taskId);

        Task<List<BackgroundTask>> GetTasksByUserAsync(string userId);

        Task<bool> CancelTaskAsync(Guid taskId);

        Task CleanupOldTasksAsync(TimeSpan olderThan);

        event EventHandler<BackgroundTask> TaskStatusChanged;

        event EventHandler<BackgroundTask> TaskCompleted;
    }
}