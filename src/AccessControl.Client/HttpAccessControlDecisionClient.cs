using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using AccessControl.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AccessControl.Client;

public sealed class HttpAccessControlDecisionClient(IHttpClientFactory clientFactory, IHttpContextAccessor contextAccessor,
    IOptions<AccessControlClientOptions> options,
    ILogger<HttpAccessControlDecisionClient> logger) : IAccessControlDecisionClient
{
    public async Task<DecisionResponse> DecideAsync(DecisionRequest request, CancellationToken cancellationToken)
    {
        using HttpClient client = clientFactory.CreateClient(nameof(HttpAccessControlDecisionClient));
        using HttpRequestMessage message = new(HttpMethod.Post, new Uri(new Uri(options.Value.ServiceUrl), "v1/decisions"))
        {
            Content = JsonContent.Create(request)
        };
        string? authorization = contextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (AuthenticationHeaderValue.TryParse(authorization, out AuthenticationHeaderValue? bearer) &&
            string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(bearer.Parameter))
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer.Parameter);
        Stopwatch timer = Stopwatch.StartNew();
        logger.LogDebug("Sending decision request for {Method} {Route}", request.HttpMethod, request.RouteTemplate);
        using HttpResponseMessage response = await client.SendAsync(message, cancellationToken);
        logger.LogDebug("Decision service responded with {StatusCode} in {ElapsedMs} ms for {Method} {Route}",
            (int)response.StatusCode, timer.ElapsedMilliseconds, request.HttpMethod, request.RouteTemplate);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Decision service HTTP failure {StatusCode} for {Method} {Route}",
                (int)response.StatusCode, request.HttpMethod, request.RouteTemplate);
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DecisionResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Access Control returned an empty decision.");
    }
}
