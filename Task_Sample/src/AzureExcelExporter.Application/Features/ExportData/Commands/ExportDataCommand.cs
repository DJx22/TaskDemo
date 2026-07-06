using MediatR;

using AzureExcelExporter.Application.Common;

namespace AzureExcelExporter.Application.Features.ExportData.Commands;

public record ExportDataCommand(string? Source) : IRequest<ApiResponse>;
