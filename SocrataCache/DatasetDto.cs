namespace SocrataCache;

public class DatasetDto
{
    public string DatasetId { get; set; }
    public string ResourceId { get; set; }
    public string Status { get; set; }
    public DateTime ReferenceDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}