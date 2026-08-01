namespace Avalonia.WebView.Extensions.OAuth2;

public sealed class OAuth2PkceLoginRequest
{
  public required string Issuer { get; init; }

  public required string ClientId { get; init; }

  public required string RedirectUri { get; init; }

  public required string Scope { get; init; }

  public string? ClientSecret { get; init; }

  public bool PreferNativeWebDialog { get; init; } = true;
}
