using Xunit;
using Xunit.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.CoreHub.Infrastructure;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.KhachLink;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// HTTP Integration Test Base - standalone implementation
/// Provides HTTP client functionality with ITestOutputHelper for debugging
/// Manages its own DbContext cleanup
/// </summary>
public abstract class HttpIntegrationTestBase : IDisposable
{
    protected readonly HttpClient _client;
    protected readonly ITestOutputHelper _output;
    protected readonly WebApplicationFactory<Program> _factory;
    protected readonly VanAnDbContext _dbContext;

    protected HttpIntegrationTestBase(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _output = output;
        _factory = factory;
        _client = _factory.CreateClient();
        
        // Setup DbContext for testing
        var scope = _factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
    }

    /// <summary>
    /// Helper method để POST và parse JSON response
    /// </summary>
    protected async Task<T?> PostAndParseAsync<T>(string endpoint, object request)
    {
        _output.WriteLine($"POST {endpoint}");
        _output.WriteLine($"Request: {JsonSerializer.Serialize(request)}");
        
        var response = await _client.PostAsJsonAsync(endpoint, request);
        
        _output.WriteLine($"Response Status: {response.StatusCode}");
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Error Response: {errorContent}");
            return default(T);
        }

        var result = await response.Content.ReadFromJsonAsync<T>();
        _output.WriteLine($"Response: {JsonSerializer.Serialize(result)}");
        
        return result;
    }

    /// <summary>
    /// Helper method để GET và parse JSON response
    /// </summary>
    protected async Task<T?> GetAndParseAsync<T>(string endpoint)
    {
        _output.WriteLine($"GET {endpoint}");
        
        var response = await _client.GetAsync(endpoint);
        
        _output.WriteLine($"Response Status: {response.StatusCode}");
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Error Response: {errorContent}");
            return default(T);
        }

        var result = await response.Content.ReadFromJsonAsync<T>();
        _output.WriteLine($"Response: {JsonSerializer.Serialize(result)}");
        
        return result;
    }

    /// <summary>
    /// Helper method để PUT và parse JSON response
    /// </summary>
    protected async Task<T?> PutAndParseAsync<T>(string endpoint, object request)
    {
        _output.WriteLine($"PUT {endpoint}");
        _output.WriteLine($"Request: {JsonSerializer.Serialize(request)}");
        
        var response = await _client.PutAsJsonAsync(endpoint, request);
        
        _output.WriteLine($"Response Status: {response.StatusCode}");
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Error Response: {errorContent}");
            return default(T);
        }

        var result = await response.Content.ReadFromJsonAsync<T>();
        _output.WriteLine($"Response: {JsonSerializer.Serialize(result)}");
        
        return result;
    }

    /// <summary>
    /// Helper method để DELETE
    /// </summary>
    protected async Task<bool> DeleteAsync(string endpoint)
    {
        _output.WriteLine($"DELETE {endpoint}");
        
        var response = await _client.DeleteAsync(endpoint);
        
        _output.WriteLine($"Response Status: {response.StatusCode}");
        
        return response.IsSuccessStatusCode;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _dbContext?.Dispose();
    }
}
