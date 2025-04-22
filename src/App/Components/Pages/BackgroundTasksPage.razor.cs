using System.Security.Claims;
using App.Components.Dialogs;
using Core.Models;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace App.Components.Pages;

[Authorize]
public partial class BackgroundTasksPage : ComponentBase, IDisposable
{
    [Inject] private AuthenticationStateProvider _AuthenticationState { get; set; }
    private List<BackgroundTask> tasks = new();
    private List<BackgroundTask> filteredTasks = new();
    private List<BackgroundTask> paginatedTasks = new();
    private bool isLoading = true;
    private string userId;
    private BackgroundTaskStatus? statusFilter = null;
    private int rowsPerPage = 10;
    private int page = 0;
    private MudTable<BackgroundTask> table;
    private Timer refreshTimer;

    protected override async Task OnInitializedAsync()
    {
        isLoading = true;

        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        userId = authState.User.FindFirst(ClaimTypes.Email)?.Value;

        await RefreshTasks();
        
        // Настраиваем таймер для периодического обновления задач
        refreshTimer = new Timer(async _ =>
        {
            await RefreshTasks();
            await InvokeAsync(StateHasChanged);
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        
        // Подписываемся на события изменения статуса задач
        BackgroundTaskService.TaskStatusChanged += OnTaskStatusChanged;
        BackgroundTaskService.TaskCompleted += OnTaskCompleted;
    }
    
    private async Task RefreshTasks()
    {
        isLoading = true;
        
        // Получаем задачи текущего пользователя
        tasks = await BackgroundTaskService.GetTasksByUserAsync(userId);
        
        ApplyFilters();
        
        isLoading = false;
    }
    
    private void ApplyFilters()
    {
        // Применяем фильтры
        filteredTasks = tasks;
        
        if (statusFilter.HasValue)
        {
            filteredTasks = filteredTasks
                .Where(t => t.Status == statusFilter.Value)
                .ToList();
        }
        
        // Выполняем пагинацию
        paginatedTasks = filteredTasks
            .Skip(page * rowsPerPage)
            .Take(rowsPerPage)
            .ToList();
    }
    
    private async Task ApplyFilter(BackgroundTaskStatus? status)
    {
        statusFilter = status;
        ApplyFilters();
        StateHasChanged();
    }
    
    private void RowsPerPageChanged(int rows)
    {
        rowsPerPage = rows;
        ApplyFilters();
        StateHasChanged();
    }
    
    private async Task CancelTask(BackgroundTask task)
    {
        // Запрашиваем подтверждение
        var parameters = new DialogParameters
        {
            { "ContentText", $"Вы уверены, что хотите отменить задачу '{task.Name}'?" },
            { "ButtonText", "Да, отменить" },
            { "Color", Color.Error }
        };
        
        var dialog = DialogService.Show<ConfirmDialog>("Подтверждение отмены", parameters);
        var result = await dialog.Result;
        
        if (!result.Canceled)
        {
            try
            {
                var success = await BackgroundTaskService.CancelTaskAsync(task.Id);
                if (success)
                {
                    Snackbar.Add("Задача успешно отменена", Severity.Success);
                    await RefreshTasks();
                }
                else
                {
                    Snackbar.Add("Не удалось отменить задачу", Severity.Error);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Ошибка при отмене задачи: {ex.Message}", Severity.Error);
            }
        }
    }
    
    private async Task ShowTaskDetails(BackgroundTask task)
    {
        var parameters = new DialogParameters
        {
            { "Task", task }
        };
        
        var options = new DialogOptions
        {
            CloseButton = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        };
        
        var dialog = DialogService.Show<TaskDetailsDialog>("Подробности задачи", parameters, options);
        await dialog.Result;
    }
    
    private void OnTaskStatusChanged(object sender, BackgroundTask task)
    {
        // Обновляем задачу в списке, если она там есть
        var existingTask = tasks.FirstOrDefault(t => t.Id == task.Id);
        if (existingTask != null)
        {
            // Обновляем существующую задачу
            var index = tasks.IndexOf(existingTask);
            tasks[index] = task;
            
            // Применяем фильтры и обновляем UI
            ApplyFilters();
            InvokeAsync(StateHasChanged);
        }
    }
    
    private void OnTaskCompleted(object sender, BackgroundTask task)
    {
        // То же самое, что и при изменении статуса
        OnTaskStatusChanged(sender, task);
    }
    
    // Вспомогательные методы для отображения
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
    
    public void Dispose()
    {
        // Отписываемся от событий и освобождаем ресурсы
        BackgroundTaskService.TaskStatusChanged -= OnTaskStatusChanged;
        BackgroundTaskService.TaskCompleted -= OnTaskCompleted;
        refreshTimer?.Dispose();
    }
}