using AgentController.Models;
using AgentController.Tests.Support;

namespace AgentController.Tests;

public class PromptLoaderTests : IDisposable
{
    private readonly TestWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void A_created_pipeline_can_be_read_back()
    {
        _workspace.CreatePipeline("json", 2, ("extractor", "generate"), ("reviewer", "review"));

        var pipeline = _workspace.PromptLoader.GetPipeline("test");

        Assert.Equal(["extractor", "reviewer"], pipeline.Agents.Select(a => a.Role));
        Assert.Equal("review", pipeline.Agents[1].Type);
        Assert.Equal("prompt of extractor", _workspace.PromptLoader.LoadPrompt("test", "extractor.md"));
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("UPPER")]
    [InlineData("")]
    [InlineData("name\n")]
    public void Pipeline_names_that_could_leave_the_pipelines_folder_are_refused(string name)
    {
        Assert.Throws<ArgumentException>(() => _workspace.PromptLoader.GetPipeline(name));
        Assert.Throws<ArgumentException>(() => _workspace.PromptLoader.DeletePipeline(name));
    }

    [Theory]
    [InlineData("../pipeline.json")]
    [InlineData("..%2fpipeline.json")]
    [InlineData("sub/file.md")]
    [InlineData("file.txt")]
    public void Prompt_files_that_could_leave_the_pipeline_folder_are_refused(string file)
    {
        _workspace.CreatePipeline("text", 2, ("a", "generate"));

        Assert.Throws<ArgumentException>(() => _workspace.PromptLoader.LoadPrompt("test", file));
        Assert.Throws<ArgumentException>(() => _workspace.PromptLoader.SavePrompt("test", file, "x"));
    }

    [Fact]
    public void A_pipeline_with_an_invalid_prompt_file_is_not_created()
    {
        var pipeline = new PipelineInfo { Name = "bad" };

        Assert.Throws<ArgumentException>(() =>
            _workspace.PromptLoader.CreatePipeline(pipeline, new Dictionary<string, string> { ["../x.md"] = "x" }));
        Assert.Throws<FileNotFoundException>(() => _workspace.PromptLoader.GetPipeline("bad"));
    }

    [Fact]
    public void Creating_the_same_pipeline_twice_is_refused()
    {
        _workspace.CreatePipeline("text", 2, ("a", "generate"));

        Assert.Throws<InvalidOperationException>(() => _workspace.CreatePipeline("text", 2, ("a", "generate")));
    }

    [Fact]
    public void Updating_a_pipeline_removes_the_prompts_of_deleted_agents()
    {
        var pipeline = _workspace.CreatePipeline("text", 2, ("a", "generate"), ("b", "generate"));
        pipeline.Agents.RemoveAt(1);

        _workspace.PromptLoader.UpdatePipeline(pipeline, new Dictionary<string, string> { ["a.md"] = "new" });

        Assert.Equal("new", _workspace.PromptLoader.LoadPrompt("test", "a.md"));
        Assert.Throws<FileNotFoundException>(() => _workspace.PromptLoader.LoadPrompt("test", "b.md"));
    }

    [Fact]
    public void A_deleted_pipeline_is_gone()
    {
        _workspace.CreatePipeline("text", 2, ("a", "generate"));

        _workspace.PromptLoader.DeletePipeline("test");

        Assert.Empty(_workspace.PromptLoader.GetPipelines());
    }
}
