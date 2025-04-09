using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Domain.Enums;

namespace Domain.Entities;

[Table("background_tasks", Schema = "appschema")]
public class BackgroundTaskEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [Column("name")]
    [MaxLength(255)]
    public string Name { get; set; }
    
    [Column("description")]
    [MaxLength(1000)]
    public string Description { get; set; }
    
    [Required]
    [Column("status")]
    public BackgroundTaskStatus Status { get; set; } = BackgroundTaskStatus.Pending;
    
    [Column("progress")]
    public int Progress { get; set; }
    
    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [Column("started_at")]
    public DateTime? StartedAt { get; set; }
    
    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }
    
    [Column("error_message")]
    [MaxLength(4000)]
    public string? ErrorMessage { get; set; }
    
    [Required]
    [Column("user_id")]
    [MaxLength(255)]
    public string UserId { get; set; }
    
    [Required]
    [Column("task_type")]
    public BackgroundTaskType TaskType { get; set; }
    
    [Column("task_data", TypeName = "jsonb")]
    public string TaskDataJson { get; set; }
    
    [Column("result_data", TypeName = "jsonb")]
    public string ResultDataJson { get; set; }
    
    [Column("cancellation_requested")]
    public bool CancellationRequested { get; set; }

    [NotMapped]
    public Dictionary<string, object> TaskData
    {
        get => !string.IsNullOrEmpty(TaskDataJson) 
            ? JsonSerializer.Deserialize<Dictionary<string, object>>(TaskDataJson) 
            : new Dictionary<string, object>();
        set => TaskDataJson = JsonSerializer.Serialize(value);
    }

    [NotMapped]
    public object Result
    {
        get => !string.IsNullOrEmpty(ResultDataJson) 
            ? JsonSerializer.Deserialize<object>(ResultDataJson) 
            : null;
        set => ResultDataJson = JsonSerializer.Serialize(value);
    }
}