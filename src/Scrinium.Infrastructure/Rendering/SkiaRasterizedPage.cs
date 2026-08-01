using System;
using System.Threading.Tasks;
using Scrinium.Core.Rendering;
using SkiaSharp;

namespace Scrinium.Infrastructure.Rendering;

internal sealed class SkiaRasterizedPage : IRasterizedPage
{
  private SKBitmap? _bitmap;

  public SkiaRasterizedPage(SKBitmap bitmap)
  {
    _bitmap = bitmap;
  }

  public SKBitmap Bitmap => _bitmap
    ?? throw new ObjectDisposedException(nameof(SkiaRasterizedPage));

  public int Width => Bitmap.Width;

  public int Height => Bitmap.Height;

  public ValueTask DisposeAsync()
  {
    _bitmap?.Dispose();
    _bitmap = null;
    return ValueTask.CompletedTask;
  }
}
