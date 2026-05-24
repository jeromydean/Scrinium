using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Scrinium.Api
{
  public class Program
  {
    public static void Main(string[] args)
    {
      WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

      builder.Services.AddOpenApi();

      WebApplication app = builder.Build();

      if (app.Environment.IsDevelopment())
      {
        app.MapOpenApi();
      }

      app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
        .WithName("HealthCheck");

      app.Run();
    }
  }
}
