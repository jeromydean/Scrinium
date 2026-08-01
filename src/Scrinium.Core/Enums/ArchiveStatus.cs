namespace Scrinium.Core.Enums;

public enum ArchiveStatus
{
  Uploading = 0,
  Queued = 1,
  Extracting = 2,
  Rendering = 3,
  Ready = 4,
  Error = 5,
}

public enum BundleStatus
{
  Processing = 0,
  Ready = 1,
  Error = 2,
}
