using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Scrinium.Core.Ingestion;

public static partial class ClientMetadataParser
{
  public const int MaxKeys = 50;
  public const int MaxKeyLength = 128;
  public const int MaxValueLength = 4096;

  [GeneratedRegex("^[a-z0-9._-]+$", RegexOptions.CultureInvariant)]
  private static partial Regex KeyPattern();

  public static Dictionary<string, string> ParseFromForm(
    IReadOnlyDictionary<string, string> formFields)
  {
    Dictionary<string, string> metadata = new(StringComparer.Ordinal);

    foreach ((string key, string value) in formFields)
    {
      if (!key.StartsWith("metadata[", StringComparison.Ordinal) || !key.EndsWith(']'))
      {
        continue;
      }

      string rawKey = key["metadata[".Length..^1];
      AddKeyValue(metadata, rawKey, value);
    }

    return metadata;
  }

  public static Dictionary<string, string> ParseFromJson(string json)
  {
    Dictionary<string, string>? parsed =
      System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);

    if (parsed is null)
    {
      throw new FormatException("metadata must be a JSON object of string key/value pairs.");
    }

    Dictionary<string, string> metadata = new(StringComparer.Ordinal);
    foreach ((string key, string value) in parsed)
    {
      AddKeyValue(metadata, key, value);
    }

    return metadata;
  }

  private static void AddKeyValue(
    Dictionary<string, string> metadata,
    string rawKey,
    string rawValue)
  {
    if (metadata.Count >= MaxKeys)
    {
      throw new InvalidOperationException($"metadata cannot contain more than {MaxKeys} keys.");
    }

    string key = rawKey.Trim().ToLowerInvariant();
    if (key.Length == 0 || key.Length > MaxKeyLength || !KeyPattern().IsMatch(key))
    {
      throw new FormatException(
        $"metadata key '{rawKey}' is invalid. Keys must match [a-z0-9._-] after normalization.");
    }

    string value = rawValue.Trim();
    if (value.Length > MaxValueLength)
    {
      throw new FormatException($"metadata value for '{key}' exceeds {MaxValueLength} characters.");
    }

    metadata[key] = value;
  }
}
