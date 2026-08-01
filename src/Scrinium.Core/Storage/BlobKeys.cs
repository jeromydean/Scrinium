using System;
using Scrinium.Core.Rendering;

namespace Scrinium.Core.Storage;

public static class BlobKeys
{
  public static string Original(Guid documentId, string fileName)
    => $"documents/{documentId:N}/original/{fileName}";

  public static string NormalizedPdf(Guid documentId)
    => $"documents/{documentId:N}/pdf/normalized.pdf";

  public static string PageRender(Guid documentId, RenderTier tier, int pageNumber)
    => $"documents/{documentId:N}/pages/{RenderTierDefaults.GetFolderName(tier)}/page_{pageNumber:D4}.webp";
}
