using AgentController;
using AgentController.Agents;
using AgentController.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddHttpClient();
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