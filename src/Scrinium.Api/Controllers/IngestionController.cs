using System;
using System.Collections.Generic;
using System.Linq;
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
  private readonly IDocumentRepository _documents;
  private readonly ITagService _tags;
  private readonly IJobQueue _jobQueue;
  private readonly IIngestionStagingStore _stagingStore;
  private readonly IClientMetadataReader _metadataReader;
  private readonly IIngestTelemetry _telemetry;
  private readonly ILogger<IngestionController> _logger;

  public IngestionController(
    IDocumentRepository documents,
    ITagService tags,
    IJobQueue jobQueue,
    IIngestionStagingStore stagingStore,
    IClientMetadataReader metadataReader,
    IIngestTelemetry telemetry,
    ILogger<IngestionController> logger)
  {
    _documents = documents;
    _tags = tags;
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
      Document? existing = await _documents.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
      if (existing is not null)
      {
        return Accepted(new IngestionAcceptedResponse
        {
          DocumentId = existing.Id,
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

    Guid documentId = Guid.CreateVersion7();
    DateTimeOffset enqueuedAt = DateTimeOffset.UtcNow;

    Document document = new()
    {
      Id = documentId,
      Status = DocumentStatus.Uploading,
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
      PageCount = 1,
    };

    using (IIngestStep acceptStep = _telemetry.BeginStep(new IngestStepContext(
      documentId,
      null,
      "accept_upload",
      "api",
      Environment.MachineName,
      traceId)))
    {
      string stagingPath = await _stagingStore.SaveAsync(file, documentId, cancellationToken);
      document.StagingPath = stagingPath;
      document.Status = DocumentStatus.Queued;

      await _documents.AddAsync(document, cancellationToken);
      await _documents.SaveChangesAsync(cancellationToken);

      if (tags is { Length: > 0 })
      {
        await _tags.ApplyTagsAsync(documentId, tags, TagSource.Upload, uploadedBy, cancellationToken);
      }

      DocumentIngestJob job = new()
      {
        DocumentId = documentId,
        StagingPath = stagingPath,
        FileName = file.FileName,
        ContentType = document.ContentType,
        UploadedBy = uploadedBy,
        InitialTags = tags ?? Array.Empty<string>(),
        ClientMetadata = clientMetadata,
        TraceId = traceId,
      };

      await _jobQueue.EnqueueAsync(IngestStreamName.Documents, job, cancellationToken);
      acceptStep.CompleteSuccess();
    }

    _logger.LogInformation(
      "Document {DocumentId} ({FileName}) queued for ingestion.",
      documentId,
      file.FileName);

    return Accepted(new IngestionAcceptedResponse
    {
      DocumentId = documentId,
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
