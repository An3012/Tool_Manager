using AITradingSystem.Data;
using AITradingSystem.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<SimulationLogService>();
builder.Services.AddHttpClient<DnseService>();
builder.Services.AddScoped<TradingCopilotService>();
builder.Services.AddScoped<ReflectionService>();
builder.Services.AddScoped<DataCleanupService>();
builder.Services.AddHostedService<TradingSimulationWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Copilot}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
