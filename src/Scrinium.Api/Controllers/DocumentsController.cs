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
[Route("api/documents")]
[Authorize]
public sealed class DocumentsController : ControllerBase
{
  private readonly IDocumentRepository _documents;
  private readonly IBlobStore _blobStore;

  public DocumentsController(IDocumentRepository documents, IBlobStore blobStore)
  {
    _documents = documents;
    _blobStore = blobStore;
  }

  [HttpGet("{id:guid}")]
  [ProducesResponseType(typeof(DocumentStatusResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<DocumentStatusResponse>> GetAsync(
    Guid id,
    CancellationToken cancellationToken)
  {
    Document? document = await _documents.GetByIdAsync(id, cancellationToken);
    if (document is null || document.Status != DocumentStatus.Ready)
    {
      return NotFound();
    }

    return Ok(Map(document));
  }

  [HttpGet("{id:guid}/status")]
  [ProducesResponseType(typeof(DocumentStatusResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<DocumentStatusResponse>> GetStatusAsync(
    Guid id,
    CancellationToken cancellationToken)
  {
    Document? document = await _documents.GetByIdAsync(id, cancellationToken);
    if (document is null)
    {
      return NotFound();
    }

    return Ok(Map(document));
  }

  [HttpGet("{id:guid}/pages/{pageNumber:int}/render")]
  [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetPageRenderAsync(
    Guid id,
    int pageNumber,
    [FromQuery] string tier = "preview",
    CancellationToken cancellationToken = default)
  {
    Document? document = await _documents.GetByIdAsync(id, cancellationToken);
    if (document is null || document.Status != DocumentStatus.Ready)
    {
      return NotFound();
    }

    if (pageNumber < 1 || pageNumber > document.PageCount)
    {
      return NotFound();
    }

    if (!Enum.TryParse(tier, ignoreCase: true, out RenderTier renderTier))
    {
      return BadRequest("Invalid tier. Use thumb, preview, or full.");
    }

    string objectKey = BlobKeys.PageRender(id, renderTier, pageNumber);
    if (!await _blobStore.ExistsAsync(objectKey, cancellationToken))
    {
      return NotFound();
    }

    Stream stream = await _blobStore.GetAsync(objectKey, cancellationToken);
    return File(stream, "image/webp");
  }

  private static DocumentStatusResponse Map(Document document)
  {
    return new DocumentStatusResponse
    {
      DocumentId = document.Id,
      Status = document.Status.ToString().ToLowerInvariant(),
      IngestQuality = document.IngestQuality?.ToString().ToLowerInvariant(),
      PageCount = document.PageCount,
      PagesFailedCount = document.PagesFailedCount,
      ProcessingStep = document.ProcessingStep,
      UploadedAt = document.UploadedAt,
      ReadyAt = document.ReadyAt,
      Tags = document.DocumentTags.Select(x => x.Tag.Name).ToArray(),
      ClientMetadata = document.ClientMetadata,
    };
  }
}
