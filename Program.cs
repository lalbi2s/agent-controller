using AgentController;
using AgentController.Agents;
using AgentController.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HostFiltering;

var builder = WebApplication.CreateBuilder(args);

// Local use only: no authentication, so the server is not reachable from the network
builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(5000));
builder.Services.Configure<HostFilteringOptions>(options => options.AllowedHosts = ["localhost"]);

builder.Services.AddControllers();
builder.Services.AddDataProtection().SetApplicationName("AgentController");

builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp => new ApiKeyStore(
    Path.Combine(builder.Environment.ContentRootPath, "data", "api-keys.json"),
    sp.GetRequiredService<IDataProtectionProvider>()));
builder.Services.AddSingleton<ApiKeyResolver>();
builder.Services.AddSingleton<ILLMClient, AnthropicClient>();
builder.Services.AddSingleton<ILLMClient, OpenAIClient>();
builder.Services.AddSingleton<LLMClientFactory>();

builder.Services.AddSingleton(sp => new PromptLoader(
    Path.Combine(builder.Environment.ContentRootPath, "pipelines"),
    sp.GetRequiredService<ILogger<PromptLoader>>()));
builder.Services.AddSingleton<AgentFactory>();
builder.Services.AddTransient<Orchestrator>();
builder.Services.AddSingleton<BreakpointService>();
builder.Services.AddSingleton<FileChunkingService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();