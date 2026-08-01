namespace Scrinium.IngestionClient.Options;

public sealed class ApiOptions
{
  public const string SectionName = "Api";

  public string BaseUrl { get; set; } = "http://localhost:5243";
}

public sealed class KeycloakClientOptions
{
  public const string SectionName = "Keycloak";

  public string TokenUrl { get; set; } = string.Empty;

  public string ClientId { get; set; } = "scrinium-api";

  public string ClientSecret { get; set; } = string.Empty;

  public string Username { get; set; } = string.Empty;

  public string Password { get; set; } = string.Empty;

  public string Scope { get; set; } = "openid profile email";
}

public sealed class WatcherOptions
{
  public const string SectionName = "Watcher";

  public string WatchFolder { get; set; } = string.Empty;

  public int PollIntervalMs { get; set; } = 2000;

  public int StableChecksRequired { get; set; } = 3;

  public bool IncludeSubdirectories { get; set; }

  public string[] DefaultTags { get; set; } = ["folder-watcher"];

  public string ProcessedSubfolder { get; set; } = "processed";

  public string FailedSubfolder { get; set; } = "failed";

  public bool WaitForReady { get; set; } = true;

  public int ReadyPollIntervalMs { get; set; } = 2000;

  public int ReadyTimeoutSeconds { get; set; } = 300;

  public bool DownloadPreviewOnReady { get; set; }

  public string PreviewOutputFolder { get; set; } = "previews";
}
