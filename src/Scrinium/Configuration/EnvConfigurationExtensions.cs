using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace Scrinium.Configuration;

public static class EnvConfigurationExtensions
{
  private static readonly Dictionary<string, string> EnvToConfigMap = new(StringComparer.OrdinalIgnoreCase)
  {
    ["SCRINIUM_DESKTOP_CLIENT_ID"] = "Auth:ClientId",
  };

  public static IConfigurationBuilder AddRepoEnvFile(this IConfigurationBuilder builder)
  {
    string? envPath = FindEnvFile();
    if (envPath is null)
    {
      return builder;
    }

    Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase);
    foreach (string line in File.ReadAllLines(envPath))
    {
      string trimmed = line.Trim();
      if (trimmed.Length == 0 || trimmed.StartsWith('#'))
      {
        continue;
      }

      int separator = trimmed.IndexOf('=');
      if (separator <= 0)
      {
        continue;
      }

      string key = trimmed[..separator].Trim();
      string value = trimmed[(separator + 1)..].Trim();
      values[key] = value;

      if (EnvToConfigMap.TryGetValue(key, out string? configKey))
      {
        values[configKey] = value;
      }

      if (string.Equals(key, "KEYCLOAK_REALM", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(value))
      {
        values["Auth:Issuer"] = $"https://localhost:8443/realms/{value}";
      }
    }

    builder.AddInMemoryCollection(values);
    return builder;
  }

  private static string? FindEnvFile()
  {
    foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
      DirectoryInfo? current = new(startPath);
      for (int i = 0; i < 6 && current is not null; i++)
      {
        string candidate = Path.Combine(current.FullName, ".env");
        if (File.Exists(candidate))
        {
          return candidate;
        }

        current = current.Parent;
      }
    }

    return null;
  }
}
