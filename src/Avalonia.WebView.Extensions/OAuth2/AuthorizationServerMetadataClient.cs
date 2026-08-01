using System.Net.Http;
using System.Text.Json;

namespace Avalonia.WebView.Extensions.OAuth2;

public static class AuthorizationServerMetadataClient
{
  private static readonly HttpClient SharedClient = new();

  public static async Task<AuthorizationServerMetadata> GetAsync(
    string issuer,
    HttpClient? httpClient = null,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(issuer))
    {
      throw new ArgumentException("Issuer is required.", nameof(issuer));
    }

    string metadataUrl = GetWellKnownMetadataUrl(issuer);
    HttpClient client = httpClient ?? SharedClient;
    using HttpResponseMessage response = await client.GetAsync(metadataUrl, cancellationToken)
      .ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    AuthorizationServerMetadata? metadata = JsonSerializer.Deserialize(
      json,
      OAuth2JsonContext.Default.AuthorizationServerMetadata);

    if (metadata is null)
    {
      throw new InvalidOperationException("Authorization server metadata response was empty.");
    }

    return metadata;
  }

  public static string GetWellKnownMetadataUrl(string issuer)
  {
    string trimmed = issuer.TrimEnd('/');
    return $"{trimmed}/.well-known/oauth-authorization-server";
  }
}
