using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AzureExcelExporter.Domain.Entities;


[Table("ExportHistory")]
public class ExportHistory : BaseEntity
{
    [Key]
    public int ExportId { get; init; }

    [Required]
    [MaxLength(250)]
    public string FileName { get; init; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string BlobUrl { get; init; } = string.Empty;

    [Required]
    public int RecordCount { get; init; }

    [Required]
    public DateTimeOffset ExportedAt { get; init; }

    [Required]
    [MaxLength(50)]
    public string Status { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? ErrorMessage { get; init; }
}

