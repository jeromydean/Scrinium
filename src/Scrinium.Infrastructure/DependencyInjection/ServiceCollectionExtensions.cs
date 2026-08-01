using System;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Scrinium.Core.Ports;
using Scrinium.Infrastructure.Blob;
using Scrinium.Infrastructure.Extraction;
using Scrinium.Infrastructure.Options;
using Scrinium.Infrastructure.Persistence;
using Scrinium.Infrastructure.Persistence.Repositories;
using Scrinium.Infrastructure.Queue;
using Scrinium.Infrastructure.Rendering;
using Scrinium.Infrastructure.Search;
using Scrinium.Infrastructure.Telemetry;
using StackExchange.Redis;

namespace Scrinium.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddScriniumInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    string connectionString = configuration.GetConnectionString("Default")
      ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

    services.AddDbContext<ScriniumDbContext>(options =>
      options.UseNpgsql(connectionString));

    services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
    services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));
    services.Configure<TikaOptions>(configuration.GetSection(TikaOptions.SectionName));
    services.Configure<GotenbergOptions>(configuration.GetSection(GotenbergOptions.SectionName));
    services.Configure<SolrOptions>(configuration.GetSection(SolrOptions.SectionName));
    services.Configure<RenderingOptions>(configuration.GetSection(RenderingOptions.SectionName));

    services.AddSingleton<IConnectionMultiplexer>(_ =>
      ConnectionMultiplexer.Connect(
        configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()?.ConnectionString
          ?? "localhost:6379"));

    services.AddSingleton<IMinioClient>(sp =>
    {
      MinioOptions options = sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<MinioOptions>>().Value;

      return new MinioClient()
        .WithEndpoint(options.Endpoint)
        .WithCredentials(options.AccessKey, options.SecretKey)
        .WithSSL(options.UseSsl)
        .Build();
    });

    services.AddHttpClient<TikaMetadataClient>();
    services.AddHttpClient<GotenbergNormalizer>();
    services.AddHttpClient<SolrDocumentIndexer>((sp, client) =>
    {
      SolrOptions options = sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<SolrOptions>>().Value;
      client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    })
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
      SolrOptions options = sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<SolrOptions>>().Value;

      if (!options.SkipCertificateValidation)
      {
        return new HttpClientHandler();
      }

      return new HttpClientHandler
      {
        ServerCertificateCustomValidationCallback =
          HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
      };
    });

    services.AddScoped<IDocumentRepository, DocumentRepository>();
    services.AddScoped<ITagService, TagService>();
    services.AddScoped<IIngestTelemetry, PostgresIngestTelemetry>();
    services.AddScoped<ISearchIndexer, SolrDocumentIndexer>();
    services.AddSingleton<IJobQueue, RedisStreamJobQueue>();
    services.AddSingleton<IBlobStore, MinioBlobStore>();
    services.AddSingleton<IFormatRouter, FormatRouter>();
    services.AddScoped<IDocumentNormalizer, GotenbergNormalizer>();
    services.AddScoped<IDocumentExtractor, DocumentExtractor>();
    services.AddScoped<IPageRenderer, PageRenderer>();

    return services;
  }
}
