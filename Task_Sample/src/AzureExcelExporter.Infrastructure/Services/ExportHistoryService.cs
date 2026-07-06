using AzureExcelExporter.Application.Interfaces;
using AzureExcelExporter.Domain.Entities;
using AzureExcelExporter.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace AzureExcelExporter.Infrastructure.Services;

public class ExportHistoryService : IExportHistoryService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ExportHistoryService> _logger;

    public ExportHistoryService(AppDbContext dbContext, ILogger<ExportHistoryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ExportHistory> SaveExportHistoryAsync(ExportHistory exportHistory, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Persisting export history for {FileName}", exportHistory.FileName);
        _dbContext.ExportHistories.Add(exportHistory);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Export history saved successfully");
        return exportHistory;
    }
}
