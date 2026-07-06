using System;
using System.Threading;
using System.Threading.Tasks;
using AzureExcelExporter.Application.Features.ProcessFile.Commands;
using AzureExcelExporter.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AzureExcelExporter.Application.Features.ProcessFile.Handlers;

public class ProcessFileCommandHandler : IRequestHandler<ProcessFileCommand, Unit>
{
    private readonly IFileProcessorService _fileProcessorService;
    private readonly ILogger<ProcessFileCommandHandler> _logger;

    public ProcessFileCommandHandler(IFileProcessorService fileProcessorService, ILogger<ProcessFileCommandHandler> logger)
    {
        _fileProcessorService = fileProcessorService;
        _logger = logger;
    }

    public async Task<Unit> Handle(ProcessFileCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing blob {FileName} from container {Container}", request.FileName, request.ContainerName);

        try
        {
            await _fileProcessorService.ProcessAsync(request.ContainerName, request.FileName, request.Content, cancellationToken);
            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file {FileName}", request.FileName);
            throw;
        }
    }
}
