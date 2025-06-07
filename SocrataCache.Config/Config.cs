using System.Text.Json;
using SocrataCache.Util;

namespace SocrataCache.Config;

public class Config
{
    private readonly string _configFilePath;
    private SocrataCacheConfig? _config;

    public Config(string configFilePath)
    {
        _configFilePath = configFilePath;
        ReadConfig().Wait(); // Ensures config is loaded when the app starts
        
        Console.WriteLine("- Loaded configuration:");
        Console.WriteLine($"- Base url:         {GetBaseUri()}");
        Console.WriteLine($"- Resource count:   {GetResources().Length}");
        Console.WriteLine($"- Retention size:   {GetRetentionSize()} gigabytes");
        Console.WriteLine($"- Retention days:   {GetRetentionDays()} days");
    }

    private async Task ReadConfig()
    {
        if (!File.Exists(_configFilePath))
        {
            Console.WriteLine($"No config file found at {Directory.GetCurrentDirectory()}");
            throw new FileNotFoundException("The config file could not be found.");
        }

        var configFileContents = await File.ReadAllTextAsync(_configFilePath);
        _config = JsonSerializer.Deserialize<SocrataCacheConfig>(configFileContents, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new JsonException("Failed to deserialize config file.");

        // Validate resource IDs are unique
        var resourceIds = _config.Resources.Select(r => r.ResourceId).ToList();
        var duplicateResourceIds = resourceIds.GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateResourceIds.Any())
        {
            throw new InvalidOperationException($"Duplicate resource IDs found: {string.Join(", ", duplicateResourceIds)}");
        }

        // Validate file types
        foreach (var resource in _config.Resources)
        {
            if (!new[] { "csv", "json", "xml" }.Contains(resource.Type.ToLower()))
            {
                throw new InvalidOperationException($"Invalid file type '{resource.Type}' for resource {resource.ResourceId}. Must be one of: csv, json, xml");
            }
        }
    }

    public SocrataCacheConfig GetConfig() => _config ?? throw new NullReferenceException("No config defined");

    public string GetBaseUri() => _config?.BaseUrl ?? throw new NullReferenceException("No BaseUrl configured.");

    public SocrataCacheResource[] GetResources() => _config?.Resources?.ToArray() ?? throw new NullReferenceException("No resources configured.");
    
    public int GetRetentionSize() => _config?.RetentionSize ?? throw new NullReferenceException("No RetentionSize configured.");
    
    public int GetRetentionDays() => _config?.RetentionDays ?? throw new NullReferenceException("No RetentionDays configured.");
    
    public string? GetWebhookUrl() => _config?.WebhookUrl;
    
}

public class SocrataCacheConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public List<SocrataCacheResource> Resources { get; set; } = [];
    public int RetentionSize { get; set; } = 50;
    public int RetentionDays { get; set; } = 14;
    public string? WebhookUrl { get; set; } = null;
}