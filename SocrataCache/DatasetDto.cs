namespace SocrataCache;

public class DatasetDto
{
    public string DatasetId { get; set; } = null!;
    public string ResourceId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime ReferenceDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Type { get; set; } = null!;
}