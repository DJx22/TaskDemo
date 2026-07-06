using AzureExcelExporter.Application.Common;
using AzureExcelExporter.Application.Common.Exceptions;
using AzureExcelExporter.Application.Features.ExportData.Commands;
using AzureExcelExporter.Application.Interfaces;
using AzureExcelExporter.Domain.Constants;
using AzureExcelExporter.Domain.Entities;
using AzureExcelExporter.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;


namespace AzureExcelExporter.Application.Features.ExportData.Handlers;

public class ExportDataCommandHandler : IRequestHandler<ExportDataCommand, ApiResponse>
{
    private readonly IDataRepository _dataRepository;
    private readonly IExcelService _excelService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IExportHistoryService _exportHistoryService;
    private readonly ILogger<ExportDataCommandHandler> _logger;

    public ExportDataCommandHandler(
        IDataRepository dataRepository,
        IExcelService excelService,
        IBlobStorageService blobStorageService,
        IExportHistoryService exportHistoryService,
        ILogger<ExportDataCommandHandler> logger)
    {
        _dataRepository = dataRepository;
        _excelService = excelService;
        _blobStorageService = blobStorageService;
        _exportHistoryService = exportHistoryService;
        _logger = logger;
    }

    public async Task<ApiResponse> Handle(ExportDataCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting export for source {Source}", request.Source ?? "default");

        try
        {
            var customers = await _dataRepository.GetCustomersForExportAsync(cancellationToken);
            if (customers is null || customers.Count == 0)
            {
                throw new BusinessException("No customers found for export.");
            }

            var fileName = $"CustomerData_{DateTimeOffset.UtcNow:yyyy-MM-dd}{ExportConstants.ExcelFileExtension}";
            var excelContent = await _excelService.GenerateExcelAsync(customers, cancellationToken);
            var blobUrl = await _blobStorageService.UploadFileAsync(fileName, excelContent, cancellationToken);

            var exportHistory = new ExportHistory
            {
                FileName = fileName,
                BlobUrl = blobUrl,
                RecordCount = customers.Count,
                ExportedAt = DateTimeOffset.UtcNow,
                Status = ExportStatus.Completed.ToString(),
                ErrorMessage = null
            };

            await _exportHistoryService.SaveExportHistoryAsync(exportHistory, cancellationToken);

            _logger.LogInformation("Export completed successfully for {RecordCount} records", customers.Count);
            return new ApiResponse
            {
                Success = true,
                Message = "Export completed successfully.",
                FileName = fileName,
                BlobUrl = blobUrl,
                Timestamp = DateTimeOffset.UtcNow
            };
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error while exporting data");
            throw;
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "Business error while exporting data");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while exporting data");
            throw new BusinessException("An unexpected error occurred while exporting data.");
        }
    }
}
