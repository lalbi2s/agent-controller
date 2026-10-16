using System.Net;
using System.Text;

namespace AgentController.Tests.Support;

public class FakeHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;

    public FakeHttpHandler(HttpStatusCode status, string body)
    {
        _status = status;
        _body = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") });
}

public class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly FakeHttpHandler _handler;

    public FakeHttpClientFactory(HttpStatusCode status, string body) => _handler = new FakeHttpHandler(status, body);

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}
