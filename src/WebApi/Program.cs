using Core;
using Core.Handlers;
using Core.Logging;
using FluentValidation.AspNetCore;
using Infrastructure;
using Infrastructure.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10L * 1024L * 1024L * 1024L;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
    options.BufferBodyLengthLimit = long.MaxValue;
});
builder.WebHost.ConfigureKestrel(options =>
{
    // Увеличиваем максимальный размер запроса до 10 ГБ
    options.Limits.MaxRequestBodySize = 10L * 1024L * 1024L * 1024L; // 10GB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
    options.BufferBodyLengthLimit = long.MaxValue;
});

builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(ImportDataCommandHandler).Assembly));
builder.Services.AddCore();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
        policy.WithOrigins("http://localhost:5024")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});
builder.Services.AddScoped(typeof(IAppLogger<>), typeof(AppLogger<>));
builder.Services.AddFluentValidationAutoValidation();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
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

        // Ключевая настройка для получения токена из cookie вместо заголовка
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token) && context.Request.Cookies.ContainsKey("AppAccessToken"))
                {
                    context.Token = context.Request.Cookies["AppAccessToken"];
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                {
                    context.Response.Headers.Add("Token-Expired", "true");
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
var app = builder.Build();

app.UseExceptionHandling();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}
app.UseHttpsRedirection();

app.UseRouting();
app.UseCors("AllowBlazor");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();