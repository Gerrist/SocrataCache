using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocrataCache.Managers.Models;

public enum DatasetStatus
{
    Pending = 0,
    Downloading = 1,
    Downloaded = 2,
    Obsolete = 3,
    Failed = 4,
    Deleted = 5
}

[Table("Dataset")]
public class DatasetModel
{
    [Required] [Key] [Column("DatasetId")] public string DatasetId { get; set; }

    [Required] [Column("ResourceId")] public string ResourceId { get; set; } = null!;

    [Required] [Column("Status")] public DatasetStatus Status { get; set; }

    [Required] [Column("ReferenceDate")] public DateTime ReferenceDate { get; set; }

    [Required] [Column("CreatedAt")] public DateTime CreatedAt { get; set; }

    [Required] [Column("UpdatedAt")] public DateTime UpdatedAt { get; set; }
}