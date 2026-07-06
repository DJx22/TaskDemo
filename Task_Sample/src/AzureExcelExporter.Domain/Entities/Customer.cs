namespace AzureExcelExporter.Domain.Entities;

public class Customer : BaseEntity
{
    public int CustomerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}
