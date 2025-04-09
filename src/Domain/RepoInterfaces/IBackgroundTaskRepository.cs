using Domain.Entities;
using Domain.Enums;

namespace Domain.RepoInterfaces;

public interface IBackgroundTaskRepository: IRepository<BackgroundTaskEntity>
{
    /// <summary>
    /// Получает список активных задач (Pending, Running)
    /// </summary>
    Task<List<BackgroundTaskEntity>> GetActiveTasksAsync();
    
    /// <summary>
    /// Получает задачу по ID
    /// </summary>
    Task<BackgroundTaskEntity> GetByIdAsync(Guid id);
    
    /// <summary>
    /// Получает список задач для конкретного пользователя
    /// </summary>
    Task<List<BackgroundTaskEntity>> GetTasksByUserAsync(string userId);
    
    /// <summary>
    /// Получает список задач с определенным статусом
    /// </summary>
    Task<List<BackgroundTaskEntity>> GetTasksByStatusAsync(BackgroundTaskStatus status);
    
    /// <summary>
    /// Обновляет статус задачи
    /// </summary>
    Task UpdateStatusAsync(Guid id, BackgroundTaskStatus status, int progress = 0, string errorMessage = null);
    
    /// <summary>
    /// Помечает задачу как выполненную
    /// </summary>
    Task CompleteTaskAsync(Guid id, object result = null);
    
    /// <summary>
    /// Помечает задачу как завершенную с ошибкой
    /// </summary>
    Task FailTaskAsync(Guid id, string errorMessage);
    
    /// <summary>
    /// Получает список недавно завершенных задач
    /// </summary>
    Task<List<BackgroundTaskEntity>> GetRecentlyCompletedTasksAsync(TimeSpan age);
    
    /// <summary>
    /// Удаляет старые завершенные задачи
    /// </summary>
    Task CleanupOldTasksAsync(TimeSpan age);
    
    /// <summary>
    /// Помечает задачу как запрошенную для отмены
    /// </summary>
    Task RequestCancellationAsync(Guid id);
    
    /// <summary>
    /// Проверяет, была ли задача запрошена для отмены
    /// </summary>
    Task<bool> IsCancellationRequestedAsync(Guid id);
}