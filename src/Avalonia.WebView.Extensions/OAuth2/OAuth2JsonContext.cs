using System.Text.Json.Serialization;

namespace Avalonia.WebView.Extensions.OAuth2;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AuthorizationServerMetadata))]
[JsonSerializable(typeof(OAuth2TokenResponse))]
internal partial class OAuth2JsonContext : JsonSerializerContext;
