using Core.Enums;

namespace Core.Models;

public class BackgroundTask
{
    /// <summary>
    /// Unique identifier for the task
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Display name for the task
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Description or additional information about the task
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// Current status of the task
    /// </summary>
    public BackgroundTaskStatus Status { get; set; } = BackgroundTaskStatus.Pending;
    
    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Progress { get; set; }
    
    /// <summary>
    /// When the task was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the task was started
    /// </summary>
    public DateTime? StartedAt { get; set; }
    
    /// <summary>
    /// When the task completed (success or failure)
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Error message if the task failed
    /// </summary>
    public string ErrorMessage { get; set; }
    
    /// <summary>
    /// Username who started the task
    /// </summary>
    public string UserId { get; set; }
    
    /// <summary>
    /// Type of task
    /// </summary>
    public BackgroundTaskType TaskType { get; set; }
    
    /// <summary>
    /// Additional data specific to the task
    /// </summary>
    public Dictionary<string, object> TaskData { get; set; } = new Dictionary<string, object>();
    
    /// <summary>
    /// Result data from the task
    /// </summary>
    public object Result { get; set; }
}