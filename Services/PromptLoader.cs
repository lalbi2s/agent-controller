using System.Text.Json;
using AgentController.Models;

namespace AgentController.Services;

public class PromptLoader
{
    private readonly string _basePath;
    private readonly ILogger<PromptLoader> _logger;

    public PromptLoader(ILogger<PromptLoader> logger)
    {
        _logger = logger;
        _basePath = Path.Combine(AppContext.BaseDirectory, "pipelines");

        if (!Directory.Exists(_basePath))
            _basePath = Path.Combine(Directory.GetCurrentDirectory(), "pipelines");
    }

    public List<PipelineInfo> GetPipelines()
    {
        var pipelines = new List<PipelineInfo>();
        if (!Directory.Exists(_basePath))
            return pipelines;

        foreach (var dir in Directory.GetDirectories(_basePath))
        {
            var configPath = Path.Combine(dir, "pipeline.json");
            if (!File.Exists(configPath)) continue;

            var json = File.ReadAllText(configPath);
            var pipeline = JsonSerializer.Deserialize<PipelineInfo>(json);
            if (pipeline != null)
                pipelines.Add(pipeline);
        }

        return pipelines;
    }

    public PipelineInfo GetPipeline(string name)
    {
        var configPath = Path.Combine(_basePath, name, "pipeline.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException($"Pipeline '{name}' not found at {configPath}");

        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<PipelineInfo>(json)
            ?? throw new InvalidOperationException($"Failed to parse pipeline config: {name}");
    }

    public string LoadPrompt(string pipelineName, string promptFile)
    {
        var path = Path.Combine(_basePath, pipelineName, promptFile);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Prompt file not found: {path}");

        _logger.LogDebug("Loading prompt from {Path}", path);
        return File.ReadAllText(path);
    }

    public void SavePrompt(string pipelineName, string promptFile, string content)
    {
        var path = Path.Combine(_basePath, pipelineName, promptFile);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Prompt file not found: {path}");

        _logger.LogInformation("Saving prompt to {Path}", path);
        File.WriteAllText(path, content);
    }

    public PipelineInfo CreatePipeline(PipelineInfo pipeline, Dictionary<string, string> prompts)
    {
        var dir = Path.Combine(_basePath, pipeline.Name);
        if (Directory.Exists(dir))
            throw new InvalidOperationException($"Pipeline '{pipeline.Name}' already exists");

        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(pipeline, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(Path.Combine(dir, "pipeline.json"), json);

        foreach (var (fileName, content) in prompts)
            File.WriteAllText(Path.Combine(dir, fileName), content);

        _logger.LogInformation("Created pipeline '{Name}' with {Count} agents", pipeline.Name, pipeline.Agents.Count);
        return pipeline;
    }

    public PipelineInfo UpdatePipeline(PipelineInfo pipeline, Dictionary<string, string> prompts)
    {
        var dir = Path.Combine(_basePath, pipeline.Name);
        if (!Directory.Exists(dir))
            throw new FileNotFoundException($"Pipeline '{pipeline.Name}' not found");

        // Overwrite pipeline.json with new structure
        var json = JsonSerializer.Serialize(pipeline, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(Path.Combine(dir, "pipeline.json"), json);

        // Write/update all agent prompt files
        var newFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (fileName, content) in prompts)
        {
            File.WriteAllText(Path.Combine(dir, fileName), content);
            newFileNames.Add(fileName);
        }

        // Delete old .md files that no longer belong to any agent
        foreach (var existingFile in Directory.GetFiles(dir, "*.md"))
        {
            var fn = Path.GetFileName(existingFile);
            if (!newFileNames.Contains(fn))
            {
                File.Delete(existingFile);
                _logger.LogInformation("Removed obsolete prompt file: {File}", fn);
            }
        }

        _logger.LogInformation("Updated pipeline '{Name}' → {Count} agents", pipeline.Name, pipeline.Agents.Count);
        return pipeline;
    }

    public void DeletePipeline(string name)
    {
        var dir = Path.Combine(_basePath, name);
        if (!Directory.Exists(dir))
            throw new FileNotFoundException($"Pipeline '{name}' not found");

        Directory.Delete(dir, recursive: true);
        _logger.LogInformation("Deleted pipeline '{Name}'", name);
    }
}
