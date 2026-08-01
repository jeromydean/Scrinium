using System;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scrinium.Api.Options;
using Scrinium.Api.Hubs;
using Scrinium.Api.Services;
using Scrinium.Core.Ports;
using Scrinium.Infrastructure.DependencyInjection;
using Scrinium.Infrastructure.Persistence;
using Scrinium.Workers.DependencyInjection;

namespace Scrinium.Api;

public class Program
{
  public static void Main(string[] args)
  {
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddSignalR();
    builder.Services.AddAuthorization();
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options =>
      {
        KeycloakOptions keycloak = builder.Configuration
          .GetSection(KeycloakOptions.SectionName)
          .Get<KeycloakOptions>() ?? new KeycloakOptions();

        options.Authority = keycloak.Authority;
        options.Audience = keycloak.Audience;
        options.RequireHttpsMetadata = true;
      });

    builder.Services.Configure<IngestionOptions>(
      builder.Configuration.GetSection(IngestionOptions.SectionName));
    builder.Services.Configure<KeycloakOptions>(
      builder.Configuration.GetSection(KeycloakOptions.SectionName));

    builder.Services.AddScriniumInfrastructure(builder.Configuration);
    builder.Services.AddScriniumWorkers(builder.Configuration);

    builder.Services.AddSingleton<IIngestionStagingStore, IngestionStagingStore>();
    builder.Services.AddSingleton<IClientMetadataReader, ClientMetadataReader>();
    builder.Services.AddSingleton<IDocumentReadyNotifier, SignalRDocumentReadyNotifier>();

    WebApplication app = builder.Build();

    using (IServiceScope scope = app.Services.CreateScope())
    {
      ScriniumDbContext dbContext = scope.ServiceProvider.GetRequiredService<ScriniumDbContext>();
      dbContext.Database.Migrate();
    }

    if (app.Environment.IsDevelopment())
    {
      app.MapOpenApi();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<IngestionHub>("/hubs/ingestion");

    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
      .WithName("HealthCheck")
      .AllowAnonymous();

    app.Run();
  }
}
