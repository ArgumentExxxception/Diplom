using AutoMapper;
using Core;
using Core.Models;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class BackgroundTaskService : IBackgroundTaskService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly SemaphoreSlim _semaphore;
        private readonly IMapper _mapper;
        private readonly int _maxConcurrentTasks;
        private readonly Dictionary<Guid, CancellationTokenSource> _cancellationTokens = new();

        public event EventHandler<BackgroundTask> TaskStatusChanged;
        public event EventHandler<BackgroundTask> TaskCompleted;

        public BackgroundTaskService(
            IServiceScopeFactory serviceScopeFactory,
            IMapper mapper,
            int maxConcurrentTasks = 3)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _mapper = mapper;
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
            // Сохраняем файл во временное хранилище
            var tempFilePath = Path.GetTempFileName();
            using (var tempFile = File.Create(tempFilePath))
            {
                await fileStream.CopyToAsync(tempFile);
            }

            // Создаем новую задачу
            var task = new BackgroundTask
            {
                Name = $"Импорт {fileName}",
                Description = $"Импорт файла {fileName} в таблицу {importRequest.TableName}",
                Status = BackgroundTaskStatus.Pending,
                TaskType = BackgroundTaskType.Import,
                UserId = userName,
                TaskData = new Dictionary<string, object>
                {
                    { "FileName", fileName },
                    { "FileSize", fileSize },
                    { "TableName", importRequest.TableName },
                    { "ImportMode", importRequest.ImportMode },
                    { "ContentType", contentType },
                    { "TempFilePath", tempFilePath }
                }
            };

            // Сохраняем задачу в БД
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                await unitOfWork.BackgroundTasks.Add(_mapper.Map<BackgroundTask, BackgroundTaskEntity>(task));
                await unitOfWork.CommitAsync();
            }

            var cts = new CancellationTokenSource();
            _cancellationTokens[task.Id] = cts;

            // Запускаем задачу в фоне
            _ = Task.Run(async () =>
            {
                try
                {
                    using var tempFileStream = File.OpenRead(tempFilePath);
                    await ExecuteImportTaskAsync(task.Id, tempFileStream, fileName, contentType, importRequest, userName, cts.Token);
                }
                catch (Exception ex)
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    await unitOfWork.BackgroundTasks.FailTaskAsync(task.Id, ex.Message);
                    await unitOfWork.CommitAsync();
                }
                finally
                {
                    // Удаляем временный файл после завершения
                    try
                    {
                        if (File.Exists(tempFilePath))
                            File.Delete(tempFilePath);
                    }
                    catch (Exception ex)
                    {
                        // Логирование ошибки удаления можно добавить при необходимости
                    }

                    _cancellationTokens.Remove(task.Id);
                }
            });

            return task;
        }

        private async Task ExecuteImportTaskAsync(
            Guid taskId,
            Stream fileStream,
            string fileName,
            string contentType,
            TableImportRequestModel importRequest,
            string userName,
            CancellationToken cancellationToken)
        {
            try
            {
                await _semaphore.WaitAsync(cancellationToken);

                using var scope = _serviceScopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                
                await unitOfWork.BackgroundTasks.UpdateStatusAsync(taskId, BackgroundTaskStatus.Running);
                await unitOfWork.CommitAsync();
                
                var updatedTask = await GetTaskByIdAsync(taskId);
                OnTaskStatusChanged(updatedTask);
                
                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;
                
                var fileHandlerService = scope.ServiceProvider.GetRequiredService<IFileHandlerService>();
                var result = await fileHandlerService.ImportDataAsync(
                    memoryStream,
                    fileName,
                    contentType,
                    importRequest,
                    cancellationToken);
                
                cancellationToken.ThrowIfCancellationRequested();

                var taskEntity = await unitOfWork.BackgroundTasks.GetByIdAsync(taskId);
                if (taskEntity.CancellationRequested)
                    cancellationToken.ThrowIfCancellationRequested();
                
                if (result.Success)
                {
                    await unitOfWork.BackgroundTasks.CompleteTaskAsync(taskId, result);
                }
                else
                {
                    await unitOfWork.BackgroundTasks.FailTaskAsync(taskId, result.Message);
                }

                await unitOfWork.CommitAsync();
                
                var completedTask = await GetTaskByIdAsync(taskId);
                OnTaskStatusChanged(completedTask);
                OnTaskCompleted(completedTask);
            }
            catch (OperationCanceledException)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                
                await unitOfWork.BackgroundTasks.UpdateStatusAsync(
                    taskId,
                    BackgroundTaskStatus.Cancelled,
                    0,
                    "Задача была отменена пользователем");

                await unitOfWork.CommitAsync();

                var cancelledTask = await GetTaskByIdAsync(taskId);
                OnTaskStatusChanged(cancelledTask);
                OnTaskCompleted(cancelledTask);
            }
            catch (Exception ex)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                await unitOfWork.BackgroundTasks.FailTaskAsync(taskId, ex.Message);
                await unitOfWork.CommitAsync();

                var failedTask = await GetTaskByIdAsync(taskId);
                OnTaskStatusChanged(failedTask);
                OnTaskCompleted(failedTask);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<BackgroundTask>> GetActiveTasksAsync()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var activeTaskEntities = await unitOfWork.BackgroundTasks.GetActiveTasksAsync();
            return activeTaskEntities.Select(e => _mapper.Map<BackgroundTaskEntity, BackgroundTask>(e)).ToList();
        }

        public async Task<BackgroundTask> GetTaskByIdAsync(Guid taskId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var taskEntity = await unitOfWork.BackgroundTasks.GetByIdAsync(taskId);
            return _mapper.Map<BackgroundTaskEntity, BackgroundTask>(taskEntity);
        }
        
        public Task<BackgroundTask> GetTaskById(Guid taskId)
        {
            return GetTaskByIdAsync(taskId);
        }

        public async Task<List<BackgroundTask>> GetTasksByUserAsync(string userId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var taskEntities = await unitOfWork.BackgroundTasks.GetTasksByUserAsync(userId);
            return taskEntities.Select(e => _mapper.Map<BackgroundTaskEntity, BackgroundTask>(e)).ToList();
        }

        public async Task<bool> CancelTaskAsync(Guid taskId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var taskEntity = await unitOfWork.BackgroundTasks.GetByIdAsync(taskId);
            if (taskEntity == null ||
                (taskEntity.Status != BackgroundTaskStatus.Pending &&
                 taskEntity.Status != BackgroundTaskStatus.Running))
                return false;
            
            await unitOfWork.BackgroundTasks.RequestCancellationAsync(taskId);
            await unitOfWork.CommitAsync();

            if (_cancellationTokens.TryGetValue(taskId, out var cts))
            {
                cts.Cancel();
                return true;
            }

            return true;
        }

        public async Task CleanupOldTasksAsync(TimeSpan olderThan)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BackgroundTasks.CleanupOldTasksAsync(olderThan);
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
}
