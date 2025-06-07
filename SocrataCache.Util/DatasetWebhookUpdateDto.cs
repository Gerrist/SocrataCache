namespace SocrataCache;

public class DatasetWebhookUpdateDto
{
    public string DatasetId { get; set; } = null!;
    public string ResourceId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
}