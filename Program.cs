using AgentController;
using AgentController.Agents;
using AgentController.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 500 * 1024 * 1024;
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 500 * 1024 * 1024;
});

builder.Services.AddControllers();

builder.Services.AddHttpClient<AnthropicClient>();
builder.Services.AddHttpClient<GitHubModelsClient>();

builder.Services.AddSingleton<ILLMClientFactory>(sp =>
{
    var clients = new List<ILLMClient>
    {
        sp.GetRequiredService<AnthropicClient>(),
        sp.GetRequiredService<GitHubModelsClient>()
    };
    var logger = sp.GetRequiredService<ILogger<LLMClientFactory>>();
    return new LLMClientFactory(clients, logger);
});

builder.Services.AddSingleton<IPromptLoader, PromptLoader>();
builder.Services.AddSingleton<IAgentFactory, AgentFactory>();
builder.Services.AddTransient<IOrchestrator, Orchestrator>();
builder.Services.AddSingleton<IBreakpointService, BreakpointService>();
builder.Services.AddSingleton<IFileChunkingService, FileChunkingService>();

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