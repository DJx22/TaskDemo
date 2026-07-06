using System.Reflection;
using AzureExcelExporter.Application.Interfaces;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace AzureExcelExporter.Infrastructure.Services;

public class ExcelService : IExcelService
{
    private readonly ILogger<ExcelService> _logger;

    public ExcelService(ILogger<ExcelService> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> GenerateExcelAsync<T>(IEnumerable<T> data, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating Excel workbook with {Count} rows", data.Count());

        await Task.CompletedTask;

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Customers");
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var headerRow = 1;
        var dataRow = 2;

        for (var index = 0; index < properties.Length; index++)
        {
            worksheet.Cell(headerRow, index + 1).Value = properties[index].Name;
        }

        foreach (var item in data)
        {
            for (var index = 0; index < properties.Length; index++)
            {
                var value = properties[index].GetValue(item);
                worksheet.Cell(dataRow, index + 1).Value = value?.ToString();
            }
            dataRow++;
        }

        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        _logger.LogInformation("Excel workbook generated successfully");
        return stream.ToArray();
    }
}
