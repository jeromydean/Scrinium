using System.Text.Json.Serialization;

namespace Avalonia.WebView.Extensions.OAuth2;

public sealed class OAuth2TokenResponse
{
  [JsonPropertyName("access_token")]
  public string? AccessToken { get; init; }

  [JsonPropertyName("token_type")]
  public string? TokenType { get; init; }

  [JsonPropertyName("expires_in")]
  public long? ExpiresIn { get; init; }

  [JsonPropertyName("refresh_token")]
  public string? RefreshToken { get; init; }

  [JsonPropertyName("id_token")]
  public string? IdToken { get; init; }

  [JsonPropertyName("scope")]
  public string? Scope { get; init; }
}
