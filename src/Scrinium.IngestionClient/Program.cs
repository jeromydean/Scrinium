using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scrinium.IngestionClient.Configuration;
using Scrinium.IngestionClient.Options;
using Scrinium.IngestionClient.Services;

namespace Scrinium.IngestionClient;

public static class Program
{
  public static void Main(string[] args)
  {
    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    builder.Configuration
      .AddRepoEnvFile();

    if (TryGetArgValue(args, "--folder", out string? folder))
    {
      builder.Configuration["Watcher:WatchFolder"] = folder;
    }

    if (TryGetArgValue(args, "--api", out string? apiBaseUrl))
    {
      builder.Configuration["Api:BaseUrl"] = apiBaseUrl;
    }

    builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));
    builder.Services.Configure<KeycloakClientOptions>(builder.Configuration.GetSection(KeycloakClientOptions.SectionName));
    builder.Services.Configure<WatcherOptions>(builder.Configuration.GetSection(WatcherOptions.SectionName));

    builder.Services.AddHttpClient<KeycloakTokenProvider>((_, client) =>
    {
      client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
      ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    });

    builder.Services.AddHttpClient<IngestionApiClient>((_, client) =>
    {
      client.Timeout = TimeSpan.FromMinutes(10);
    });

    builder.Services.AddHostedService<FolderIngestionWatcher>();

    IHost host = builder.Build();
    host.Run();
  }

  private static bool TryGetArgValue(string[] args, string name, out string? value)
  {
    value = null;
    for (int i = 0; i < args.Length - 1; i++)
    {
      if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
      {
        value = args[i + 1];
        return true;
      }
    }

    return false;
  }
}
