using System.Net;
using AgentController.Agents;
using AgentController.Services;
using AgentController.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentController.Tests;

public class AgentTests
{
    private static Agent CreateAgent(string provider, string model, params ILLMClient[] clients) =>
        new("agent", "role", "system prompt", new LLMClientFactory(clients, NullLogger<LLMClientFactory>.Instance),
            provider, model, NullLogger.Instance);

    [Theory]
    [InlineData("""{"a":1}""", """{"a":1}""")]
    [InlineData("Here you go:\n```json\n{\"a\":1}\n```", """{"a":1}""")]
    [InlineData("[1,2] and some text", "[1,2]")]
    [InlineData("List [Note] then {\"a\":[1]}", """{"a":[1]}""")]
    public void ExtractJson_finds_the_json_value(string text, string expected)
    {
        Assert.Equal(expected, Agent.ExtractJson(text));
    }

    [Theory]
    [InlineData("no json at all")]
    [InlineData("{broken json")]
    [InlineData("body { color: red } is css")]
    public void ExtractJson_returns_null_when_there_is_no_valid_json(string text)
    {
        Assert.Null(Agent.ExtractJson(text));
    }

    [Fact]
    public async Task A_json_agent_repeats_the_request_once_when_the_answer_is_not_json()
    {
        var llm = new FakeLLMClient().Reply("sorry, here is text", """{"ok":true}""");

        var result = await CreateAgent("anthropic", "m1", llm).ExecuteAsync("input", expectJson: true);

        Assert.Equal("""{"ok":true}""", result);
        Assert.Equal(2, llm.Calls.Count);
    }

    [Fact]
    public async Task A_text_agent_returns_the_answer_untouched_with_a_single_request()
    {
        var llm = new FakeLLMClient().Reply("  <style>a { color: red }</style> hello  ");

        var result = await CreateAgent("anthropic", "m1", llm).ExecuteAsync("input", expectJson: false);

        Assert.Equal("<style>a { color: red }</style> hello", result);
        Assert.Single(llm.Calls);
    }

    [Fact]
    public async Task The_configured_model_is_sent_to_the_configured_provider()
    {
        var llm = new FakeLLMClient().Reply("ok");

        await CreateAgent("anthropic", "my-model", llm).ExecuteAsync("input", expectJson: false);

        Assert.Equal("my-model", llm.Calls[0].Model);
        Assert.Equal("system prompt", llm.Calls[0].SystemPrompt);
    }

    [Fact]
    public async Task A_rejected_key_falls_back_to_another_provider_with_its_own_default_model()
    {
        var anthropic = new FakeLLMClient("anthropic").Reply(
            new HttpRequestException("Anthropic returned HTTP 401", null, HttpStatusCode.Unauthorized));
        var openai = new FakeLLMClient("openai").Reply("from openai");

        var result = await CreateAgent("anthropic", "claude-model", anthropic, openai).ExecuteAsync("input", expectJson: false);

        Assert.Equal("from openai", result);
        Assert.Equal("", openai.Calls[0].Model);
    }

    [Fact]
    public async Task A_provider_without_key_is_replaced_and_the_model_is_not_reused()
    {
        var anthropic = new FakeLLMClient("anthropic", isAvailable: false);
        var openai = new FakeLLMClient("openai").Reply("from openai");

        await CreateAgent("anthropic", "claude-model", anthropic, openai).ExecuteAsync("input", expectJson: false);

        Assert.Empty(anthropic.Calls);
        Assert.Equal("", openai.Calls[0].Model);
    }

    [Fact]
    public async Task An_error_that_is_not_about_the_key_is_not_retried_elsewhere()
    {
        var anthropic = new FakeLLMClient("anthropic").Reply(
            new HttpRequestException("Anthropic returned HTTP 500", null, HttpStatusCode.InternalServerError));
        var openai = new FakeLLMClient("openai").Reply("unused");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateAgent("anthropic", "m", anthropic, openai).ExecuteAsync("input", expectJson: false));
        Assert.Empty(openai.Calls);
    }

    [Fact]
    public async Task It_fails_with_a_clear_message_when_no_provider_has_a_key()
    {
        var llm = new FakeLLMClient("anthropic", isAvailable: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateAgent("anthropic", "m", llm).ExecuteAsync("input", expectJson: false));
        Assert.Contains("API key", ex.Message);
    }
}
