namespace Scrinium.Options;

public sealed class ApiOptions
{
  public string BaseUrl { get; set; } = "http://localhost:5243";
}

public sealed class AuthOptions
{
  public string Issuer { get; set; } = "https://localhost:8443/realms/scrinium";

  public string ClientId { get; set; } = "scrinium-avalonia";

  public string RedirectUri { get; set; } = "http://localhost";

  public string Scope { get; set; } = "openid profile email";
}
