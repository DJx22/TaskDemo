using MediatR;

namespace AzureExcelExporter.Application.Features.ProcessFile.Commands;

public record ProcessFileCommand(string FileName, string ContainerName, byte[] Content) : IRequest<Unit>;
