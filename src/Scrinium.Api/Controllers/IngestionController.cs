using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scrinium.Api.Models;
using Scrinium.Api.Services;
using Scrinium.Core.Domain;
using Scrinium.Core.Enums;
using Scrinium.Core.Jobs;
using Scrinium.Core.Ports;
using Scrinium.Core.Telemetry;

namespace Scrinium.Api.Controllers;

[ApiController]
[Route("api/ingestion")]
[Authorize]
public sealed class IngestionController : ControllerBase
{
  private readonly IArchiveRepository _archives;
  private readonly IJobQueue _jobQueue;
  private readonly IIngestionStagingStore _stagingStore;
  private readonly IClientMetadataReader _metadataReader;
  private readonly IIngestTelemetry _telemetry;
  private readonly ILogger<IngestionController> _logger;

  public IngestionController(
    IArchiveRepository archives,
    IJobQueue jobQueue,
    IIngestionStagingStore stagingStore,
    IClientMetadataReader metadataReader,
    IIngestTelemetry telemetry,
    ILogger<IngestionController> logger)
  {
    _archives = archives;
    _jobQueue = jobQueue;
    _stagingStore = stagingStore;
    _metadataReader = metadataReader;
    _telemetry = telemetry;
    _logger = logger;
  }

  [HttpPost]
  [Consumes("multipart/form-data")]
  [ProducesResponseType(typeof(IngestionAcceptedResponse), StatusCodes.Status202Accepted)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<IngestionAcceptedResponse>> PostAsync(
    IFormFile file,
    [FromForm] string[]? tags,
    [FromForm] string? clientReference,
    [FromForm] string? idempotencyKey,
    CancellationToken cancellationToken)
  {
    if (file is null || file.Length == 0)
    {
      return BadRequest(new { error = "A non-empty file is required." });
    }

    Guid uploadedBy = GetUserId();
    string traceId = HttpContext.TraceIdentifier;

    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
      Archive? existing = await _archives.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
      if (existing is not null)
      {
        return Accepted(new IngestionAcceptedResponse
        {
          ArchiveId = existing.Id,
          Status = existing.Status.ToString().ToLowerInvariant(),
          FileName = existing.OriginalFileName,
          EnqueuedAt = existing.UploadedAt,
        });
      }
    }

    Dictionary<string, string> clientMetadata;
    try
    {
      clientMetadata = _metadataReader.Read(Request.Form);
    }
    catch (FormatException ex)
    {
      return BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
      return BadRequest(new { error = ex.Message });
    }

    if (!string.IsNullOrWhiteSpace(clientReference))
    {
      clientMetadata["clientreference"] = clientReference.Trim();
    }

    Guid archiveId = Guid.CreateVersion7();
    DateTimeOffset enqueuedAt = DateTimeOffset.UtcNow;

    Archive archive = new()
    {
      Id = archiveId,
      Status = ArchiveStatus.Uploading,
      OriginalFileName = file.FileName,
      ContentType = string.IsNullOrWhiteSpace(file.ContentType)
        ? "application/octet-stream"
        : file.ContentType,
      ByteSize = file.Length,
      UploadedBy = uploadedBy,
      UploadedAt = enqueuedAt,
      IdempotencyKey = idempotencyKey,
      TraceId = traceId,
      ClientMetadata = clientMetadata,
      SheetCount = 0,
    };

    using (IIngestStep acceptStep = _telemetry.BeginStep(new IngestStepContext(
      archiveId,
      null,
      null,
      "accept_upload",
      "api",
      Environment.MachineName,
      traceId)))
    {
      string stagingPath = await _stagingStore.SaveAsync(file, archiveId, cancellationToken);
      archive.StagingPath = stagingPath;
      archive.Status = ArchiveStatus.Queued;

      await _archives.AddAsync(archive, cancellationToken);
      await _archives.SaveChangesAsync(cancellationToken);

      ArchiveIngestJob job = new()
      {
        ArchiveId = archiveId,
        StagingPath = stagingPath,
        FileName = file.FileName,
        ContentType = archive.ContentType,
        UploadedBy = uploadedBy,
        InitialTags = tags ?? Array.Empty<string>(),
        ClientMetadata = clientMetadata,
        TraceId = traceId,
      };

      await _jobQueue.EnqueueAsync(IngestStreamName.Archives, job, cancellationToken);
      acceptStep.CompleteSuccess();
    }

    _logger.LogInformation(
      "Archive {ArchiveId} ({FileName}) queued for ingestion.",
      archiveId,
      file.FileName);

    return Accepted(new IngestionAcceptedResponse
    {
      ArchiveId = archiveId,
      Status = "queued",
      FileName = file.FileName,
      EnqueuedAt = enqueuedAt,
    });
  }

  private Guid GetUserId()
  {
    string? subject = User.FindFirstValue(ClaimTypes.NameIdentifier)
      ?? User.FindFirstValue("sub");

    return Guid.TryParse(subject, out Guid userId)
      ? userId
      : Guid.Empty;
  }
}
