using System;
using System.IO;
using System.Threading.Tasks;
using AzureExcelExporter.Application.Features.ProcessFile.Commands;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureExcelExporter.Functions.Functions;

public class ProcessBlobFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<ProcessBlobFunction> _logger;

    public ProcessBlobFunction(IMediator mediator, ILogger<ProcessBlobFunction> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [Function("ProcessBlob")]
    public async Task Run([BlobTrigger("exports/{name}", Connection = "AzureWebJobsStorage")] Stream blobStream, string name)
    {
        _logger.LogInformation("Blob trigger fired for exports/{Name}", name);
        try
        {
            await using var ms = new MemoryStream();
            await blobStream.CopyToAsync(ms);
            var content = ms.ToArray();

            var command = new ProcessFileCommand(name, "exports", content);
            await _mediator.Send(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in blob trigger for {Name}", name);
            throw;
        }
    }
}
