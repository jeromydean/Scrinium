using System;
using Scrinium.Core.Extraction;
using Scrinium.Core.Ports;

namespace Scrinium.Infrastructure.Extraction;

public sealed class FormatRouter : IFormatRouter
{
  public DocumentFormatKind Classify(string contentType)
  {
    if (string.IsNullOrWhiteSpace(contentType))
    {
      return DocumentFormatKind.Office;
    }

    string mime = contentType.Split(';', StringSplitOptions.TrimEntries)[0].ToLowerInvariant();

    if (mime == "application/pdf")
    {
      return DocumentFormatKind.Pdf;
    }

    if (mime.StartsWith("image/", StringComparison.Ordinal))
    {
      return DocumentFormatKind.Image;
    }

    if (mime is "text/html"
      or "application/xhtml+xml"
      or "message/rfc822"
      or "text/markdown")
    {
      return DocumentFormatKind.Web;
    }

    return DocumentFormatKind.Office;
  }
}
