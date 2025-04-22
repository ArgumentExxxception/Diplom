using Core;
using Core.Handlers;
using Core.Logging;
using FluentValidation.AspNetCore;
using Infrastructure;
using Infrastructure.Logging;
using WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(ImportDataCommandHandler).Assembly));
builder.Services.AddCore();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy.WithOrigins("http://localhost:5024")
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddScoped(typeof(IAppLogger<>), typeof(AppLogger<>));
builder.Services.AddFluentValidationAutoValidation();
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
app.UseCors("AllowBlazorClient");
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var accessToken = context.Request.Cookies["access_token"];
    if (!string.IsNullOrWhiteSpace(accessToken))
    {
        context.Request.Headers.Authorization = $"Bearer {accessToken}";
    }
    await next();
});
app.UseAuthorization();
app.MapControllers();

app.Run();