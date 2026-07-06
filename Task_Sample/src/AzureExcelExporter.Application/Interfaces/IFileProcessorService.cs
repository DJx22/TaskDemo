namespace AzureExcelExporter.Application.Interfaces;

public interface IFileProcessorService
{
    Task ProcessAsync(string containerName, string fileName, byte[] content, CancellationToken cancellationToken);
}
