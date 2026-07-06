using AzureExcelExporter.Domain.Entities;

namespace AzureExcelExporter.Application.Interfaces;

public interface IDataRepository
{
    Task<IReadOnlyList<Customer>> GetCustomersForExportAsync(CancellationToken cancellationToken);
}
