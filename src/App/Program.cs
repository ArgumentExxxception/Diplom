using MudBlazor.Services;
using BlazorApp1.Interfaces;
using BlazorApp1.Services;
using Core;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

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
