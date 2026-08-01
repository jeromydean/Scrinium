namespace Scrinium.Core.Rendering;

public enum RenderTier
{
  Thumb = 0,
  Preview = 1,
  Full = 2,
}

public static class RenderTierDefaults
{
  public static int GetTargetWidth(RenderTier tier) => tier switch
  {
    RenderTier.Thumb => 150,
    RenderTier.Preview => 800,
    RenderTier.Full => 1920,
    _ => 800,
  };

  public static string GetFolderName(RenderTier tier) => tier switch
  {
    RenderTier.Thumb => "thumb",
    RenderTier.Preview => "preview",
    RenderTier.Full => "full",
    _ => "preview",
  };
}
