using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .SetBasePath(@"C:\vibecoding\gemini_windsurf\2_Gateway")
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

Console.WriteLine("=== All ReverseProxy keys ===");
foreach (var kv in config.AsEnumerable())
{
    if (kv.Key.StartsWith("ReverseProxy", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"  {kv.Key} = {kv.Value ?? "<null>"}");
    }
}

Console.WriteLine();
Console.WriteLine("=== ReverseProxy:Routes children ===");
var routesSection = config.GetSection("ReverseProxy:Routes");
foreach (var child in routesSection.GetChildren())
{
    Console.WriteLine($"  Route: {child.Key} -> ClusterId = {child.GetSection("ClusterId").Value}");
}

Console.WriteLine();
Console.WriteLine("=== ReverseProxy:Clusters children ===");
var clustersSection = config.GetSection("ReverseProxy:Clusters");
foreach (var child in clustersSection.GetChildren())
{
    var addr = child.GetSection("Destinations:destination1:Address").Value;
    Console.WriteLine($"  Cluster: {child.Key} -> Address = {addr}");
}
