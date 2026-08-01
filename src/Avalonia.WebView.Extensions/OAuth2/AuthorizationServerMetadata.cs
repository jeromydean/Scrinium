using System.Text.Json.Serialization;

namespace Avalonia.WebView.Extensions.OAuth2;

/// <summary>
/// OAuth 2.0 Authorization Server Metadata per RFC 8414.
/// </summary>
public sealed class AuthorizationServerMetadata
{
  [JsonPropertyName("issuer")]
  public string? Issuer { get; init; }

  [JsonPropertyName("authorization_endpoint")]
  public string? AuthorizationEndpoint { get; init; }

  [JsonPropertyName("token_endpoint")]
  public string? TokenEndpoint { get; init; }

  [JsonPropertyName("code_challenge_methods_supported")]
  public string[]? CodeChallengeMethodsSupported { get; init; }

  [JsonPropertyName("scopes_supported")]
  public string[]? ScopesSupported { get; init; }

  [JsonPropertyName("response_types_supported")]
  public string[]? ResponseTypesSupported { get; init; }
}
