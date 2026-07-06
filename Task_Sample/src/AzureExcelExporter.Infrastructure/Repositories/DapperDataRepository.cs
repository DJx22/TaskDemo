using System.Data;
using AzureExcelExporter.Application.Interfaces;
using AzureExcelExporter.Domain.Entities;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AzureExcelExporter.Infrastructure.Options;

namespace AzureExcelExporter.Infrastructure.Repositories;

public class DapperDataRepository : IDataRepository
{
    private readonly DatabaseOptions _databaseOptions;
    private readonly ILogger<DapperDataRepository> _logger;

    public DapperDataRepository(IOptions<DatabaseOptions> databaseOptions, ILogger<DapperDataRepository> logger)
    {
        _databaseOptions = databaseOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersForExportAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing Dapper query to read customers");
        await using var connection = new SqlConnection(_databaseOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = "SELECT CustomerId, Name, Email, City, CreatedAt FROM Customers";
        var customers = (await connection.QueryAsync<Customer>(sql)).ToList();

        _logger.LogInformation("Dapper query completed with {Count} customers", customers.Count);
        return customers;
    }
}
