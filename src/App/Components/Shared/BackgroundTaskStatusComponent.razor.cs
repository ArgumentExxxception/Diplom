using Core.Models;
using Domain.Enums;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace App.Components.Shared;

public partial class BackgroundTaskStatusComponent : ComponentBase
{
    private List<BackgroundTask> activeTasks = new();
    private HashSet<Guid> expandedTaskIds = new();
    private Timer refreshTimer;

    protected override void OnInitialized()
    {
        BackgroundTaskService.TaskStatusChanged += OnTaskStatusChanged;
        BackgroundTaskService.TaskCompleted += OnTaskCompleted;

        // Initial load of active tasks
        RefreshActiveTasks();

        // Set up a timer to refresh the task list
        refreshTimer = new Timer(_ =>
        {
            RefreshActiveTasks();
            InvokeAsync(StateHasChanged);
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    private void RefreshActiveTasks()
    {
        var tasks = BackgroundTaskService.GetActiveTasks();

        // Add recently completed tasks that haven't been dismissed
        var recentlyCompleted = BackgroundTaskService.GetTasksByUser(null)
            .Where(t => t.Status != BackgroundTaskStatus.Running &&
                        t.Status != BackgroundTaskStatus.Pending &&
                        t.CompletedAt.HasValue &&
                        (DateTime.UtcNow - t.CompletedAt.Value).TotalMinutes < 10)
            .ToList();

        activeTasks = tasks.Concat(recentlyCompleted)
            .OrderByDescending(t => t.Status == BackgroundTaskStatus.Running)
            .ThenByDescending(t => t.Status == BackgroundTaskStatus.Pending)
            .ThenByDescending(t => t.CompletedAt)
            .ToList();
    }

    private void OnTaskStatusChanged(object sender, BackgroundTask task)
    {
        InvokeAsync(() =>
        {
            RefreshActiveTasks();
            StateHasChanged();
        });
    }

    private void OnTaskCompleted(object sender, BackgroundTask task)
    {
        InvokeAsync(() =>
        {
            RefreshActiveTasks();
            StateHasChanged();
        });
    }

    private void ToggleTaskExpand(Guid taskId)
    {
        if (expandedTaskIds.Contains(taskId))
            expandedTaskIds.Remove(taskId);
        else
            expandedTaskIds.Add(taskId);
    }

    private async Task CancelTask(Guid taskId)
    {
        await BackgroundTaskService.CancelTaskAsync(taskId);
    }

    private void DismissTask(Guid taskId)
    {
        var task = BackgroundTaskService.GetTaskById(taskId);
        if (task != null &&
            (task.Status == BackgroundTaskStatus.Completed ||
             task.Status == BackgroundTaskStatus.Failed ||
             task.Status == BackgroundTaskStatus.Cancelled))
        {
            activeTasks.Remove(task);
            if (expandedTaskIds.Contains(taskId))
                expandedTaskIds.Remove(taskId);
        }
    }

    private string GetTaskStatusText(BackgroundTask task)
    {
        return task.Status switch
        {
            BackgroundTaskStatus.Pending => "Pending...",
            BackgroundTaskStatus.Running => "Running...",
            BackgroundTaskStatus.Completed => "Completed",
            BackgroundTaskStatus.Failed => "Failed",
            BackgroundTaskStatus.Cancelled => "Cancelled",
            _ => task.Status.ToString()
        };
    }

    private Color GetTaskColor(BackgroundTask task)
    {
        return task.Status switch
        {
            BackgroundTaskStatus.Pending => Color.Info,
            BackgroundTaskStatus.Running => Color.Primary,
            BackgroundTaskStatus.Completed => Color.Success,
            BackgroundTaskStatus.Failed => Color.Error,
            BackgroundTaskStatus.Cancelled => Color.Warning,
            _ => Color.Default
        };
    }

    private string GetTaskIcon(BackgroundTask task)
    {
        string icon = task.TaskType switch
        {
            BackgroundTaskType.Import => Icons.Material.Filled.CloudUpload,
            BackgroundTaskType.Export => Icons.Material.Filled.CloudDownload,
            BackgroundTaskType.DataProcessing => Icons.Material.Filled.Storage,
            BackgroundTaskType.SystemMaintenance => Icons.Material.Filled.Settings,
            _ => Icons.Material.Filled.Task
        };

        return icon;
    }

    private string FormatDuration(BackgroundTask task)
    {
        if (task.StartedAt.HasValue)
        {
            var end = task.CompletedAt ?? DateTime.UtcNow;
            var duration = end - task.StartedAt.Value;

            if (duration.TotalHours >= 1)
                return $"{duration.TotalHours:F1}h";
            else if (duration.TotalMinutes >= 1)
                return $"{duration.TotalMinutes:F0}m";
            else
                return $"{duration.TotalSeconds:F0}s";
        }

        return "";
    }

    public void Dispose()
    {
        BackgroundTaskService.TaskStatusChanged -= OnTaskStatusChanged;
        BackgroundTaskService.TaskCompleted -= OnTaskCompleted;
        refreshTimer?.Dispose();
    }
}