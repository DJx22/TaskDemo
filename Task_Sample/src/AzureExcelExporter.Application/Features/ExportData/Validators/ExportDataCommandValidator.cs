using FluentValidation;
using AzureExcelExporter.Application.Features.ExportData.Commands;

namespace AzureExcelExporter.Application.Features.ExportData.Validators;

public class ExportDataCommandValidator : AbstractValidator<ExportDataCommand>
{
    public ExportDataCommandValidator()
    {
        RuleFor(x => x.Source)
            .MaximumLength(100)
            .WithMessage("Source must be 100 characters or less.");
    }
}
