// using App.Components.Dialogs;
// using Core.Models;
// using Domain.Enums;
// using Microsoft.AspNetCore.Components;
// using MudBlazor;
//
// namespace App.Components.Shared;
//
// public partial class FloatingBackgroundTasks : ComponentBase, IDisposable
// {
//     private List<BackgroundTask> activeTasks = new();
//     private HashSet<Guid> expandedTaskIds = new();
//     private HashSet<Guid> hiddenTaskIds = new();
//     private bool isVisible = false;
//     private Timer refreshTimer;
//
//     protected override void OnInitialized()
//     {
//         // Подписываемся на события изменения статуса задач
//         BackgroundTaskService.TaskStatusChanged += OnTaskStatusChanged;
//         BackgroundTaskService.TaskCompleted += OnTaskCompleted;
//         
//         // Загружаем активные задачи
//         RefreshActiveTasks();
//         
//         // Настраиваем таймер для периодического обновления
//         refreshTimer = new Timer(_ =>
//         {
//             RefreshActiveTasks();
//             InvokeAsync(StateHasChanged);
//         }, null, TimeSpan.Zero, TimeSpan.FromSeconds(3));
//     }
//
//     private void RefreshActiveTasks()
//     {
//         // Получаем все активные задачи и фильтруем скрытые
//         var tasks = BackgroundTaskService.GetActiveTasks()
//             .Where(t => !hiddenTaskIds.Contains(t.Id))
//             .OrderByDescending(t => t.CreatedAt)
//             .ToList();
//             
//         // Если активных задач нет, но компонент видим, и нет недавно выполненных задач - скрываем его
//         if (tasks.Count == 0 && isVisible && !BackgroundTaskService
//                 .GetTasksByUserAsync(null)
//                 .Any(t => t.Status != BackgroundTaskStatus.Running && 
//                           t.Status != BackgroundTaskStatus.Pending && 
//                           !hiddenTaskIds.Contains(t.Id) &&
//                           t.CompletedAt.HasValue && 
//                           (DateTime.UtcNow - t.CompletedAt.Value).TotalMinutes < 1))
//         {
//             isVisible = false;
//         }
//         
//         // Если появились новые задачи - показываем компонент
//         if (tasks.Count > 0 && activeTasks.Count == 0)
//         {
//             isVisible = true;
//         }
//         
//         activeTasks = tasks;
//     }
//
//     private void ToggleTaskExpanded(Guid taskId)
//     {
//         if (expandedTaskIds.Contains(taskId))
//             expandedTaskIds.Remove(taskId);
//         else
//             expandedTaskIds.Add(taskId);
//     }
//
//     private void ToggleVisibility()
//     {
//         isVisible = !isVisible;
//     }
//
//     private void HideTask(Guid taskId)
//     {
//         hiddenTaskIds.Add(taskId);
//         RefreshActiveTasks();
//     }
//
//     private async Task CancelTask(BackgroundTask task)
//     {
//         try
//         {
//             await BackgroundTaskService.CancelTaskAsync(task.Id);
//         }
//         catch
//         {
//             // Игнорируем ошибки при отмене
//         }
//     }
//
//     private void GoToTasksPage()
//     {
//         NavigationManager.NavigateTo("/background-tasks");
//     }
//
//     private async Task ShowTaskDetails(BackgroundTask task)
//     {
//         var parameters = new DialogParameters
//         {
//             { "Task", task }
//         };
//         
//         var options = new DialogOptions
//         {
//             CloseButton = true,
//             MaxWidth = MaxWidth.Medium,
//             FullWidth = true
//         };
//         
//         var dialog = DialogService.Show<TaskDetailsDialog>("Подробности задачи", parameters, options);
//         await dialog.Result;
//     }
//
//     private void OnTaskStatusChanged(object sender, BackgroundTask task)
//     {
//         InvokeAsync(() =>
//         {
//             RefreshActiveTasks();
//             StateHasChanged();
//         });
//     }
//
//     private void OnTaskCompleted(object sender, BackgroundTask task)
//     {
//         InvokeAsync(() =>
//         {
//             // Если задача завершилась - раскрываем её для показа результата
//             expandedTaskIds.Add(task.Id);
//             RefreshActiveTasks();
//             StateHasChanged();
//         });
//     }
//
//     // Вспомогательные методы для отображения
//     private string GetTaskTypeName(BackgroundTaskType type)
//     {
//         return type switch
//         {
//             BackgroundTaskType.Import => "Импорт",
//             BackgroundTaskType.Export => "Экспорт",
//             BackgroundTaskType.DataProcessing => "Обработка данных",
//             BackgroundTaskType.SystemMaintenance => "Обслуживание системы",
//             BackgroundTaskType.Other => "Прочее",
//             _ => type.ToString()
//         };
//     }
//     
//     private string GetTaskStatusName(BackgroundTaskStatus status)
//     {
//         return status switch
//         {
//             BackgroundTaskStatus.Pending => "В ожидании",
//             BackgroundTaskStatus.Running => "Выполняется",
//             BackgroundTaskStatus.Completed => "Завершено",
//             BackgroundTaskStatus.Failed => "Ошибка",
//             BackgroundTaskStatus.Cancelled => "Отменено",
//             _ => status.ToString()
//         };
//     }
//     
//     private Color GetTaskColor(BackgroundTask task)
//     {
//         return task.Status switch
//         {
//             BackgroundTaskStatus.Pending => Color.Info,
//             BackgroundTaskStatus.Running => Color.Primary,
//             BackgroundTaskStatus.Completed => Color.Success,
//             BackgroundTaskStatus.Failed => Color.Error,
//             BackgroundTaskStatus.Cancelled => Color.Warning,
//             _ => Color.Default
//         };
//     }
//     
//     private string GetTaskIcon(BackgroundTask task)
//     {
//         string baseIcon = task.TaskType switch
//         {
//             BackgroundTaskType.Import => Icons.Material.Filled.CloudUpload,
//             BackgroundTaskType.Export => Icons.Material.Filled.CloudDownload,
//             BackgroundTaskType.DataProcessing => Icons.Material.Filled.Storage,
//             BackgroundTaskType.SystemMaintenance => Icons.Material.Filled.Settings,
//             _ => Icons.Material.Filled.Task
//         };
//         
//         return baseIcon;
//     }
//
//     public void Dispose()
//     {
//         // Отписываемся от событий и освобождаем ресурсы
//         BackgroundTaskService.TaskStatusChanged -= OnTaskStatusChanged;
//         BackgroundTaskService.TaskCompleted -= OnTaskCompleted;
//         refreshTimer?.Dispose();
//     }
// }