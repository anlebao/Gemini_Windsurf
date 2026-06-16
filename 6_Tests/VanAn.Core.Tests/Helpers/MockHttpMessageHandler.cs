using System.Net;
using System.Text;
using System.Text.Json;

namespace VanAn.Core.Tests.Helpers;

/// <summary>
/// Stub HttpMessageHandler — routes by URL substring + HttpMethod, first-match.
/// No external packages needed (built on System.Net.Http).
/// Usage:
///   handler.AddResponse("auth/token", HttpMethod.Post, new { access_token = "tok" });
///   handler.AddRawResponse("health", HttpMethod.Get, HttpStatusCode.OK, "{}");
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(string UrlSubstring, HttpMethod Method, Func<HttpResponseMessage> Factory)>
        _routes = new();

    /// <summary>Register a route that returns HTTP 200 with JSON-serialized body.</summary>
    public void AddResponse(string urlSubstring, HttpMethod method, object jsonBody)
    {
        _routes.Add((urlSubstring, method, () =>
        {
            var json    = JsonSerializer.Serialize(jsonBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }));
    }

    /// <summary>Register a route with explicit status code and raw string body.</summary>
    public void AddRawResponse(string urlSubstring, HttpMethod method,
        HttpStatusCode statusCode, string body)
    {
        _routes.Add((urlSubstring, method, () =>
        {
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            return new HttpResponseMessage(statusCode) { Content = content };
        }));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? "";

        foreach (var (sub, method, factory) in _routes)
        {
            if (url.Contains(sub, StringComparison.OrdinalIgnoreCase)
                && request.Method == method)
                return Task.FromResult(factory());
        }

        // No matching route → 404 with diagnostic message
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                $"MockHttpMessageHandler: no route for [{request.Method}] {url}")
        });
    }
}
