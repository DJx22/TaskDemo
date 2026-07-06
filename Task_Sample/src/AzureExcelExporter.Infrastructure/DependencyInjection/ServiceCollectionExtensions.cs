using AzureExcelExporter.Application.Behaviors;
using AzureExcelExporter.Application.Interfaces;
using AzureExcelExporter.Infrastructure.Options;
using AzureExcelExporter.Infrastructure.Persistence;
using AzureExcelExporter.Infrastructure.Repositories;
using AzureExcelExporter.Infrastructure.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AzureExcelExporter.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<AzureStorageOptions>(configuration.GetSection(AzureStorageOptions.SectionName));

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("SqlConnection")));

        services.AddScoped<IDataRepository, DapperDataRepository>();
        services.AddScoped<IExcelService, ExcelService>();
        services.AddScoped<IBlobStorageService, BlobStorageService>();
        services.AddScoped<IFileProcessorService, FileProcessorService>();
        services.AddScoped<IExportHistoryService, ExportHistoryService>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AzureExcelExporter.Application.Features.ExportData.Commands.ExportDataCommand).Assembly));
        services.AddValidatorsFromAssembly(typeof(AzureExcelExporter.Application.Features.ExportData.Commands.ExportDataCommand).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
