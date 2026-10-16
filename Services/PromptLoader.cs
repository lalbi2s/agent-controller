using System.Text.Json;
using System.Text.RegularExpressions;
using AgentController.Models;

namespace AgentController.Services;

public class PromptLoader
{
    private static readonly Regex NamePattern = new(@"^[a-z0-9-]+\z");
    private static readonly Regex PromptFilePattern = new(@"^[a-z0-9-]+\.md\z");
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _basePath;
    private readonly ILogger<PromptLoader> _logger;

    public PromptLoader(string basePath, ILogger<PromptLoader> logger)
    {
        _basePath = basePath;
        _logger = logger;
    }

    public List<PipelineInfo> GetPipelines()
    {
        if (!Directory.Exists(_basePath))
            return [];

        return Directory.GetDirectories(_basePath)
            .Select(dir => Path.Combine(dir, "pipeline.json"))
            .Where(File.Exists)
            .Select(ReadPipeline)
            .ToList();
    }

    public PipelineInfo GetPipeline(string name)
    {
        var configPath = Path.Combine(PipelineDirectory(name), "pipeline.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException($"Pipeline '{name}' not found");

        return ReadPipeline(configPath);
    }

    public string LoadPrompt(string pipelineName, string promptFile)
    {
        var path = PromptPath(pipelineName, promptFile);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Prompt '{promptFile}' not found");

        return File.ReadAllText(path);
    }

    public void SavePrompt(string pipelineName, string promptFile, string content)
    {
        var path = PromptPath(pipelineName, promptFile);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Prompt '{promptFile}' not found");

        File.WriteAllText(path, content);
        _logger.LogInformation("Saved prompt {File} of pipeline {Pipeline}", promptFile, pipelineName);
    }

    public PipelineInfo CreatePipeline(PipelineInfo pipeline, Dictionary<string, string> prompts)
    {
        var dir = PipelineDirectory(pipeline.Name);
        ValidatePromptFiles(pipeline.Name, prompts);

        if (Directory.Exists(dir))
            throw new InvalidOperationException($"Pipeline '{pipeline.Name}' already exists");

        Directory.CreateDirectory(dir);
        WritePipeline(dir, pipeline, prompts);

        _logger.LogInformation("Created pipeline {Name} with {Count} agents", pipeline.Name, pipeline.Agents.Count);
        return pipeline;
    }

    public PipelineInfo UpdatePipeline(PipelineInfo pipeline, Dictionary<string, string> prompts)
    {
        var dir = PipelineDirectory(pipeline.Name);
        ValidatePromptFiles(pipeline.Name, prompts);

        if (!Directory.Exists(dir))
            throw new FileNotFoundException($"Pipeline '{pipeline.Name}' not found");

        WritePipeline(dir, pipeline, prompts);

        // Prompt files of removed agents are no longer needed
        foreach (var existingFile in Directory.GetFiles(dir, "*.md"))
        {
            if (!prompts.ContainsKey(Path.GetFileName(existingFile)))
                File.Delete(existingFile);
        }

        _logger.LogInformation("Updated pipeline {Name} with {Count} agents", pipeline.Name, pipeline.Agents.Count);
        return pipeline;
    }

    public void DeletePipeline(string name)
    {
        var dir = PipelineDirectory(name);
        if (!Directory.Exists(dir))
            throw new FileNotFoundException($"Pipeline '{name}' not found");

        Directory.Delete(dir, recursive: true);
        _logger.LogInformation("Deleted pipeline {Name}", name);
    }

    // Names come from URLs and request bodies: only allow plain names so they cannot point outside the pipelines folder
    private string PipelineDirectory(string name)
    {
        if (!NamePattern.IsMatch(name))
            throw new ArgumentException($"Invalid pipeline name '{name}'. Use lowercase letters, digits and hyphens.");

        return Path.Combine(_basePath, name);
    }

    private string PromptPath(string pipelineName, string promptFile)
    {
        if (!PromptFilePattern.IsMatch(promptFile))
            throw new ArgumentException($"Invalid prompt file '{promptFile}'. Use lowercase letters, digits and hyphens, ending with .md.");

        return Path.Combine(PipelineDirectory(pipelineName), promptFile);
    }

    private void ValidatePromptFiles(string pipelineName, Dictionary<string, string> prompts)
    {
        foreach (var file in prompts.Keys)
            PromptPath(pipelineName, file);
    }

    private static void WritePipeline(string dir, PipelineInfo pipeline, Dictionary<string, string> prompts)
    {
        File.WriteAllText(Path.Combine(dir, "pipeline.json"), JsonSerializer.Serialize(pipeline, WriteOptions));

        foreach (var (fileName, content) in prompts)
            File.WriteAllText(Path.Combine(dir, fileName), content);
    }

    private static PipelineInfo ReadPipeline(string configPath) =>
        JsonSerializer.Deserialize<PipelineInfo>(File.ReadAllText(configPath))
            ?? throw new InvalidOperationException($"Invalid pipeline file {configPath}");
}
