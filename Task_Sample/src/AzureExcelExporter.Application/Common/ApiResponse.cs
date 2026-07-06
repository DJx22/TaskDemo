namespace AzureExcelExporter.Application.Common;

public class ApiResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public string? BlobUrl { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
