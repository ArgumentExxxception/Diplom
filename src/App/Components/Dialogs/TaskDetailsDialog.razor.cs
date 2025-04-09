using Core.Models;
using Domain.Enums;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace App.Components.Dialogs;

public partial class TaskDetailsDialog : ComponentBase
{
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }
    [Parameter] public BackgroundTask Task { get; set; }
    
    // Вспомогательные методы
    private string GetTaskTypeName(BackgroundTaskType type)
    {
        return type switch
        {
            BackgroundTaskType.Import => "Импорт",
            BackgroundTaskType.Export => "Экспорт",
            BackgroundTaskType.DataProcessing => "Обработка данных",
            BackgroundTaskType.SystemMaintenance => "Обслуживание системы",
            BackgroundTaskType.Other => "Прочее",
            _ => type.ToString()
        };
    }
    
    private string GetTaskStatusName(BackgroundTaskStatus status)
    {
        return status switch
        {
            BackgroundTaskStatus.Pending => "В ожидании",
            BackgroundTaskStatus.Running => "Выполняется",
            BackgroundTaskStatus.Completed => "Завершено",
            BackgroundTaskStatus.Failed => "Ошибка",
            BackgroundTaskStatus.Cancelled => "Отменено",
            _ => status.ToString()
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
        string baseIcon = task.TaskType switch
        {
            BackgroundTaskType.Import => Icons.Material.Filled.CloudUpload,
            BackgroundTaskType.Export => Icons.Material.Filled.CloudDownload,
            BackgroundTaskType.DataProcessing => Icons.Material.Filled.Storage,
            BackgroundTaskType.SystemMaintenance => Icons.Material.Filled.Settings,
            _ => Icons.Material.Filled.Task
        };
        
        return baseIcon;
    }
    
    private async Task CancelTask()
    {
        try
        {
            await BackgroundTaskService.CancelTaskAsync(Task.Id);
            MudDialog.Close();
        }
        catch (Exception)
        {
            // Обработка ошибки
        }
    }
    
    private string FormatValue(object value)
    {
        if (value == null)
            return "-";
        
        // Форматирование значений для наглядности
        return value switch
        {
            DateTime dt => dt.ToString("dd.MM.yyyy HH:mm:ss"),
            bool b => b ? "Да" : "Нет",
            _ => value.ToString()
        };
    }
}