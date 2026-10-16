using AgentController.Controllers;
using AgentController.Tests.Support;
using Microsoft.AspNetCore.Mvc;

namespace AgentController.Tests;

public class PipelinesControllerTests : IDisposable
{
    private readonly TestWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    private static CreatePipelineRequest Request(string name, params (string Name, string Type)[] agents) => new()
    {
        Name = name,
        Agents = agents.Select(a => new CreateAgentRequest { Name = a.Name, Type = a.Type, Prompt = "p" }).ToList()
    };

    [Fact]
    public void A_valid_pipeline_is_created()
    {
        var controller = new PipelinesController(_workspace.PromptLoader);

        var result = controller.Create(Request("my-pipeline", ("Extractor", "generate"), ("Checker", "review")));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("review", _workspace.PromptLoader.GetPipeline("my-pipeline").Agents[1].Type);
    }

    [Fact]
    public void An_unknown_agent_type_is_a_bad_request()
    {
        var controller = new PipelinesController(_workspace.PromptLoader);

        var result = controller.Create(Request("my-pipeline", ("Extractor", "enrich")));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Two_agents_with_the_same_name_are_a_bad_request()
    {
        var controller = new PipelinesController(_workspace.PromptLoader);

        var result = controller.Create(Request("my-pipeline", ("Same", "generate"), ("same", "generate")));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void A_pipeline_name_with_a_path_is_a_bad_request()
    {
        var controller = new PipelinesController(_workspace.PromptLoader);

        Assert.IsType<BadRequestObjectResult>(controller.Create(Request("../escape", ("A", "generate"))));
        Assert.IsType<BadRequestObjectResult>(controller.Get("../escape"));
        Assert.IsType<BadRequestObjectResult>(controller.Delete("../.."));
    }

    [Fact]
    public void An_unknown_pipeline_is_not_found()
    {
        var controller = new PipelinesController(_workspace.PromptLoader);

        Assert.IsType<NotFoundObjectResult>(controller.Get("missing"));
    }
}
