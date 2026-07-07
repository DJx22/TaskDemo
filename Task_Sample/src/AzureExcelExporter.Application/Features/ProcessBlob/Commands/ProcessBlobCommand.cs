using AzureExcelExporter.Application.Common;
using MediatR;

namespace AzureExcelExporter.Application.Features.ProcessBlob.Commands;

public record ProcessBlobCommand(string BlobName, Stream Content) : IRequest<ApiResponse>;
