using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Scrinium.Core.Domain;
using Scrinium.Core.Extraction;
using Scrinium.Core.Ports;
using Scrinium.Core.Rendering;
using Scrinium.Infrastructure.Rendering;
using SkiaSharp;
using ZXing;
using ZXing.SkiaSharp;

namespace Scrinium.Infrastructure.Extraction;

public sealed class ZxingBarcodeScanner : IBarcodeScanner
{
  public Task<IReadOnlyList<BarcodeResult>> ScanAsync(
    IRasterizedPage page,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (page is not SkiaRasterizedPage skiaPage)
    {
      throw new ArgumentException("Unsupported rasterized page implementation.", nameof(page));
    }

    BarcodeReader reader = CreateReader();
    Result[] results = reader.DecodeMultiple(skiaPage.Bitmap) ?? [];
    if (results.Length == 0)
    {
      Result? single = reader.Decode(skiaPage.Bitmap);
      if (single is not null)
      {
        results = [single];
      }
    }

    List<BarcodeResult> barcodes = new();
    foreach (Result result in results)
    {
      if (string.IsNullOrWhiteSpace(result.Text))
      {
        continue;
      }

      barcodes.Add(new BarcodeResult
      {
        Symbology = result.BarcodeFormat.ToString(),
        Value = result.Text,
        Bbox = ToBoundingBox(result),
        Confidence = 1.0f,
      });
    }

    return Task.FromResult<IReadOnlyList<BarcodeResult>>(barcodes);
  }

  private static BarcodeReader CreateReader()
    => new()
    {
      AutoRotate = true,
      Options = new ZXing.Common.DecodingOptions
      {
        TryHarder = true,
        PossibleFormats =
        [
          BarcodeFormat.QR_CODE,
          BarcodeFormat.CODE_128,
          BarcodeFormat.CODE_39,
          BarcodeFormat.EAN_13,
          BarcodeFormat.EAN_8,
          BarcodeFormat.DATA_MATRIX,
          BarcodeFormat.PDF_417,
        ],
      },
    };

  private static BoundingBox ToBoundingBox(Result result)
  {
    if (result.ResultPoints is not { Length: > 0 })
    {
      return new BoundingBox();
    }

    float minX = result.ResultPoints.Min(point => point.X);
    float minY = result.ResultPoints.Min(point => point.Y);
    float maxX = result.ResultPoints.Max(point => point.X);
    float maxY = result.ResultPoints.Max(point => point.Y);

    return new BoundingBox
    {
      X = Math.Max(0, (int)Math.Floor(minX)),
      Y = Math.Max(0, (int)Math.Floor(minY)),
      W = Math.Max(1, (int)Math.Ceiling(maxX - minX)),
      H = Math.Max(1, (int)Math.Ceiling(maxY - minY)),
    };
  }
}
