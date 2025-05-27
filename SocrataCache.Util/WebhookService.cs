using System.Text;
using System.Text.Json;

namespace SocrataCache.Util;

public class WebhookService
{
    private readonly HttpClient _httpClient;
    private readonly string? _webhookUrl;

    public WebhookService(string? webhookUrl)
    {
        _webhookUrl = webhookUrl;
        _httpClient = new HttpClient();
    }

    public async Task SendWebhookNotification(DatasetWebhookUpdateDto dataset)
    {
        if (string.IsNullOrEmpty(_webhookUrl))
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(dataset);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_webhookUrl, content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - we don't want webhook failures to affect the main application flow
            Console.WriteLine($"Failed to send webhook notification: {ex.Message}");
        }
    }
}