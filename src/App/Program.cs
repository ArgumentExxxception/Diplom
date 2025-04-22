using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using App.Interfaces;
using App.Services;
using Blazored.LocalStorage;
using Core;
using FluentValidation.AspNetCore;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
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
builder.Services.AddScoped(sp =>
{
    var handler = new HttpClientHandler
    {
        UseCookies = true
    };
    return new HttpClient(handler)
    {
        BaseAddress = new Uri("https://localhost:7056")
    };
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddScoped<IDatabaseClientService, DatabaseClientService>();
builder.Services.AddScoped<IDataImportClientService, DataImportClientService>();
builder.Services.AddScoped<IDatabaseClientService, DatabaseClientService>();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<IAuthClientService,AuthClientService>();
builder.Services.AddScoped<ErrorHandlingService>();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue;
});
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
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
            OnTokenValidated = async context =>
            {
                var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                var token = context.SecurityToken as JwtSecurityToken;
                if (token != null)
                {
                    var isValid = await authService.ValidateTokenAsync(token.RawData);
                    if (!isValid)
                    {
                        context.Fail("Token is blacklisted");
                    }
                }
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireUserRole", policy => policy.RequireRole("User"));
}); 
builder.Services.AddCascadingAuthenticationState();
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
    app.Use(async (context, next) =>
{
    var accessToken = context.Request.Cookies["access_token"];
    var refreshToken = context.Request.Cookies["refresh_token"];

    // Прокидываем токен, если есть
    if (!string.IsNullOrWhiteSpace(accessToken))
    {
        context.Request.Headers.Authorization = $"Bearer {accessToken}";

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]);

        try
        {
            // Валидируем access_token
            tokenHandler.ValidateToken(accessToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                
                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role,
            }, out _);
        }
        catch (SecurityTokenExpiredException)
        {
            // Истёкший токен — пробуем обновить
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                var authService = context.RequestServices.GetRequiredService<IAuthService>();
                var response = await authService.RefreshTokenAsync(accessToken, refreshToken);

                if (response.Successful)
                {
                    // Обновляем куки
                    context.Response.Cookies.Append("access_token", response.Token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTime.UtcNow.AddMinutes(15)
                    });

                    context.Response.Cookies.Append("refresh_token", response.RefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTime.UtcNow.AddDays(7)
                    });

                    // Заменяем токен для текущего запроса
                    context.Request.Headers.Authorization = $"Bearer {response.Token}";
                }
            }
        }
        catch (Exception ex)
        {
            // Игнорируем другие ошибки валидации, чтобы не падало приложение
            Console.WriteLine($"Token validation error: {ex.Message}");
        }
    }

    await next();
});
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
