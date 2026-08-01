using System;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Scrinium.Configuration;
using Scrinium.Options;
using Scrinium.Services;
using Scrinium.ViewModels;
using Scrinium.Views;

namespace Scrinium;

public partial class App : Application
{
  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      IConfiguration configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddRepoEnvFile()
        .AddEnvironmentVariables(prefix: "SCRINIUM_")
        .Build();

      ApiOptions apiOptions = configuration.GetSection("Api").Get<ApiOptions>() ?? new ApiOptions();
      AuthOptions authOptions = configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();

      HttpClientHandler authHandler = new()
      {
        ServerCertificateCustomValidationCallback =
          HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
      };

      HttpClient authHttpClient = new(authHandler);
      InteractiveAuthService authService = new(authOptions, authHttpClient);

      HttpClient apiHttpClient = new();
      ScriniumApiClient apiClient = new(apiHttpClient, apiOptions, authService);

      MainWindowViewModel viewModel = new(apiClient, authService);
      MainWindow mainWindow = new()
      {
        DataContext = viewModel,
      };

      mainWindow.Opened += async (_, _) =>
      {
        await viewModel.InitializeAsync(mainWindow);
      };

      desktop.MainWindow = mainWindow;
    }

    base.OnFrameworkInitializationCompleted();
  }
}
