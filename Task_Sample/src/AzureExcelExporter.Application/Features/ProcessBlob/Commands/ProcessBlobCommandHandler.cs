using AzureExcelExporter.Application.Common;
using AzureExcelExporter.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AzureExcelExporter.Application.Features.ProcessBlob.Commands;

public class ProcessBlobCommandHandler : IRequestHandler<ProcessBlobCommand, ApiResponse>
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<ProcessBlobCommandHandler> _logger;

    public ProcessBlobCommandHandler(IBlobStorageService blobStorageService, ILogger<ProcessBlobCommandHandler> logger)
    {
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    public async Task<ApiResponse> Handle(ProcessBlobCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting blob processing for {BlobName}", request.BlobName);

        try
        {
            await _blobStorageService.ProcessUploadedBlobAsync(request.BlobName, request.Content, cancellationToken);

            return new ApiResponse
            {
                Success = true,
                Message = $"Blob {request.BlobName} processed successfully.",
                Timestamp = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blob processing failed for {BlobName}", request.BlobName);
            throw;
        }
    }
}
