using System.Data;
using AzureExcelExporter.Application.Interfaces;
using AzureExcelExporter.Domain.Entities;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzureExcelExporter.Infrastructure.Repositories;

public class DapperDataRepository : IDataRepository
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DapperDataRepository> _logger;

    public DapperDataRepository(
        IConfiguration configuration,
        ILogger<DapperDataRepository> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersForExportAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Executing Dapper query to read customers");

        await using var connection = new SqlConnection(
            _configuration["Database:ConnectionString"]);

        await connection.OpenAsync(cancellationToken);

        var sql =
            "SELECT CustomerId, Name, Email, City, CreatedAt FROM Customers";

        var customers =
            (await connection.QueryAsync<Customer>(sql)).ToList();

        _logger.LogInformation(
            "Dapper query completed with {Count} customers",
            customers.Count);

        return customers;
    }
}