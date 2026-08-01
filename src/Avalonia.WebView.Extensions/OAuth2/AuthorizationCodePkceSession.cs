using System.Security.Cryptography;

namespace Avalonia.WebView.Extensions.OAuth2;

public sealed class AuthorizationCodePkceSession
{
  private AuthorizationCodePkceSession(
    Uri authorizationUri,
    Uri redirectUri,
    string redirectUriString,
    string state,
    string codeVerifier,
    string? nonce)
  {
    AuthorizationUri = authorizationUri;
    RedirectUri = redirectUri;
    RedirectUriString = redirectUriString;
    State = state;
    CodeVerifier = codeVerifier;
    Nonce = nonce;
  }

  public Uri AuthorizationUri { get; }

  public Uri RedirectUri { get; }

  public string RedirectUriString { get; }

  public string State { get; }

  public string CodeVerifier { get; }

  public string? Nonce { get; }

  public static AuthorizationCodePkceSession Create(
    AuthorizationServerMetadata metadata,
    string clientId,
    string redirectUri,
    string scope,
    string? nonce = null,
    string? resource = null)
  {
    if (metadata.AuthorizationEndpoint is not { Length: > 0 } authEndpoint)
    {
      throw new InvalidOperationException("Authorization server metadata is missing authorization_endpoint.");
    }

    string redirectForOAuth = redirectUri.Trim();
    if (redirectForOAuth.Length == 0)
    {
      throw new ArgumentException("Redirect URI is required.", nameof(redirectUri));
    }

    if (!Uri.TryCreate(redirectForOAuth, UriKind.Absolute, out Uri? redirectUriParsed))
    {
      throw new ArgumentException("Redirect URI must be an absolute URL.", nameof(redirectUri));
    }

    EnsurePkceS256Supported(metadata);

    string codeVerifier = Pkce.CreateCodeVerifier();
    string codeChallenge = Pkce.CreateCodeChallengeS256(codeVerifier);
    string state = CreateState();

    List<string> query =
    [
      "response_type=code",
      $"client_id={Uri.EscapeDataString(clientId)}",
      $"redirect_uri={Uri.EscapeDataString(redirectForOAuth)}",
      $"scope={Uri.EscapeDataString(scope)}",
      $"state={Uri.EscapeDataString(state)}",
      $"code_challenge={Uri.EscapeDataString(codeChallenge)}",
      "code_challenge_method=S256",
    ];

    if (!string.IsNullOrEmpty(nonce))
    {
      query.Add($"nonce={Uri.EscapeDataString(nonce)}");
    }

    if (!string.IsNullOrEmpty(resource))
    {
      query.Add($"resource={Uri.EscapeDataString(resource)}");
    }

    char separator = authEndpoint.Contains('?', StringComparison.Ordinal) ? '&' : '?';
    Uri authorizationUri = new($"{authEndpoint}{separator}{string.Join("&", query)}");

    return new AuthorizationCodePkceSession(
      authorizationUri,
      redirectUriParsed,
      redirectForOAuth,
      state,
      codeVerifier,
      nonce);
  }

  private static void EnsurePkceS256Supported(AuthorizationServerMetadata metadata)
  {
    string[]? methods = metadata.CodeChallengeMethodsSupported;
    if (methods is null || methods.Length == 0)
    {
      return;
    }

    foreach (string method in methods)
    {
      if (string.Equals(method, "S256", StringComparison.OrdinalIgnoreCase))
      {
        return;
      }
    }

    throw new InvalidOperationException(
      "Authorization server metadata lists code_challenge_methods_supported but does not include S256.");
  }

  private static string CreateState()
  {
    byte[] bytes = new byte[32];
    RandomNumberGenerator.Fill(bytes);
    return Convert.ToHexString(bytes);
  }
}
