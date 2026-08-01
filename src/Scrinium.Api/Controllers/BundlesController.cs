using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scrinium.Api.Models;
using Scrinium.Core.Domain;
using Scrinium.Core.Enums;
using Scrinium.Core.Ports;
using Scrinium.Core.Rendering;
using Scrinium.Core.Storage;

namespace Scrinium.Api.Controllers;

[ApiController]
[Route("api/bundles")]
[Authorize]
public sealed class BundlesController : ControllerBase
{
  private readonly IBundleRepository _bundles;
  private readonly IBlobStore _blobStore;

  public BundlesController(IBundleRepository bundles, IBlobStore blobStore)
  {
    _bundles = bundles;
    _blobStore = blobStore;
  }

  [HttpGet]
  [ProducesResponseType(typeof(BundleListResponse), StatusCodes.Status200OK)]
  public async Task<ActionResult<BundleListResponse>> ListAsync(
    [FromQuery] string? status,
    [FromQuery] int skip = 0,
    [FromQuery] int take = 50,
    CancellationToken cancellationToken = default)
  {
    BundleStatus? statusFilter = null;
    if (!string.IsNullOrWhiteSpace(status)
      && Enum.TryParse(status, ignoreCase: true, out BundleStatus parsedStatus))
    {
      statusFilter = parsedStatus;
    }

    skip = Math.Max(skip, 0);
    take = Math.Clamp(take, 1, 100);

    BundleListResult result = await _bundles.ListReadyAsync(
      new BundleListQuery
      {
        Status = statusFilter,
        Skip = skip,
        Take = take,
      },
      cancellationToken);

    return Ok(new BundleListResponse
    {
      Items = result.Items.Select(MapSummary).ToArray(),
      TotalCount = result.TotalCount,
      Skip = skip,
      Take = take,
    });
  }

  [HttpGet("{id:guid}")]
  [ProducesResponseType(typeof(BundleDetailResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<BundleDetailResponse>> GetAsync(
    Guid id,
    CancellationToken cancellationToken)
  {
    Bundle? bundle = await _bundles.GetByIdAsync(id, cancellationToken);
    if (bundle is null || bundle.Status != BundleStatus.Ready)
    {
      return NotFound();
    }

    return Ok(MapDetail(bundle));
  }

  [HttpGet("{id:guid}/status")]
  [ProducesResponseType(typeof(BundleDetailResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<BundleDetailResponse>> GetStatusAsync(
    Guid id,
    CancellationToken cancellationToken)
  {
    Bundle? bundle = await _bundles.GetByIdAsync(id, cancellationToken);
    if (bundle is null)
    {
      return NotFound();
    }

    return Ok(MapDetail(bundle));
  }

  [HttpGet("by-archive/{archiveId:guid}")]
  [ProducesResponseType(typeof(BundleDetailResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<BundleDetailResponse>> GetDefaultByArchiveAsync(
    Guid archiveId,
    CancellationToken cancellationToken)
  {
    Bundle? bundle = await _bundles.GetDefaultByArchiveIdAsync(archiveId, cancellationToken);
    if (bundle is null)
    {
      return NotFound();
    }

    return Ok(MapDetail(bundle));
  }

  [HttpGet("{id:guid}/sheets/{archiveSheetId:guid}/render")]
  [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetSheetRenderAsync(
    Guid id,
    Guid archiveSheetId,
    [FromQuery] string tier = "preview",
    CancellationToken cancellationToken = default)
  {
    Bundle? bundle = await _bundles.GetByIdAsync(id, cancellationToken);
    if (bundle is null || bundle.Status != BundleStatus.Ready)
    {
      return NotFound();
    }

    Sheet? membership = bundle.Sheets.FirstOrDefault(x => x.ArchiveSheetId == archiveSheetId);
    if (membership is null
      || membership.ArchiveSheet.RenderStatus != PageProcessingStatus.Ready
      || bundle.SourceArchiveId is null)
    {
      return NotFound();
    }

    if (!Enum.TryParse(tier, ignoreCase: true, out RenderTier renderTier))
    {
      return BadRequest("Invalid tier. Use thumb, preview, or full.");
    }

    string objectKey = BlobKeys.SheetRender(
      bundle.SourceArchiveId.Value,
      archiveSheetId,
      renderTier,
      membership.ArchiveSheet.SequenceInArchive);

    if (!await _blobStore.ExistsAsync(objectKey, cancellationToken))
    {
      return NotFound();
    }

    Stream stream = await _blobStore.GetAsync(objectKey, cancellationToken);
    return File(stream, "image/webp");
  }

  private static BundleSummaryResponse MapSummary(Bundle bundle)
  {
    return new BundleSummaryResponse
    {
      BundleId = bundle.Id,
      ArchiveId = bundle.SourceArchiveId,
      Title = bundle.Title,
      Status = bundle.Status.ToString().ToLowerInvariant(),
      IngestQuality = bundle.IngestQuality?.ToString().ToLowerInvariant(),
      SheetCount = bundle.Sheets.Count,
      SheetsFailedCount = bundle.SheetsFailedCount,
      IsDefault = bundle.IsDefault,
      CreatedAt = bundle.CreatedAt,
      ReadyAt = bundle.ReadyAt,
      Tags = bundle.BundleTags.Select(x => x.Tag.Name).ToArray(),
    };
  }

  private static BundleDetailResponse MapDetail(Bundle bundle)
  {
    return new BundleDetailResponse
    {
      BundleId = bundle.Id,
      ArchiveId = bundle.SourceArchiveId,
      Title = bundle.Title,
      Status = bundle.Status.ToString().ToLowerInvariant(),
      IngestQuality = bundle.IngestQuality?.ToString().ToLowerInvariant(),
      SheetCount = bundle.Sheets.Count,
      SheetsFailedCount = bundle.SheetsFailedCount,
      IsDefault = bundle.IsDefault,
      CreatedAt = bundle.CreatedAt,
      ReadyAt = bundle.ReadyAt,
      Tags = bundle.BundleTags.Select(x => x.Tag.Name).ToArray(),
      Sheets = bundle.Sheets
        .OrderBy(x => x.SortOrder)
        .Select(x => new BundleSheetResponse
        {
          ArchiveSheetId = x.ArchiveSheetId,
          SortOrder = x.SortOrder,
          SequenceInArchive = x.ArchiveSheet.SequenceInArchive,
          ProcessingStatus = x.ArchiveSheet.ProcessingStatus.ToString().ToLowerInvariant(),
          RenderStatus = x.ArchiveSheet.RenderStatus.ToString().ToLowerInvariant(),
          HasTextLayer = x.ArchiveSheet.HasTextLayer,
        })
        .ToArray(),
    };
  }
}
