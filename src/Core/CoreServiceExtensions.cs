using Microsoft.Extensions.DependencyInjection;

namespace Core;

public static class CoreServiceExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        // Добавляем MediatR (вся бизнес-логика и обработчики команд)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CoreServiceExtensions).Assembly));
        return services;
    }
}