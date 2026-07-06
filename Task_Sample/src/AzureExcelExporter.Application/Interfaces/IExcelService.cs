namespace AzureExcelExporter.Application.Interfaces;

public interface IExcelService
{
    Task<byte[]> GenerateExcelAsync<T>(IEnumerable<T> data, CancellationToken cancellationToken);
}
