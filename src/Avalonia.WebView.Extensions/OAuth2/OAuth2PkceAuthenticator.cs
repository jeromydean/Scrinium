using Avalonia.Controls;
using System.Net.Http;

namespace Avalonia.WebView.Extensions.OAuth2;

/// <summary>
/// Interactive OAuth 2.0 authorization code + PKCE login using
/// <see cref="WebAuthenticationBroker"/>.
/// </summary>
public static class OAuth2PkceAuthenticator
{
  public static async Task<OAuth2TokenResponse> AuthenticateInteractiveAsync(
    TopLevel topLevel,
    OAuth2PkceLoginRequest request,
    HttpClient? httpClient = null,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(topLevel);
    ArgumentNullException.ThrowIfNull(request);

    AuthorizationServerMetadata metadata = await AuthorizationServerMetadataClient.GetAsync(
      request.Issuer,
      httpClient,
      cancellationToken);

    AuthorizationCodePkceSession session = AuthorizationCodePkceSession.Create(
      metadata,
      request.ClientId,
      request.RedirectUri,
      request.Scope);

    WebAuthenticatorOptions options = new(session.AuthorizationUri, session.RedirectUri)
    {
      PreferNativeWebDialog = request.PreferNativeWebDialog,
    };

    WebAuthenticationResult result = await WebAuthenticationBroker.AuthenticateAsync(topLevel, options)
      .ConfigureAwait(true);

    AuthorizationCallbackResult parsed = AuthorizationCallbackParser.Parse(
      result.CallbackUri,
      session.State);

    return await AuthorizationServerTokenClient.ExchangeAuthorizationCodeAsync(
      metadata,
      request.ClientId,
      parsed.AuthorizationCode,
      session.RedirectUriString,
      session.CodeVerifier,
      httpClient,
      request.ClientSecret,
      cancellationToken).ConfigureAwait(false);
  }
}
