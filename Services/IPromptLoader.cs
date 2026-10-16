using AgentController.Models;

namespace AgentController.Services;

public interface IPromptLoader
{
    List<PipelineInfo> GetPipelines();
    PipelineInfo GetPipeline(string name);
    string LoadPrompt(string pipelineName, string promptFile);
    List<string> GetPromptFiles(string pipelineName);
    void SavePrompt(string pipelineName, string promptFile, string content);
    PipelineInfo CreatePipeline(PipelineInfo pipeline, Dictionary<string, string> prompts);
    PipelineInfo UpdatePipeline(PipelineInfo pipeline, Dictionary<string, string> prompts);
    void DeletePipeline(string name);
}
