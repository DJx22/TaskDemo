using AzureExcelExporter.Application.Features.ProcessBlob.Commands;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureExcelExporter.Functions.Functions;

public class BlobTriggerFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<BlobTriggerFunction> _logger;

    public BlobTriggerFunction(IMediator mediator, ILogger<BlobTriggerFunction> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [Function("BlobTrigger")]
    public async Task Run(
        [BlobTrigger("%BlobTrigger:SourceContainer%/{name}", Connection = "AzureStorage:ConnectionString")] Stream blobStream,
        string name,
        CancellationToken cancellationToken)
    {
        if (blobStream is null)
        {
            _logger.LogWarning("Received null blob stream for {BlobName}", name);
            return;
        }

        _logger.LogInformation("Blob trigger activated for {BlobName}", name);

        try
        {
            var response = await _mediator.Send(new ProcessBlobCommand(name, blobStream), cancellationToken);
            _logger.LogInformation("Blob processing completed for {BlobName}. {Message}", name, response.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blob processing failed for {BlobName}", name);
            throw;
        }
    }
}
