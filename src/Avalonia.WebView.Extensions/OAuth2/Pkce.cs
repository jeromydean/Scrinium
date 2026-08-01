using System.Security.Cryptography;
using System.Text;

namespace Avalonia.WebView.Extensions.OAuth2;

/// <summary>
/// Proof Key for Code Exchange (PKCE) helpers per RFC 7636.
/// </summary>
public static class Pkce
{
  public static string CreateCodeVerifier(int size = 64)
  {
    if (size is < 43 or > 128)
    {
      throw new ArgumentOutOfRangeException(nameof(size), "Verifier length must be between 43 and 128.");
    }

    byte[] bytes = new byte[size];
    RandomNumberGenerator.Fill(bytes);
    return Base64UrlEncode(bytes);
  }

  public static string CreateCodeChallengeS256(string codeVerifier)
  {
    byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
    return Base64UrlEncode(hash);
  }

  private static string Base64UrlEncode(ReadOnlySpan<byte> data)
  {
    return Convert.ToBase64String(data)
      .TrimEnd('=')
      .Replace('+', '-')
      .Replace('/', '_');
  }
}
