using Microsoft.AspNetCore.Mvc;
using Scrinium.Api.Models;
using Scrinium.Api.Services;

namespace Scrinium.Api.Controllers
{
  [ApiController]
  [Route("api/ingestion")]
  public class IngestionController : ControllerBase
  {
    private readonly IIngestionQueue _ingestionQueue;
    private readonly IIngestionStagingStore _stagingStore;
    private readonly ILogger<IngestionController> _logger;

    public IngestionController(
      IIngestionQueue ingestionQueue,
      IIngestionStagingStore stagingStore,
      ILogger<IngestionController> logger)
    {
      _ingestionQueue = ingestionQueue;
      _stagingStore = stagingStore;
      _logger = logger;
    }

    /// <summary>
    /// Accepts a document upload, stages the file, and enqueues it for background ingestion.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(IngestionAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IngestionAcceptedResponse>> PostAsync(
      IFormFile file,
      CancellationToken cancellationToken)
    {
      if (file is null || file.Length == 0)
      {
        return BadRequest(new { error = "A non-empty file is required." });
      }

      Guid documentId = Guid.NewGuid();
      string stagingPath = await _stagingStore.SaveAsync(file, documentId, cancellationToken);

      IngestionJob job = new()
      {
        DocumentId = documentId,
        FileName = file.FileName,
        ContentType = string.IsNullOrWhiteSpace(file.ContentType)
          ? "application/octet-stream"
          : file.ContentType,
        StagingPath = stagingPath,
        FileSizeBytes = file.Length,
      };

      await _ingestionQueue.EnqueueAsync(job, cancellationToken);

      _logger.LogInformation(
        "Document {DocumentId} ({FileName}) queued for ingestion.",
        documentId,
        file.FileName);

      IngestionAcceptedResponse response = new()
      {
        DocumentId = documentId,
        Status = "queued",
        FileName = file.FileName,
      };

      return Accepted(response);
    }
  }
}
