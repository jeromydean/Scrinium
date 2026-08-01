namespace Avalonia.WebView.Extensions.OAuth2;

public static class AuthorizationCallbackParser
{
  public static AuthorizationCallbackResult Parse(Uri callbackUri, string expectedState)
  {
    string query = callbackUri.Query;
    if (string.IsNullOrEmpty(query))
    {
      throw new InvalidOperationException("Callback URI has no query string.");
    }

    Dictionary<string, string> values = ParseQueryString(query);
    if (values.TryGetValue("error", out string? error) && !string.IsNullOrEmpty(error))
    {
      values.TryGetValue("error_description", out string? description);
      throw new InvalidOperationException(
        string.IsNullOrEmpty(description) ? error : $"{error}: {description}");
    }

    if (!values.TryGetValue("code", out string? code) || string.IsNullOrEmpty(code))
    {
      throw new InvalidOperationException("Callback URI is missing code.");
    }

    if (!values.TryGetValue("state", out string? state) || string.IsNullOrEmpty(state))
    {
      throw new InvalidOperationException("Callback URI is missing state.");
    }

    if (!string.Equals(state, expectedState, StringComparison.Ordinal))
    {
      throw new InvalidOperationException("State does not match the authorization request.");
    }

    return new AuthorizationCallbackResult(code);
  }

  private static Dictionary<string, string> ParseQueryString(string query)
  {
    Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
    string trimmed = query.StartsWith('?') ? query[1..] : query;
    if (trimmed.Length == 0)
    {
      return values;
    }

    foreach (string part in trimmed.Split('&'))
    {
      if (part.Length == 0)
      {
        continue;
      }

      int separator = part.IndexOf('=');
      string key;
      string value;
      if (separator < 0)
      {
        key = Uri.UnescapeDataString(part);
        value = string.Empty;
      }
      else
      {
        key = Uri.UnescapeDataString(part[..separator]);
        value = Uri.UnescapeDataString(part[(separator + 1)..]);
      }

      values[key] = value;
    }

    return values;
  }
}

public readonly record struct AuthorizationCallbackResult(string AuthorizationCode);
