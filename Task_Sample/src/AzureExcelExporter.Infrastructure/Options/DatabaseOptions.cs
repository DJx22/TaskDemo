namespace AzureExcelExporter.Infrastructure.Options;

public class DatabaseOptions
{
    public const string SectionName = "Database";
    public string ConnectionString { get; init; } = string.Empty;
}
