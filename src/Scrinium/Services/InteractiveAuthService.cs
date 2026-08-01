using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.WebView.Extensions.OAuth2;
using Scrinium.Options;

namespace Scrinium.Services;

public interface IAccessTokenProvider
{
  bool IsSignedIn { get; }

  Task SignInAsync(TopLevel topLevel, CancellationToken cancellationToken = default);

  Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class InteractiveAuthService : IAccessTokenProvider
{
  private readonly OAuth2PkceLoginRequest _loginRequest;
  private readonly HttpClient _httpClient;
  private OAuth2TokenResponse? _token;
  private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

  public InteractiveAuthService(AuthOptions options, HttpClient httpClient)
  {
    _httpClient = httpClient;
    _loginRequest = new OAuth2PkceLoginRequest
    {
      Issuer = options.Issuer,
      ClientId = options.ClientId,
      RedirectUri = options.RedirectUri,
      Scope = options.Scope,
      PreferNativeWebDialog = true,
    };
  }

  public bool IsSignedIn =>
    _token?.AccessToken is not null && DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-30);

  public async Task SignInAsync(TopLevel topLevel, CancellationToken cancellationToken = default)
  {
    OAuth2TokenResponse token = await OAuth2PkceAuthenticator.AuthenticateInteractiveAsync(
      topLevel,
      _loginRequest,
      _httpClient,
      cancellationToken);

    _token = token;
    _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(token.ExpiresIn ?? 300, 60));
  }

  public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
  {
    if (!IsSignedIn || string.IsNullOrWhiteSpace(_token?.AccessToken))
    {
      throw new InvalidOperationException("Sign in is required before calling the API.");
    }

    return Task.FromResult(_token.AccessToken);
  }
}
