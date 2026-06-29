using AITradingSystem.Data;
using AITradingSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Semantic Kernel
var kernelBuilder = Kernel.CreateBuilder();
// Add your AI services here (OpenAI, Azure OpenAI, Google Gemini, etc.)
// For now, we'll create a basic kernel
builder.Services.AddSingleton(kernelBuilder.Build());

builder.Services.AddSingleton<SimulationLogService>();
builder.Services.AddHttpClient<DnseService>();
builder.Services.AddScoped<TradingCopilotService>();
builder.Services.AddScoped<ReflectionService>();
builder.Services.AddScoped<DataCleanupService>();
builder.Services.AddScoped<AiPlanGenerationService>();
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
