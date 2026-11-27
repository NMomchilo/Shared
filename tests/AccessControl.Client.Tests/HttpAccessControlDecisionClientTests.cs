using System.Net;
using System.Net.Http.Json;
using AccessControl.Client;
using AccessControl.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

namespace AccessControl.Tests;

public sealed class HttpAccessControlDecisionClientTests
{
    private static readonly Guid Organization = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Role = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task DecideAsync_SendsScopedRequestAndReturnsDecisionAsync()
    {
        DecisionRequest? sent = null;
        bool keyPresent = true;
        string? path = null;
        RecordingHandler handler = new(async (request, cancellationToken) =>
        {
            sent = await request.Content!.ReadFromJsonAsync<DecisionRequest>(cancellationToken);
            keyPresent = request.Headers.Contains("X-Access-Control-Key");
            path = request.RequestUri!.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new DecisionResponse(true, "Permit")) };
        });
        HttpAccessControlDecisionClient client = CreateClient(handler);

        DecisionResponse decision = await client.DecideAsync(
            new DecisionRequest("oid", "GET", "/customers/{id}", "tenant-a", Organization),
            CancellationToken.None);

        Assert.True(decision.Permit);
        Assert.Equal("/base/v1/decisions", path);
        Assert.False(keyPresent);
        Assert.Equal(Organization, sent?.UnitOrganizationId);
        Assert.Equal("tenant-a", sent?.TenantId);
    }

    [Fact]
    public async Task DecideAsync_RejectsFailedAndEmptyResponsesAsync()
    {
        DecisionRequest request = new("oid", "GET", "/customers/{id}", "tenant-a", Organization);
        HttpAccessControlDecisionClient unavailable = CreateClient(new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));
        HttpAccessControlDecisionClient empty = CreateClient(new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
            })));

        await Assert.ThrowsAsync<HttpRequestException>(() => unavailable.DecideAsync(request, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => empty.DecideAsync(request, CancellationToken.None));
    }

    private static HttpAccessControlDecisionClient CreateClient(HttpMessageHandler handler) =>
        new(new ClientFactory(handler), new HttpContextAccessor(), Options.Create(new AccessControlClientOptions
        {
            ServiceUrl = "https://access.example/base/"
        }), NullLogger<HttpAccessControlDecisionClient>.Instance);

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => respond(request, cancellationToken);
    }
}
