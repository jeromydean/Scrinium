using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scrinium.Api.Options;
using Scrinium.Api.Services;
using Scrinium.Api.Workers;

namespace Scrinium.Api
{
  public class Program
  {
    public static void Main(string[] args)
    {
      WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

      builder.Services.AddControllers();
      builder.Services.AddOpenApi();
      builder.Services.Configure<IngestionOptions>(
        builder.Configuration.GetSection(IngestionOptions.SectionName));
      builder.Services.AddSingleton<IIngestionQueue, IngestionQueue>();
      builder.Services.AddSingleton<IIngestionStagingStore, IngestionStagingStore>();
      builder.Services.AddHostedService<IngestionBackgroundService>();

      WebApplication app = builder.Build();

      if (app.Environment.IsDevelopment())
      {
        app.MapOpenApi();
      }

      app.MapControllers();

      app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
        .WithName("HealthCheck");

      app.Run();
    }
  }
}
