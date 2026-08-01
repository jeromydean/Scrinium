using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scrinium.Workers.Ingestion;
using Scrinium.Workers.Options;

namespace Scrinium.Workers.DependencyInjection;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddScriniumWorkers(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    services.Configure<WorkerOptions>(configuration.GetSection(WorkerOptions.SectionName));
    services.AddHostedService<ArchiveIngestionWorker>();
    services.AddHostedService<ArchiveSheetIngestionWorker>();
    services.AddHostedService<FinalizeBundleWorker>();
    return services;
  }
}
