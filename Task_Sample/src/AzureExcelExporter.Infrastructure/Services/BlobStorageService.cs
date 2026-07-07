using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AzureExcelExporter.Application.Interfaces;
using ClosedXML.Excel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace AzureExcelExporter.Infrastructure.Services;

public class BlobStorageService : IBlobStorageService
{
    public sealed record ExcelCustomerRow(string Name, string Email, string City);
    public sealed record CleanedCustomerRow(string FirstName, string LastName, string Email, string City);

    private readonly IConfiguration _configuration;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(
        IConfiguration configuration,
        ILogger<BlobStorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(content);
        return await UploadFileAsync(fileName, stream, GetDefaultContainerName(), cancellationToken);
    }

    public async Task<string> UploadFileAsync(
        string fileName,
        Stream content,
        string containerName,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Uploading file {FileName} to blob storage container {ContainerName}",
            fileName,
            containerName);

        var blobServiceClient = CreateBlobServiceClient();
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(fileName);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        await blobClient.UploadAsync(content, overwrite: true, cancellationToken);

        _logger.LogInformation(
            "Blob upload completed for {FileName}",
            fileName);

        return blobClient.Uri.ToString();
    }

    public async Task ProcessUploadedBlobAsync(string sourceBlobName, Stream content, CancellationToken cancellationToken)
    {
        var processedBlobName = BuildProcessedBlobName(sourceBlobName);
        var destinationContainerName = _configuration["BlobTrigger:DestinationContainer"] ?? GetDefaultContainerName();
        var archiveContainerName = _configuration["BlobTrigger:ArchiveContainer"];
        var sourceContainerName = _configuration["BlobTrigger:SourceContainer"] ?? GetDefaultContainerName();

        _logger.LogInformation(
            "Processing uploaded blob {SourceBlobName} into {DestinationContainerName}",
            sourceBlobName,
            destinationContainerName);

        var originalBytes = await ReadStreamToBytesAsync(content, cancellationToken);
        var processedBytes = ProcessExcelContent(sourceBlobName, originalBytes);

        await UploadFileAsync(processedBlobName, new MemoryStream(processedBytes), destinationContainerName, cancellationToken);

        await MoveOrDeleteSourceBlobAsync(sourceContainerName, sourceBlobName, archiveContainerName, cancellationToken);
    }

    public string BuildProcessedBlobName(string originalFileName)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var extension = Path.HasExtension(originalFileName)
            ? Path.GetExtension(originalFileName)
            : string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);

        return string.IsNullOrWhiteSpace(fileNameWithoutExtension)
            ? $"{timestamp}{extension}"
            : $"{fileNameWithoutExtension}_Processed_{timestamp}{extension}";
    }

    public static List<CleanedCustomerRow> CleanCustomerRows(IEnumerable<ExcelCustomerRow> rows)
    {
        var cleanedRows = new List<CleanedCustomerRow>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var normalizedName = NormalizeText(row.Name);
            var normalizedEmail = NormalizeText(row.Email);
            var normalizedCity = NormalizeText(row.City);

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                normalizedName = "Unknown";
            }

            if (string.IsNullOrWhiteSpace(normalizedCity))
            {
                normalizedCity = "Unknown";
            }

            normalizedCity = StandardizeCity(normalizedCity);

            var normalizedEmailValue = NormalizeEmail(normalizedEmail);
            var cleanedEmail = string.IsNullOrWhiteSpace(normalizedEmailValue) || !IsValidEmail(normalizedEmailValue)
                ? string.Empty
                : normalizedEmailValue;

            if (!string.IsNullOrWhiteSpace(cleanedEmail) && !seenEmails.Add(cleanedEmail))
            {
                continue;
            }

            var (firstName, lastName) = SplitName(normalizedName);
            cleanedRows.Add(new CleanedCustomerRow(firstName, lastName, cleanedEmail, normalizedCity));
        }

        return cleanedRows;
    }

    private byte[] ProcessExcelContent(string sourceBlobName, byte[] fileBytes)
    {
        if (!IsExcelFile(sourceBlobName))
        {
            return fileBytes;
        }

        try
        {
            using var inputStream = new MemoryStream(fileBytes);
            using var workbook = new XLWorkbook(inputStream);
            var worksheet = workbook.Worksheets.FirstOrDefault() ?? workbook.Worksheets.Add("Customers");

            var cleanedRows = ExtractRows(worksheet);
            if (cleanedRows.Count == 0)
            {
                return fileBytes;
            }

            using var outputStream = new MemoryStream();
            using var cleanedWorkbook = new XLWorkbook();
            var cleanedWorksheet = cleanedWorkbook.Worksheets.Add("Customers");

            cleanedWorksheet.Cell(1, 1).Value = "FirstName";
            cleanedWorksheet.Cell(1, 2).Value = "LastName";
            cleanedWorksheet.Cell(1, 3).Value = "Email";
            cleanedWorksheet.Cell(1, 4).Value = "City";

            for (var index = 0; index < cleanedRows.Count; index++)
            {
                var row = cleanedRows[index];
                var outputRow = index + 2;
                cleanedWorksheet.Cell(outputRow, 1).Value = row.FirstName;
                cleanedWorksheet.Cell(outputRow, 2).Value = row.LastName;
                cleanedWorksheet.Cell(outputRow, 3).Value = row.Email;
                cleanedWorksheet.Cell(outputRow, 4).Value = row.City;
            }

            cleanedWorkbook.SaveAs(outputStream);
            return outputStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean Excel file {SourceBlobName}; leaving original content unchanged", sourceBlobName);
            return fileBytes;
        }
    }

    private static List<CleanedCustomerRow> ExtractRows(IXLWorksheet worksheet)
    {
        var rows = new List<ExcelCustomerRow>();
        var headerRow = worksheet.FirstRowUsed();
        if (headerRow is null)
        {
            return [];
        }

        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var headerName = NormalizeText(cell.GetString());
            if (!string.IsNullOrWhiteSpace(headerName))
            {
                headerMap[headerName] = cell.Address.ColumnNumber - 1;
            }
        }

        var nameColumn = GetColumnIndex(headerMap, "Name");
        var emailColumn = GetColumnIndex(headerMap, "Email");
        var cityColumn = GetColumnIndex(headerMap, "City");

        if (nameColumn < 0)
        {
            nameColumn = 0;
        }

        if (emailColumn < 0)
        {
            emailColumn = 1;
        }

        if (cityColumn < 0)
        {
            cityColumn = 2;
        }

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var name = GetCellValue(row.Cell(nameColumn + 1));
            var email = GetCellValue(row.Cell(emailColumn + 1));
            var city = GetCellValue(row.Cell(cityColumn + 1));

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(city))
            {
                continue;
            }

            rows.Add(new ExcelCustomerRow(name, email, city));
        }

        return CleanCustomerRows(rows);
    }

    private static int GetColumnIndex(IReadOnlyDictionary<string, int> headerMap, string columnName)
    {
        return headerMap.TryGetValue(columnName, out var index) ? index : -1;
    }

    private static string GetCellValue(IXLCell cell)
    {
        return cell.IsEmpty() ? string.Empty : NormalizeText(cell.GetString());
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string StandardizeCity(string city)
    {
        var normalizedValue = NormalizeText(city).ToLowerInvariant();

        return normalizedValue switch
        {
            "valsad" => "Valsad",
            "surat" => "Surat",
            "ahmedabad" => "Ahmedabad",
            "mumbai" => "Mumbai",
            "" => "Unknown",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalizedValue)
        };
    }

    private static (string FirstName, string LastName) SplitName(string name)
    {
        var normalizedName = NormalizeText(name).Replace(".", " ");
        var parts = Regex.Split(normalizedName, @"\s+").Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();

        if (parts.Length == 0)
        {
            return ("Unknown", string.Empty);
        }

        if (parts.Length == 1)
        {
            return (CultureInfo.InvariantCulture.TextInfo.ToTitleCase(parts[0].ToLowerInvariant()), string.Empty);
        }

        return (CultureInfo.InvariantCulture.TextInfo.ToTitleCase(parts[0].ToLowerInvariant()), string.Join(' ', parts.Skip(1).Select(part => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(part.ToLowerInvariant()))));
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var normalized = email.Trim().ToLowerInvariant();
        normalized = normalized.Replace("[at]", "@", StringComparison.OrdinalIgnoreCase)
            .Replace("(at)", "@", StringComparison.OrdinalIgnoreCase)
            .Replace(" at ", "@", StringComparison.OrdinalIgnoreCase)
            .Replace(" at", "@", StringComparison.OrdinalIgnoreCase)
            .Replace("at ", "@", StringComparison.OrdinalIgnoreCase)
            .Replace("[AT]", "@", StringComparison.OrdinalIgnoreCase)
            .Replace("(AT)", "@", StringComparison.OrdinalIgnoreCase);

        return normalized;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return MailAddress.TryCreate(email, out var address) && address.Address == email.Trim();
    }

    private static bool IsExcelFile(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<byte[]> ReadStreamToBytesAsync(Stream content, CancellationToken cancellationToken)
    {
        await using var memoryStream = new MemoryStream();
        await content.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    private async Task MoveOrDeleteSourceBlobAsync(
        string sourceContainerName,
        string sourceBlobName,
        string? archiveContainerName,
        CancellationToken cancellationToken)
    {
        var blobServiceClient = CreateBlobServiceClient();
        var sourceContainerClient = blobServiceClient.GetBlobContainerClient(sourceContainerName);
        var sourceBlobClient = sourceContainerClient.GetBlobClient(sourceBlobName);

        if (!string.IsNullOrWhiteSpace(archiveContainerName) && !string.Equals(archiveContainerName, sourceContainerName, StringComparison.OrdinalIgnoreCase))
        {
            var archiveContainerClient = blobServiceClient.GetBlobContainerClient(archiveContainerName);
            await archiveContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            await using var sourceStream = await sourceBlobClient.OpenReadAsync(cancellationToken: cancellationToken);
            await archiveContainerClient.GetBlobClient(sourceBlobName).UploadAsync(sourceStream, overwrite: true, cancellationToken);
            await sourceBlobClient.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
            _logger.LogInformation("Moved blob {SourceBlobName} to archive container {ArchiveContainerName}", sourceBlobName, archiveContainerName);
            return;
        }

        await sourceBlobClient.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        _logger.LogInformation("Deleted source blob {SourceBlobName} from container {SourceContainerName}", sourceBlobName, sourceContainerName);
    }

    private BlobServiceClient CreateBlobServiceClient()
    {
        var connectionString = _configuration["AzureStorage:ConnectionString"];
        return new BlobServiceClient(connectionString);
    }

    private string GetDefaultContainerName()
    {
        return _configuration["AzureStorage:ContainerName"] ?? "exports";
    }
}