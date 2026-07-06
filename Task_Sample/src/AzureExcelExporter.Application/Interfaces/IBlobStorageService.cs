namespace AzureExcelExporter.Application.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadFileAsync(string fileName, byte[] content, CancellationToken cancellationToken);
}
