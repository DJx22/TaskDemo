namespace AzureExcelExporter.Application.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadFileAsync(string fileName, byte[] content, CancellationToken cancellationToken);
    Task<string> UploadFileAsync(string fileName, Stream content, string containerName, CancellationToken cancellationToken);
    Task ProcessUploadedBlobAsync(string sourceBlobName, Stream content, CancellationToken cancellationToken);
    string BuildProcessedBlobName(string originalFileName);
}
