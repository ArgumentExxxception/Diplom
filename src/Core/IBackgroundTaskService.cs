using Core.Models;

namespace Core;

public interface IBackgroundTaskService
{
        /// <summary>
        /// Enqueues a new background import task
        /// </summary>
        /// <param name="fileName">Name of the file to import</param>
        /// <param name="fileSize">Size of the file in bytes</param>
        /// <param name="importRequest">Import request parameters</param>
        /// <param name="fileStream">The file stream to process</param>
        /// <param name="contentType">The content type of the file</param>
        /// <param name="userName">Username who initiated the import</param>
        /// <returns>The created background task</returns>
        Task<BackgroundTask> EnqueueImportTaskAsync(
            string fileName, 
            long fileSize, 
            TableImportRequestModel importRequest, 
            Stream fileStream,
            string contentType,
            string userName);

        /// <summary>
        /// Gets all active background tasks
        /// </summary>
        public List<BackgroundTask> GetActiveTasks();

        /// <summary>
        /// Gets a specific task by ID
        /// </summary>
        BackgroundTask GetTaskById(Guid taskId);

        /// <summary>
        /// Gets tasks for a specific user
        /// </summary>
        List<BackgroundTask> GetTasksByUser(string userId);

        /// <summary>
        /// Cancels a task if possible
        /// </summary>
        Task<bool> CancelTaskAsync(Guid taskId);

        /// <summary>
        /// Remove completed tasks older than the specified timespan
        /// </summary>
        void CleanupOldTasks(TimeSpan olderThan);

        /// <summary>
        /// Event that fires when a task's status changes
        /// </summary>
        event EventHandler<BackgroundTask> TaskStatusChanged;

        /// <summary>
        /// Event that fires when a task completes
        /// </summary>
        event EventHandler<BackgroundTask> TaskCompleted;
}