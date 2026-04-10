using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Services;

namespace VanAn.Shared.Tests.Services;

public class LocalizationServiceTests : IDisposable
{
    private readonly ILogger<LocalizationService> _logger;
    private readonly IMemoryCache _cache;
    private readonly LocalizationService _service;
    private readonly string _testResourcesPath;
    private readonly string _enJsonPath;
    private readonly string _viJsonPath;
    private static readonly object _lockObject = new object();

    public LocalizationServiceTests()
    {
        _logger = new TestLogger<LocalizationService>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        
        // Create unique test directory for each test instance
        _testResourcesPath = Path.Combine(Path.GetTempPath(), $"VanAnTestResources_{Guid.NewGuid():N}");
        var localizationPath = Path.Combine(_testResourcesPath, "1_Shared", "Localization");
        
        lock (_lockObject)
        {
            // Create test directory and files
            Directory.CreateDirectory(localizationPath);
            
            // Create unique file names with correct pattern
            _enJsonPath = Path.Combine(localizationPath, "Resources.en.json");
            _viJsonPath = Path.Combine(localizationPath, "Resources.vi.json");
            
            // Create nested JSON test file
            var nestedJson = @"{
                ""common"": {
                    ""buttons"": {
                        ""save"": ""Save"",
                        ""cancel"": ""Cancel"",
                        ""submit"": ""Submit""
                    },
                    ""messages"": {
                        ""success"": ""Operation completed successfully"",
                        ""error"": ""An error occurred""
                    }
                },
                ""product"": {
                    ""name"": ""Product Name"",
                    ""price"": ""Price"",
                    ""description"": ""Description""
                },
                ""simple_key"": ""Simple Value""
            }";
            
            File.WriteAllText(_enJsonPath, nestedJson);
            
            // Create Vietnamese version
            var viJson = @"{
                ""common"": {
                    ""buttons"": {
                        ""save"": ""Lưu"",
                        ""cancel"": ""Hủy"",
                        ""submit"": ""Gửi""
                    },
                    ""messages"": {
                        ""success"": ""Thao tác thành công"",
                        ""error"": ""Đã xảy ra lỗi""
                    }
                },
                ""product"": {
                    ""name"": ""Tên sản phẩm"",
                    ""price"": ""Giá"",
                    ""description"": ""Mô tả""
                },
                ""simple_key"": ""Giá trị đơn giản""
            }";
            
            File.WriteAllText(_viJsonPath, viJson);
            
            // Create service with custom path by temporarily changing current directory
            var originalDir = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(_testResourcesPath);
                _service = new LocalizationService(_logger, _cache);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }
    }

    [Fact]
    public async Task GetAllStringsAsync_ShouldFlattenNestedJson()
    {
        // Act
        var result = await _service.GetAllStringsAsync("en");
        
        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count > 0);
        
        // Check flattened keys exist
        Assert.True(result.ContainsKey("common.buttons.save"));
        Assert.True(result.ContainsKey("common.buttons.cancel"));
        Assert.True(result.ContainsKey("common.buttons.submit"));
        Assert.True(result.ContainsKey("common.messages.success"));
        Assert.True(result.ContainsKey("common.messages.error"));
        Assert.True(result.ContainsKey("product.name"));
        Assert.True(result.ContainsKey("product.price"));
        Assert.True(result.ContainsKey("product.description"));
        Assert.True(result.ContainsKey("simple_key"));
        
        // Check values
        Assert.Equal("Save", result["common.buttons.save"]);
        Assert.Equal("Cancel", result["common.buttons.cancel"]);
        Assert.Equal("Submit", result["common.buttons.submit"]);
        Assert.Equal("Operation completed successfully", result["common.messages.success"]);
        Assert.Equal("An error occurred", result["common.messages.error"]);
        Assert.Equal("Product Name", result["product.name"]);
        Assert.Equal("Price", result["product.price"]);
        Assert.Equal("Description", result["product.description"]);
        Assert.Equal("Simple Value", result["simple_key"]);
    }

    [Fact]
    public async Task GetAllStringsAsync_ShouldWorkForVietnamese()
    {
        // Act
        var result = await _service.GetAllStringsAsync("vi");
        
        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("common.buttons.save"));
        Assert.Equal("Lưu", result["common.buttons.save"]);
        Assert.Equal("Tên sản phẩm", result["product.name"]);
    }

    [Fact]
    public async Task GetAllStringsAsync_ShouldCacheResults()
    {
        // Act
        var result1 = await _service.GetAllStringsAsync("en");
        var result2 = await _service.GetAllStringsAsync("en");
        
        // Assert - Should be equivalent due to caching (not necessarily same reference)
        Assert.Equal(result1.Count, result2.Count);
        foreach (var kvp in result1)
        {
            Assert.True(result2.ContainsKey(kvp.Key));
            Assert.Equal(result2[kvp.Key], kvp.Value);
        }
    }

    [Fact]
    public async Task GetStringAsync_ShouldReturnCorrectValue()
    {
        // Act
        var result = await _service.GetStringAsync("common.buttons.save", "en");
        
        // Assert
        Assert.Equal("Save", result);
    }

    [Fact]
    public async Task GetStringAsync_ShouldFallbackToDefaultCulture()
    {
        // Set culture to Vietnamese first
        await _service.SetCultureAsync("vi");
        
        // Act - Request English key
        var result = await _service.GetStringAsync("common.buttons.save", "en");
        
        // Assert
        Assert.Equal("Save", result);
    }

    public void Dispose()
    {
        lock (_lockObject)
        {
            // Cleanup specific test files and directories
            try
            {
                if (File.Exists(_enJsonPath))
                    File.Delete(_enJsonPath);
                if (File.Exists(_viJsonPath))
                    File.Delete(_viJsonPath);
                if (Directory.Exists(_testResourcesPath))
                    Directory.Delete(_testResourcesPath, true);
            }
            catch
            {
                // Ignore cleanup errors for tests
            }
            
            _cache?.Dispose();
        }
    }
}

// Simple test logger implementation
public class TestLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null!;
    
    public bool IsEnabled(LogLevel logLevel) => true;
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Do nothing for tests
    }
}
