using System.Net.Http;
using System.Text.Json;

namespace Avalonia.WebView.Extensions.OAuth2;

public static class AuthorizationServerTokenClient
{
  private static readonly HttpClient SharedClient = new();

  public static async Task<OAuth2TokenResponse> ExchangeAuthorizationCodeAsync(
    AuthorizationServerMetadata metadata,
    string clientId,
    string authorizationCode,
    string redirectUri,
    string codeVerifier,
    HttpClient? httpClient = null,
    string? clientSecret = null,
    CancellationToken cancellationToken = default)
  {
    if (metadata.TokenEndpoint is not { Length: > 0 } tokenEndpoint)
    {
      throw new InvalidOperationException("Authorization server metadata is missing token_endpoint.");
    }

    List<KeyValuePair<string, string>> form =
    [
      new("grant_type", "authorization_code"),
      new("client_id", clientId),
      new("code", authorizationCode),
      new("redirect_uri", redirectUri),
      new("code_verifier", codeVerifier),
    ];

    if (!string.IsNullOrEmpty(clientSecret))
    {
      form.Add(new KeyValuePair<string, string>("client_secret", clientSecret));
    }

    HttpClient client = httpClient ?? SharedClient;
    using FormUrlEncodedContent content = new(form);
    using HttpResponseMessage response = await client.PostAsync(tokenEndpoint, content, cancellationToken)
      .ConfigureAwait(false);
    string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    if (!response.IsSuccessStatusCode)
    {
      throw new InvalidOperationException($"Token endpoint returned {(int)response.StatusCode}: {body}");
    }

    OAuth2TokenResponse? token = JsonSerializer.Deserialize(body, OAuth2JsonContext.Default.OAuth2TokenResponse);
    if (token is null || string.IsNullOrEmpty(token.AccessToken))
    {
      throw new InvalidOperationException("Token response could not be read.");
    }

    return token;
  }
}
