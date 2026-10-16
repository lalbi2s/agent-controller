using System.Net;
using System.Text.Json;
using AgentController.Controllers;
using AgentController.Services;
using AgentController.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentController.Tests;

public class ApiKeyTests : IDisposable
{
    private const string Key = "sk-test-SECRET-KEY-1234";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "agent-controller-tests", Guid.NewGuid().ToString("N"));
    private readonly ApiKeyStore _store;

    public ApiKeyTests()
    {
        _store = new ApiKeyStore(FilePath, new EphemeralDataProtectionProvider());
    }

    private string FilePath => Path.Combine(_folder, "api-keys.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private ApiKeyResolver CreateResolver(Dictionary<string, string?>? settings = null) =>
        new(_store, new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build());

    private KeysController CreateController(params ILLMClient[] clients) =>
        new(_store, CreateResolver(), new LLMClientFactory(clients, NullLogger<LLMClientFactory>.Instance));

    private static string ToJson(IActionResult result) =>
        JsonSerializer.Serialize(((ObjectResult)result).Value);

    [Fact]
    public void The_key_file_does_not_contain_the_key_in_clear_text()
    {
        _store.Set("anthropic", Key);

        var content = File.ReadAllText(FilePath);

        Assert.DoesNotContain(Key, content);
        Assert.DoesNotContain("SECRET", content);
        Assert.Equal(Key, _store.Get("anthropic"));
    }

    [Fact]
    public void A_saved_key_can_be_read_again_by_a_new_store_with_the_same_protection()
    {
        var provider = new EphemeralDataProtectionProvider();
        new ApiKeyStore(FilePath, provider).Set("openai", Key);

        Assert.Equal(Key, new ApiKeyStore(FilePath, provider).Get("openai"));
    }

    [Fact]
    public void A_key_that_cannot_be_decrypted_is_treated_as_missing()
    {
        new ApiKeyStore(FilePath, new EphemeralDataProtectionProvider()).Set("openai", Key);

        var otherMachine = new ApiKeyStore(FilePath, new EphemeralDataProtectionProvider());

        Assert.Null(otherMachine.Get("openai"));
    }

    [Fact]
    public void Deleting_a_key_removes_it_from_the_file()
    {
        _store.Set("anthropic", Key);
        _store.Set("openai", "other-key-0000");

        _store.Delete("anthropic");

        Assert.Null(_store.Get("anthropic"));
        Assert.Equal("other-key-0000", _store.Get("openai"));
        Assert.DoesNotContain("anthropic", File.ReadAllText(FilePath));
    }

    [Fact]
    public void The_key_saved_in_the_interface_wins_over_the_configuration()
    {
        var resolver = CreateResolver(new() { ["Anthropic:ApiKey"] = "from-config-0000" });
        Assert.Equal(("from-config-0000", "config"), resolver.GetKeyAndSource("anthropic"));

        _store.Set("anthropic", Key);

        Assert.Equal((Key, "ui"), resolver.GetKeyAndSource("anthropic"));
    }

    [Fact]
    public void Without_any_key_the_provider_is_not_configured()
    {
        Assert.Equal((null, "none"), CreateResolver().GetKeyAndSource("openai"));
    }

    [Fact]
    public void The_status_never_contains_the_key_only_its_last_4_characters()
    {
        var controller = CreateController();
        controller.Save("anthropic", new SaveKeyRequest { Key = Key });

        var json = ToJson(controller.GetAll());

        Assert.DoesNotContain(Key, json);
        Assert.DoesNotContain("SECRET", json);
        Assert.Contains("\"Last4\":\"1234\"", json);
        Assert.Contains("\"Source\":\"ui\"", json);
    }

    [Fact]
    public void A_short_key_does_not_reveal_any_character()
    {
        var controller = CreateController();
        controller.Save("anthropic", new SaveKeyRequest { Key = "abc123" });

        Assert.Contains("\"Last4\":\"\"", ToJson(controller.GetAll()));
    }

    [Fact]
    public void Deleting_through_the_api_makes_the_provider_not_configured()
    {
        var controller = CreateController();
        controller.Save("anthropic", new SaveKeyRequest { Key = Key });

        Assert.IsType<NoContentResult>(controller.Delete("anthropic"));

        var json = ToJson(controller.GetAll());
        Assert.Contains("\"Configured\":false", json);
        Assert.DoesNotContain("1234", json);
    }

    [Fact]
    public void Unknown_providers_and_empty_keys_are_refused()
    {
        var controller = CreateController();

        Assert.IsType<NotFoundObjectResult>(controller.Save("evil", new SaveKeyRequest { Key = Key }));
        Assert.IsType<BadRequestObjectResult>(controller.Save("anthropic", new SaveKeyRequest { Key = "   " }));
        Assert.IsType<BadRequestObjectResult>(controller.Save("anthropic", new SaveKeyRequest { Key = new string('x', 501) }));
    }

    [Fact]
    public async Task A_connection_test_with_a_rejected_key_gives_a_fixed_message()
    {
        // Even if the exception text contained the key, the answer must not
        var client = new FakeLLMClient("anthropic")
        {
            TestConnectionError = new HttpRequestException($"Bad key {Key}", null, HttpStatusCode.Unauthorized)
        };

        var json = ToJson(await CreateController(client).Test("anthropic", CancellationToken.None));

        Assert.Contains("Key rejected by provider", json);
        Assert.DoesNotContain(Key, json);
    }

    [Fact]
    public async Task A_connection_test_reports_success_unreachable_and_missing_key()
    {
        var working = new FakeLLMClient("anthropic");
        Assert.Contains("\"Ok\":true", ToJson(await CreateController(working).Test("anthropic", CancellationToken.None)));

        var unreachable = new FakeLLMClient("anthropic") { TestConnectionError = new HttpRequestException("dns failure") };
        Assert.Contains("Provider unreachable", ToJson(await CreateController(unreachable).Test("anthropic", CancellationToken.None)));

        var noKey = new FakeLLMClient("anthropic", isAvailable: false);
        Assert.Contains("No key configured", ToJson(await CreateController(noKey).Test("anthropic", CancellationToken.None)));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Provider_errors_never_contain_the_key(HttpStatusCode status)
    {
        var body = "{\"error\":{\"message\":\"Invalid key " + Key + " for this model\"}}";
        _store.Set("anthropic", Key);
        var client = new AnthropicClient(new FakeHttpClientFactory(status, body), CreateResolver(), new ConfigurationBuilder().Build());

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.CompleteAsync("system", "hello", ""));

        Assert.DoesNotContain(Key, ex.Message);
        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task A_bad_request_error_keeps_the_provider_message_for_debugging()
    {
        _store.Set("openai", Key);
        var body = """{"error":{"message":"The model gpt-x does not exist"}}""";
        var client = new OpenAIClient(new FakeHttpClientFactory(HttpStatusCode.NotFound, body), CreateResolver(), new ConfigurationBuilder().Build());

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.CompleteAsync("system", "hello", "gpt-x"));

        Assert.Contains("The model gpt-x does not exist", ex.Message);
    }

    [Fact]
    public async Task A_cut_off_response_is_reported()
    {
        _store.Set("anthropic", Key);
        var body = """{"stop_reason":"max_tokens","content":[{"type":"text","text":"{\"half\":"}]}""";
        var client = new AnthropicClient(new FakeHttpClientFactory(HttpStatusCode.OK, body), CreateResolver(), new ConfigurationBuilder().Build());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.CompleteAsync("system", "hello", ""));

        Assert.Contains("cut off", ex.Message);
    }
}
