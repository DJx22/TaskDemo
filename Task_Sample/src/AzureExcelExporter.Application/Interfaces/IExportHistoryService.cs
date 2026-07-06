using AzureExcelExporter.Domain.Entities;

namespace AzureExcelExporter.Application.Interfaces;

public interface IExportHistoryService
{
    Task<ExportHistory> SaveExportHistoryAsync(ExportHistory exportHistory, CancellationToken cancellationToken);
}
