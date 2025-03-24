using MudBlazor.Services;
using App.Interfaces;
using App.Services;
using Core;
using Core.Logging;
using Infrastructure;
using Infrastructure.Logging;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug() // Минимальный уровень логирования — Debug
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information) // Для Microsoft — Information
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning) // Для Microsoft.AspNetCore — Warning
    .Enrich.FromLogContext() // Добавляет контекстные данные
    .Enrich.WithThreadId() // Добавляет идентификатор потока
    .Enrich.WithProcessId() // Добавляет идентификатор процесса
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}") // Логи в консоль
    .WriteTo.File(
        path: "logs/app-.log", // Логи в файл
        rollingInterval: RollingInterval.Day, // Новый файл каждый день
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}") // Формат логов
    .CreateLogger(); // Создает логгер

builder.Host.UseSerilog();
// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5024/") });
builder.Services.AddEntityFrameworkNpgsql().AddDbContext<Context>
(opt =>
{
    opt.UseNpgsql(c => c.MigrationsAssembly("App"));
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DbConnection"));
});
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddControllers();
builder.Services.AddScoped<IDatabaseService, DatabaseService>();
builder.Services.AddScoped<IDatabaseClientService, DatabaseClientService>();
builder.Services.AddScoped<IDataImportClientService, DataImportClientService>();
builder.Services.AddScoped<IFileHandlerService, FileHandlerService>();
builder.Services.AddScoped<IDatabaseClientService, DatabaseClientService>();
builder.Services.AddScoped<IDataImportRepository, DataImportRepository>();
builder.Services.AddScoped(typeof(IAppLogger<>), typeof(AppLogger<>));
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue;
});


var app = builder.Build();

try
{
    Log.Information("Запуск приложения");
    
    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseHttpsRedirection();


    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRazorComponents<App.Components.App>()
        .AddInteractiveServerRenderMode();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Приложение завершилось неожиданно");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
