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
    }

    public SocrataCacheConfig GetConfig() => _config ?? throw new NullReferenceException("No config defined");

    public string GetBaseUri() => _config?.BaseUrl ?? throw new NullReferenceException("No BaseUrl configured.");

    public SocrataCacheResource[] GetResources() => _config?.Resources?.ToArray() ?? throw new NullReferenceException("No resources configured.");
    
    public int GetRetentionSize() => _config?.RetentionSize ?? throw new NullReferenceException("No RetentionSize configured.");
    
    public int GetRetentionDays() => _config?.RetentionDays ?? throw new NullReferenceException("No RetentionDays configured.");
    
}

public class SocrataCacheConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public List<SocrataCacheResource> Resources { get; set; } = [];
    public int RetentionSize { get; set; } = 50;
    public int RetentionDays { get; set; } = 14;
}