using Core;
using Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Services;

public class BackgroundTaskCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory; // Используем IServiceScopeFactory вместо IBackgroundTaskService
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _taskMaxAge = TimeSpan.FromDays(1);

    public BackgroundTaskCleanupService(
        IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Создаем область видимости для доступа к сервисам
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    // Получаем BackgroundTaskService из области видимости
                    var backgroundTaskService = scope.ServiceProvider.GetRequiredService<IBackgroundTaskService>();
                    
                    // Вызываем метод очистки
                    backgroundTaskService.CleanupOldTasks(_taskMaxAge);
                }
            }
            catch (Exception ex)
            {
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }
}