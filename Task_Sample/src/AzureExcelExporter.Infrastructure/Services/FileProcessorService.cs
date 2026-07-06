using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using AzureExcelExporter.Application.Interfaces;
using AzureExcelExporter.Domain.Entities;
using AzureExcelExporter.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzureExcelExporter.Infrastructure.Services;

public class FileProcessorService : IFileProcessorService
{
    private readonly AzureStorageOptions _options;
    private readonly ILogger<FileProcessorService> _logger;
    private readonly IExportHistoryService _exportHistoryService;

    public FileProcessorService(IOptions<AzureStorageOptions> options, ILogger<FileProcessorService> logger, IExportHistoryService exportHistoryService)
    {
        _options = options.Value;
        _logger = logger;
        _exportHistoryService = exportHistoryService;
    }

    public async Task ProcessAsync(string containerName, string fileName, byte[] content, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting processing for {FileName} in {Container}", fileName, containerName);

        var processedContainerName = string.IsNullOrWhiteSpace(_options.ContainerName)
            ? containerName + "-processed"
            : _options.ContainerName + "-processed";

        var blobServiceClient = new BlobServiceClient(_options.ConnectionString);

        // Priority: process Excel files produced by export task
        if (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
        {
            // Requires ClosedXML (add package ClosedXML)
            try
            {
                using var inMs = new MemoryStream(content);
                using var workbook = new ClosedXML.Excel.XLWorkbook(inMs);

                foreach (var ws in workbook.Worksheets)
                {
                    var range = ws.RangeUsed();
                    if (range == null)
                        continue;

                    foreach (var cell in range.Cells())
                    {
                        if (cell.IsEmpty() || cell.Value.IsBlank || string.IsNullOrWhiteSpace(cell.GetString()))
                        {
                            cell.Value = "N/A";
                        }
                        else if (cell.DataType == ClosedXML.Excel.XLDataType.Text)
                        {
                            var s = cell.GetString();
                            cell.Value = s.Trim();
                        }
                    }
                }

                var processedContainer = blobServiceClient.GetBlobContainerClient(processedContainerName);
                await processedContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                var destBlob = processedContainer.GetBlobClient(fileName);
                await using var outMs = new MemoryStream();
                workbook.SaveAs(outMs);
                outMs.Position = 0;
                await destBlob.UploadAsync(outMs, overwrite: true, cancellationToken);

                _logger.LogInformation("Uploaded processed Excel to {Container}/{File}", processedContainerName, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing Excel file {File}", fileName);
                throw;
            }
        }
        else if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            var text = System.Text.Encoding.UTF8.GetString(content);
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                _logger.LogWarning("CSV file {FileName} is empty", fileName);
            }
            var processedLines = new List<string>();
            foreach (var line in lines)
            {
                var cols = line.Split(',').Select(c => string.IsNullOrWhiteSpace(c) ? "N/A" : c.Trim());
                processedLines.Add(string.Join(',', cols));
            }
            var processedText = string.Join('\n', processedLines);
            var processedBytes = System.Text.Encoding.UTF8.GetBytes(processedText);

            var processedContainer = blobServiceClient.GetBlobContainerClient(processedContainerName);
            await processedContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var destBlob = processedContainer.GetBlobClient(fileName);
            await using var ms = new MemoryStream(processedBytes);
            await destBlob.UploadAsync(ms, overwrite: true, cancellationToken);

            _logger.LogInformation("Uploaded processed CSV to {Container}/{File}", processedContainerName, fileName);
        }
        else
        {
            // For non-csv files, just move/copy the blob to processed container
            var processedContainer = blobServiceClient.GetBlobContainerClient(processedContainerName);
            await processedContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var destBlob = processedContainer.GetBlobClient(fileName);
            await using var ms = new MemoryStream(content);
            await destBlob.UploadAsync(ms, overwrite: true, cancellationToken);

            _logger.LogInformation("Copied file to processed container {Container}/{File}", processedContainerName, fileName);
        }

        // Delete original blob from source container
        try
        {
            var sourceContainer = blobServiceClient.GetBlobContainerClient(containerName);
            var sourceBlob = sourceContainer.GetBlobClient(fileName);
            await sourceBlob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            _logger.LogInformation("Deleted source blob {Container}/{File}", containerName, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete source blob {File}", fileName);
        }

        // Persist history using existing ExportHistory entity
        var history = new ExportHistory
        {
            FileName = fileName,
            BlobUrl = $"{processedContainerName}/{fileName}",
            RecordCount = 0,
            ExportedAt = DateTimeOffset.UtcNow,
            Status = "Processed",
            ErrorMessage = null
        };

        await _exportHistoryService.SaveExportHistoryAsync(history, cancellationToken);

        _logger.LogInformation("Processing complete for {FileName}", fileName);
    }
}
