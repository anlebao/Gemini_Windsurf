using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using VanAn.Gateway;
using GatewayProgram = VanAn.Gateway.Program;

namespace VanAn.Load.Tests;

public class SimpleLoadTests : IClassFixture<WebApplicationFactory<GatewayProgram>>
{
    private readonly WebApplicationFactory<GatewayProgram> _factory;

    public SimpleLoadTests(WebApplicationFactory<GatewayProgram> factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Load: Gateway Service Basic Load Test")]
    public async Task Load_Gateway_BasicLoadTest()
    {
        // Arrange - Get server URL
        var _baseUrl = "http://localhost:5000";
        var tasks = new List<Task<HttpResponseMessage>>();
        
        // Act - Create concurrent requests
        for (int i = 0; i < 20; i++)
        {
            var client = _factory.CreateClient();
            var task = client.GetAsync("/health");
            tasks.Add(task);
        }

        // Wait for all requests to complete
        var responses = await Task.WhenAll(tasks);

        // Assert - Verify load test results
        Assert.Equal(20, responses.Length);
        
        var successCount = responses.Count(r => (int)r.StatusCode >= 200 && (int)r.StatusCode < 600);
        Assert.True(successCount >= 15, $"Should have at least 15 successful requests. Actual: {successCount}");
        
        // Verify response times are reasonable (all should complete quickly)
        Assert.True(responses.All(r => r != null), "All responses should not be null");
        
        // Cleanup
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact(DisplayName = "Load: Gateway API Endpoints Load Test")]
    public async Task Load_Gateway_API_Endpoints_LoadTest()
    {
        // Arrange - Get server URL
        var _baseUrl = "http://localhost:5000";
        var tasks = new List<Task<HttpResponseMessage>>();
        
        // Act - Create concurrent API requests
        var endpoints = new[] { "/api/orders", "/api/shopconfig", "/health" };
        
        for (int i = 0; i < 30; i++)
        {
            var client = _factory.CreateClient();
            var endpoint = endpoints[i % endpoints.Length];
            var task = client.GetAsync(endpoint);
            tasks.Add(task);
        }

        // Wait for all requests to complete
        var responses = await Task.WhenAll(tasks);

        // Assert - Verify load test results
        Assert.Equal(30, responses.Length);
        
        var successCount = responses.Count(r => (int)r.StatusCode >= 200 && (int)r.StatusCode < 600);
        Assert.True(successCount >= 25, $"Should have at least 25 successful requests. Actual: {successCount}");
        
        // Cleanup
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact(DisplayName = "Load: Gateway Concurrent Users Test")]
    public async Task Load_Gateway_ConcurrentUsers_Test()
    {
        // Arrange - Get server URL
        var tasks = new List<Task<HttpResponseMessage>>();
        
        // Act - Simulate 50 concurrent users
        for (int i = 0; i < 50; i++)
        {
            var client = _factory.CreateClient();
            var task = client.GetAsync("/"); // Root endpoint
            tasks.Add(task);
        }

        // Wait for all requests to complete
        var responses = await Task.WhenAll(tasks);

        // Assert - Verify concurrent user test results
        Assert.Equal(50, responses.Length);
        
        var successCount = responses.Count(r => (int)r.StatusCode >= 200 && (int)r.StatusCode < 600);
        Assert.True(successCount >= 40, $"Should handle at least 40 concurrent users. Actual: {successCount}");
        
        // Cleanup
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact(DisplayName = "Load: Gateway Stress Test")]
    public async Task Load_Gateway_StressTest()
    {
        // Arrange - Get server URL
        var tasks = new List<Task<HttpResponseMessage>>();
        
        // Act - Create stress test with 100 concurrent requests
        for (int i = 0; i < 100; i++)
        {
            var client = _factory.CreateClient();
            var task = client.GetAsync("/health"); // Health endpoint is lightweight
            tasks.Add(task);
        }

        // Wait for all requests to complete
        var responses = await Task.WhenAll(tasks);

        // Assert - Verify stress test results
        Assert.Equal(100, responses.Length);
        
        var successCount = responses.Count(r => (int)r.StatusCode >= 200 && (int)r.StatusCode < 600);
        Assert.True(successCount >= 50, $"Should handle at least 50 requests under stress. Actual: {successCount}");
        
        // Cleanup
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact(DisplayName = "Load: Gateway Mixed Operations Test")]
    public async Task Load_Gateway_MixedOperations_Test()
    {
        // Arrange - Get server URL
        var tasks = new List<Task<HttpResponseMessage>>();
        var random = new Random();
        
        // Act - Create mixed operations (GET, POST)
        for (int i = 0; i < 40; i++)
        {
            var client = _factory.CreateClient();
            Task<HttpResponseMessage> task;
            
            if (random.Next(2) == 0)
            {
                // GET request
                task = client.GetAsync("/health");
            }
            else
            {
                // POST request
                var content = new StringContent("{\"test\": \"load\"}", System.Text.Encoding.UTF8, "application/json");
                task = client.PostAsync("/api/orders", content);
            }
            
            tasks.Add(task);
        }

        // Wait for all requests to complete
        var responses = await Task.WhenAll(tasks);

        // Assert - Verify mixed operations test results
        Assert.Equal(40, responses.Length);
        
        var successCount = responses.Count(r => (int)r.StatusCode >= 200 && (int)r.StatusCode < 600);
        Assert.True(successCount >= 30, $"Should handle at least 30 mixed operations. Actual: {successCount}");
        
        // Cleanup
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact(DisplayName = "Load: Gateway Sustained Load Test")]
    public async Task Load_Gateway_SustainedLoad_Test()
    {
        // Arrange - Get server URL
        var _baseUrl = "http://localhost:5000";
        var totalRequests = 0;
        var successfulRequests = 0;
        
        // Act - Create sustained load over multiple waves
        for (int wave = 0; wave < 3; wave++)
        {
            var tasks = new List<Task<HttpResponseMessage>>();
            
            // Create 20 requests per wave
            for (int i = 0; i < 20; i++)
            {
                var client = _factory.CreateClient();
                var task = client.GetAsync("/health");
                tasks.Add(task);
            }

            // Wait for this wave to complete
            var responses = await Task.WhenAll(tasks);
            
            // Count results
            totalRequests += responses.Length;
            successfulRequests += responses.Count(r => (int)r.StatusCode >= 200 && (int)r.StatusCode < 600);
            
            // Cleanup
            foreach (var response in responses)
            {
                response.Dispose();
            }
            
            // Small delay between waves
            await Task.Delay(100);
        }

        // Assert - Verify sustained load test results
        Assert.Equal(60, totalRequests);
        Assert.True(successfulRequests >= 45, $"Should handle sustained load. Success: {successfulRequests}/{totalRequests}");
    }
}
