using AzureExcelExporter.Application.Common;
using AzureExcelExporter.Application.Features.ExportData.Commands;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace AzureExcelExporter.Functions.Functions;

public class ExportDataFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<ExportDataFunction> _logger;

    public ExportDataFunction(IMediator mediator, ILogger<ExportDataFunction> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }


    [Function("ExportData")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Received export request");

        try
        {
            var command = new ExportDataCommand(null);

            var response = await _mediator.Send(command);

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(response);

            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export request failed");

            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteAsJsonAsync(new ApiResponse
            {
                Success = false,
                Message = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });

            return response;
        }
    }

}
