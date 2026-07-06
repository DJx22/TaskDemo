namespace AzureExcelExporter.Infrastructure.Options;

public class AzureStorageOptions
{
    public const string SectionName = "AzureStorage";
    public string ConnectionString { get; init; } = string.Empty;
    public string ContainerName { get; init; } = string.Empty;
}
