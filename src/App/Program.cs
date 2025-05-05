using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using App.Interfaces;
using App.Services;
using Blazored.LocalStorage;
using Core;
using FluentValidation.AspNetCore;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using MudBlazor.Services;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCore();
builder.Services.AddInfrastructureServices(builder.Configuration);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithProcessId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}") // Логи в консоль
    .WriteTo.File(
        path: "logs/app-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}") // Формат логов
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddMudServices();
builder.Services.AddHttpClient("API", client => 
    {
        client.BaseAddress = new Uri("http://localhost:5056");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler 
    {
        UseCookies = true,
        CookieContainer = new CookieContainer()
    });
builder.Services.AddScoped(sp => 
    new HttpClient 
    { 
        BaseAddress = new Uri("http://localhost:5056"),
        Timeout = TimeSpan.FromMinutes(10)
    });

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddScoped<IDatabaseClientService, DatabaseClientService>();
builder.Services.AddScoped<IDataImportClientService, DataImportClientService>();
builder.Services.AddScoped<IDatabaseClientService, DatabaseClientService>();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<IAuthClientService,AuthClientService>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<IDataExportClientService, DataExportClientService>();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<ErrorHandlingService>();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue;
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies["AppAccessToken"];
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireUserRole", policy => policy.RequireRole("User"));
}); 
builder.Services.AddFluentValidationClientsideAdapters();


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
    
    app.UseAuthentication();
    app.UseAuthorization();

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
