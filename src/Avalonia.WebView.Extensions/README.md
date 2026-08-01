# Avalonia.WebView.Extensions

OAuth 2.0 authorization server metadata discovery (RFC 8414) and authorization-code + PKCE (RFC 7636) helpers for Avalonia desktop apps using [`WebAuthenticationBroker`](https://docs.avaloniaui.net/docs/app-development/embedding-web-content).

This code is adapted from the [`feature/oauth2-rfc8414-pkce`](https://github.com/jeromydean/Avalonia.Controls.WebView/tree/feature/oauth2-rfc8414-pkce) branch of [jeromydean/Avalonia.Controls.WebView](https://github.com/jeromydean/Avalonia.Controls.WebView), extracted into a standalone library so Scrinium (and other apps) can use it without vendoring the full WebView repo.

## Usage

```csharp
var token = await OAuth2PkceAuthenticator.AuthenticateInteractiveAsync(
  topLevel,
  new OAuth2PkceLoginRequest
  {
    Issuer = "https://localhost:8443/realms/scrinium",
    ClientId = "scrinium-avalonia",
    RedirectUri = "http://localhost",
    Scope = "openid profile email",
  },
  httpClient);
```

Register the same redirect URI with your identity provider (Keycloak allows `http://localhost` and `http://127.0.0.1:*` in local dev).
