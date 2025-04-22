using Core;
using Core.Logging;
using Core.ServiceInterfaces;
using Domain.RepoInterfaces;
using Infrastructure.Logging;
using Infrastructure.Mappers;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services,IConfiguration configuration)
    {
        services.AddEntityFrameworkNpgsql().AddDbContext<Context>(opt =>
        {
            opt.UseNpgsql(c => c.MigrationsAssembly("Infrastructure"));
            opt.UseNpgsql(configuration.GetConnectionString("DbConnection"));
        });
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IBackgroundTaskService, BackgroundTaskService>();
        services.AddHostedService<BackgroundTaskCleanupService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        services.AddAutoMapper(typeof(MappingProfile).Assembly);
        services.AddScoped(typeof(IAppLogger<>), typeof(AppLogger<>));
        services.AddScoped<IDatabaseService, DatabaseService>();
        
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBackgroundTaskRepository, BackgroundTaskRepository>();
        services.AddScoped<IDataImportRepository, DataImportRepository>();

        services.AddScoped<IFileHandlerService, FileHandlerService>();

        services.AddScoped<IXmlImportService, XmlImportService>();
        services.AddScoped<ICsvImportService, CsvImportService>();

        return services;
    }
}