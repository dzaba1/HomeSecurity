using System.Net;

namespace Dzaba.HomeSecurity.Authorization.OrgApi.Tests;

/// <summary>Answers every request with one canned response and records what it was asked.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode statusCode;
    private readonly string? body;

    public StubHttpMessageHandler(HttpStatusCode statusCode, string? body = null)
    {
        this.statusCode = statusCode;
        this.body = body;
    }

    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        var response = new HttpResponseMessage(statusCode);
        if (body is not null)
        {
            response.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        }

        return Task.FromResult(response);
    }
}
