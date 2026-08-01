using System;
using Scrinium.Core.Rendering;

namespace Scrinium.Core.Storage;

public static class BlobKeys
{
  public static string Original(Guid archiveId, string fileName)
    => $"archives/{archiveId:N}/original/{fileName}";

  public static string NormalizedPdf(Guid archiveId)
    => $"archives/{archiveId:N}/pdf/normalized.pdf";

  public static string SheetRender(Guid archiveId, Guid archiveSheetId, RenderTier tier, int sequenceInArchive)
    => $"archives/{archiveId:N}/sheets/{archiveSheetId:N}/{RenderTierDefaults.GetFolderName(tier)}/sheet_{sequenceInArchive:D4}.webp";
}
