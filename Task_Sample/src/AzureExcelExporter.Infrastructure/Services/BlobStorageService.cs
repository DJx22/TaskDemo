using Azure.Storage.Blobs;
using AzureExcelExporter.Application.Interfaces;
using AzureExcelExporter.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzureExcelExporter.Infrastructure.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly AzureStorageOptions _options;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IOptions<AzureStorageOptions> options, ILogger<BlobStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(string fileName, byte[] content, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Uploading file {FileName} to blob storage", fileName);

        var blobServiceClient = new BlobServiceClient(_options.ConnectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(fileName);
        await using var stream = new MemoryStream(content);
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

        var blobUrl = blobClient.Uri.ToString();
        _logger.LogInformation("Blob upload completed for {FileName}", fileName);
        return blobUrl;
    }
}
