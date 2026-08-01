using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrinium.Core.Domain;
using Scrinium.Core.Enums;
using Scrinium.Core.Extraction;
using Scrinium.Core.Jobs;
using Scrinium.Core.Ports;
using Scrinium.Core.Storage;
using Scrinium.Core.Telemetry;
using Scrinium.Infrastructure.Persistence;
using Scrinium.Workers.Options;

namespace Scrinium.Workers.Ingestion;

public sealed class ArchiveIngestionWorker : BackgroundService
{
  private const string ConsumerGroup = "archive-workers";

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IJobQueue _jobQueue;
  private readonly WorkerOptions _options;
  private readonly ILogger<ArchiveIngestionWorker> _logger;

  public ArchiveIngestionWorker(
    IServiceScopeFactory scopeFactory,
    IJobQueue jobQueue,
    IOptions<WorkerOptions> options,
    ILogger<ArchiveIngestionWorker> logger)
  {
    _scopeFactory = scopeFactory;
    _jobQueue = jobQueue;
    _options = options.Value;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await _jobQueue.EnsureConsumerGroupAsync(
      IngestStreamName.Archives,
      ConsumerGroup,
      stoppingToken);

    _logger.LogInformation("Archive ingestion worker started as {ConsumerName}.", _options.ConsumerName);

    while (!stoppingToken.IsCancellationRequested)
    {
      QueueMessage<ArchiveIngestJob>? message = await _jobQueue.DequeueAsync<ArchiveIngestJob>(
        IngestStreamName.Archives,
        ConsumerGroup,
        _options.ConsumerName,
        stoppingToken);

      if (message is null)
      {
        await Task.Delay(_options.PollIntervalMs, stoppingToken);
        continue;
      }

      await IngestionJobRunner.RunAsync(
        message,
        IngestStreamName.Archives,
        ConsumerGroup,
        _jobQueue,
        _options,
        _logger,
        ProcessAsync,
        MarkArchiveErrorAsync,
        stoppingToken);
    }
  }

  private async Task ProcessAsync(ArchiveIngestJob job, CancellationToken cancellationToken)
  {
    using IServiceScope scope = _scopeFactory.CreateScope();
    ScriniumDbContext dbContext = scope.ServiceProvider.GetRequiredService<ScriniumDbContext>();
    IArchiveRepository archives = scope.ServiceProvider.GetRequiredService<IArchiveRepository>();
    IBundleRepository bundles = scope.ServiceProvider.GetRequiredService<IBundleRepository>();
    IBlobStore blobStore = scope.ServiceProvider.GetRequiredService<IBlobStore>();
    IDocumentNormalizer normalizer = scope.ServiceProvider.GetRequiredService<IDocumentNormalizer>();
    IDocumentExtractor extractor = scope.ServiceProvider.GetRequiredService<IDocumentExtractor>();
    IFormatRouter formatRouter = scope.ServiceProvider.GetRequiredService<IFormatRouter>();
    ITagService tags = scope.ServiceProvider.GetRequiredService<ITagService>();
    IIngestTelemetry telemetry = scope.ServiceProvider.GetRequiredService<IIngestTelemetry>();

    Archive? archive = await archives.GetByIdAsync(job.ArchiveId, cancellationToken);
    if (archive is null)
    {
      throw new InvalidOperationException($"Archive {job.ArchiveId} was not found.");
    }

    if (archive.Status is ArchiveStatus.Ready or ArchiveStatus.Error)
    {
      _logger.LogInformation(
        "Archive {ArchiveId} is already {Status}; skipping replay.",
        job.ArchiveId,
        archive.Status);
      return;
    }

    string originalKey = BlobKeys.Original(job.ArchiveId, job.FileName);
    DocumentFormatKind formatKind = formatRouter.Classify(job.ContentType);

    if (archive.Status is ArchiveStatus.Rendering)
    {
      await FanOutSheetsAsync(job, dbContext, archives, bundles, archive, formatKind, cancellationToken);
      CleanupStagingFile(job);
      return;
    }

    byte[] originalBytes = await LoadOriginalBytesAsync(job, blobStore, originalKey, cancellationToken);

    if (!await blobStore.ExistsAsync(originalKey, cancellationToken))
    {
      await IngestStepRunner.RunAsync(
        telemetry,
        CreateArchiveStepContext(job, ArchiveIngestSteps.StoreOriginal),
        async () =>
        {
          await using MemoryStream stagingStream = new(originalBytes);
          await blobStore.PutAsync(originalKey, stagingStream, cancellationToken);
          archive.Status = ArchiveStatus.Extracting;
          archive.ProcessingStep = ArchiveIngestSteps.StoreOriginal;
          archive.IngestStartedAt ??= DateTimeOffset.UtcNow;
          await archives.UpdateAsync(archive, cancellationToken);
          await archives.SaveChangesAsync(cancellationToken);
        });
    }
    else
    {
      archive.Status = ArchiveStatus.Extracting;
      archive.IngestStartedAt ??= DateTimeOffset.UtcNow;
    }

    NormalizeResult normalizeResult = await IngestStepRunner.RunAsync(
      telemetry,
      CreateArchiveStepContext(job, ArchiveIngestSteps.Normalize),
      () => NormalizeAsync(job, blobStore, normalizer, originalBytes, cancellationToken));

    if (!ShouldSkipExtraction(archive))
    {
      archive.Status = ArchiveStatus.Extracting;
      await archives.UpdateAsync(archive, cancellationToken);
      await archives.SaveChangesAsync(cancellationToken);

      Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);
      Dictionary<string, object?> warnings = new();
      int sheetCount = 1;

      if (normalizeResult.PdfBytes.Length > 0)
      {
        Dictionary<string, string> pdfMetadata = await IngestStepRunner.RunAsync(
          telemetry,
          CreateArchiveStepContext(job, ArchiveIngestSteps.ExtractPdfMetadata),
          () => extractor.ExtractPdfMetadataAsync(normalizeResult.PdfBytes, cancellationToken));

        foreach (KeyValuePair<string, string> entry in pdfMetadata)
        {
          metadata[entry.Key] = entry.Value;
        }

        sheetCount = await IngestStepRunner.RunAsync(
          telemetry,
          CreateArchiveStepContext(job, ArchiveIngestSteps.ResolvePageCount),
          () => Task.FromResult(extractor.GetPdfPageCount(normalizeResult.PdfBytes)));

        Dictionary<string, string> tikaMetadata = await IngestStepRunner.RunAsync(
          telemetry,
          CreateArchiveStepContext(job, ArchiveIngestSteps.ExtractMetadata),
          () => extractor.ExtractTikaMetadataAsync(originalBytes, job.ContentType, cancellationToken));

        foreach (KeyValuePair<string, string> entry in tikaMetadata)
        {
          metadata.TryAdd(entry.Key, entry.Value);
        }
      }
      else if (formatKind is DocumentFormatKind.Image)
      {
        sheetCount = 1;
        metadata = await IngestStepRunner.RunAsync(
          telemetry,
          CreateArchiveStepContext(job, ArchiveIngestSteps.ExtractMetadata),
          () => extractor.ExtractTikaMetadataAsync(originalBytes, job.ContentType, cancellationToken));
      }
      else
      {
        sheetCount = 1;
        metadata = await IngestStepRunner.RunAsync(
          telemetry,
          CreateArchiveStepContext(job, ArchiveIngestSteps.ExtractMetadata),
          () => extractor.ExtractTikaMetadataAsync(originalBytes, job.ContentType, cancellationToken));

        // Office/web text is deferred to sheet processing / OCR; metadata only at archive level for now.
        _ = await IngestStepRunner.RunAsync(
          telemetry,
          CreateArchiveStepContext(job, ArchiveIngestSteps.ExtractTikaText),
          () => extractor.ExtractTikaTextAsync(originalBytes, job.ContentType, cancellationToken));
      }

      archive.ExtractedMetadata = metadata;
      archive.ExtractionWarnings = warnings;
      archive.SheetCount = Math.Max(sheetCount, 1);
      archive.ProcessingStep = ArchiveIngestSteps.ExtractionComplete;
      await archives.UpdateAsync(archive, cancellationToken);
      await archives.SaveChangesAsync(cancellationToken);
    }

    await FanOutSheetsAsync(job, dbContext, archives, bundles, archive, formatKind, cancellationToken);

    Bundle? defaultBundle = await dbContext.Bundles
      .FirstOrDefaultAsync(
        x => x.SourceArchiveId == archive.Id && x.IsDefault,
        cancellationToken);

    if (defaultBundle is not null && job.InitialTags.Count > 0)
    {
      await tags.ApplyTagsAsync(
        defaultBundle.Id,
        job.InitialTags,
        TagSource.Upload,
        job.UploadedBy,
        cancellationToken);
    }

    CleanupStagingFile(job);
  }

  private async Task FanOutSheetsAsync(
    ArchiveIngestJob job,
    ScriniumDbContext dbContext,
    IArchiveRepository archives,
    IBundleRepository bundles,
    Archive archive,
    DocumentFormatKind formatKind,
    CancellationToken cancellationToken)
  {
    using IServiceScope scope = _scopeFactory.CreateScope();
    IIngestTelemetry telemetry = scope.ServiceProvider.GetRequiredService<IIngestTelemetry>();
    IBlobStore blobStore = scope.ServiceProvider.GetRequiredService<IBlobStore>();
    IDocumentExtractor extractor = scope.ServiceProvider.GetRequiredService<IDocumentExtractor>();

    await IngestStepRunner.RunAsync(
      telemetry,
      CreateArchiveStepContext(job, ArchiveIngestSteps.FanOutSheets),
      async () =>
      {
        archive.Status = ArchiveStatus.Rendering;
        archive.ProcessingStep = ArchiveIngestSteps.FanOutSheets;

        await EnsureSheetCountAsync(job, archives, archive, blobStore, formatKind, extractor, cancellationToken);

        Bundle? defaultBundle = await dbContext.Bundles
          .Include(x => x.Sheets)
          .FirstOrDefaultAsync(
            x => x.SourceArchiveId == archive.Id && x.IsDefault,
            cancellationToken);

        if (defaultBundle is null)
        {
          defaultBundle = new Bundle
          {
            Id = Guid.CreateVersion7(),
            Title = archive.OriginalFileName,
            Status = BundleStatus.Processing,
            IsDefault = true,
            SourceArchiveId = archive.Id,
            CreatedBy = archive.UploadedBy,
            CreatedAt = DateTimeOffset.UtcNow,
            TraceId = job.TraceId,
          };
          await bundles.AddAsync(defaultBundle, cancellationToken);
          await bundles.SaveChangesAsync(cancellationToken);
        }

        HashSet<int> existingSequences = (await dbContext.ArchiveSheets
          .Where(x => x.ArchiveId == archive.Id)
          .Select(x => x.SequenceInArchive)
          .ToListAsync(cancellationToken)).ToHashSet();

        List<ArchiveSheet> newSheets = new();
        for (int sequence = 1; sequence <= archive.SheetCount; sequence++)
        {
          if (existingSequences.Contains(sequence))
          {
            continue;
          }

          ArchiveSheet archiveSheet = new()
          {
            Id = Guid.CreateVersion7(),
            ArchiveId = archive.Id,
            SequenceInArchive = sequence,
            SourceKind = formatKind is DocumentFormatKind.Image
              ? PageSourceKind.Image
              : PageSourceKind.PdfPage,
            ProcessingStatus = PageProcessingStatus.Pending,
            RenderStatus = PageProcessingStatus.Pending,
          };
          newSheets.Add(archiveSheet);
          dbContext.ArchiveSheets.Add(archiveSheet);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        HashSet<Guid> existingMemberships = defaultBundle.Sheets
          .Select(x => x.ArchiveSheetId)
          .ToHashSet();

        List<ArchiveSheet> allSheets = await dbContext.ArchiveSheets
          .Where(x => x.ArchiveId == archive.Id)
          .OrderBy(x => x.SequenceInArchive)
          .ToListAsync(cancellationToken);

        foreach (ArchiveSheet archiveSheet in allSheets)
        {
          if (existingMemberships.Contains(archiveSheet.Id))
          {
            continue;
          }

          dbContext.Sheets.Add(new Sheet
          {
            BundleId = defaultBundle.Id,
            ArchiveSheetId = archiveSheet.Id,
            SortOrder = archiveSheet.SequenceInArchive,
          });
        }

        await archives.UpdateAsync(archive, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (ArchiveSheet archiveSheet in allSheets)
        {
          if (archiveSheet.RenderStatus is PageProcessingStatus.Ready or PageProcessingStatus.Error)
          {
            continue;
          }

          await _jobQueue.EnqueueAsync(
            IngestStreamName.Sheets,
            new ArchiveSheetIngestJob
            {
              ArchiveId = archive.Id,
              ArchiveSheetId = archiveSheet.Id,
              BundleId = defaultBundle.Id,
              SequenceInArchive = archiveSheet.SequenceInArchive,
              TraceId = job.TraceId,
            },
            cancellationToken);
        }

        if (allSheets.Count == 0
          || allSheets.All(x => x.RenderStatus is PageProcessingStatus.Ready or PageProcessingStatus.Error))
        {
          await MaybeEnqueueFinalizeAsync(defaultBundle.Id, archive.Id, job.TraceId, bundles, cancellationToken);
        }
      });
  }

  private async Task EnsureSheetCountAsync(
    ArchiveIngestJob job,
    IArchiveRepository archives,
    Archive archive,
    IBlobStore blobStore,
    DocumentFormatKind formatKind,
    IDocumentExtractor extractor,
    CancellationToken cancellationToken)
  {
    if (archive.SheetCount > 1 || formatKind is DocumentFormatKind.Image)
    {
      return;
    }

    string pdfKey = BlobKeys.NormalizedPdf(job.ArchiveId);
    string originalKey = BlobKeys.Original(job.ArchiveId, job.FileName);
    string? sourceKey = null;

    if (await blobStore.ExistsAsync(pdfKey, cancellationToken))
    {
      sourceKey = pdfKey;
    }
    else if (formatKind is DocumentFormatKind.Pdf && await blobStore.ExistsAsync(originalKey, cancellationToken))
    {
      sourceKey = originalKey;
    }

    if (sourceKey is null)
    {
      return;
    }

    await using Stream pdfStream = await blobStore.GetAsync(sourceKey, cancellationToken);
    byte[] pdfBytes = await ReadAllBytesAsync(pdfStream, cancellationToken);
    int sheetCount = extractor.GetPdfPageCount(pdfBytes);
    if (sheetCount <= archive.SheetCount)
    {
      return;
    }

    archive.SheetCount = sheetCount;
    await archives.UpdateAsync(archive, cancellationToken);
    await archives.SaveChangesAsync(cancellationToken);
  }

  private async Task MaybeEnqueueFinalizeAsync(
    Guid bundleId,
    Guid archiveId,
    string traceId,
    IBundleRepository bundles,
    CancellationToken cancellationToken)
  {
    int pending = await bundles.CountPendingMemberSheetsAsync(bundleId, cancellationToken);
    if (pending > 0)
    {
      return;
    }

    if (await bundles.TryMarkFinalizeEnqueuedAsync(bundleId, cancellationToken))
    {
      await _jobQueue.EnqueueAsync(
        IngestStreamName.Finalize,
        new FinalizeBundleJob
        {
          BundleId = bundleId,
          ArchiveId = archiveId,
          TraceId = traceId,
        },
        cancellationToken);
    }
  }

  private async Task<NormalizeResult> NormalizeAsync(
    ArchiveIngestJob job,
    IBlobStore blobStore,
    IDocumentNormalizer normalizer,
    byte[] originalBytes,
    CancellationToken cancellationToken)
  {
    string pdfKey = BlobKeys.NormalizedPdf(job.ArchiveId);
    if (await blobStore.ExistsAsync(pdfKey, cancellationToken))
    {
      await using Stream pdfStream = await blobStore.GetAsync(pdfKey, cancellationToken);
      return new NormalizeResult
      {
        PdfBytes = await ReadAllBytesAsync(pdfStream, cancellationToken),
        WasConverted = true,
        ContentType = "application/pdf",
      };
    }

    NormalizeResult normalizeResult = await normalizer.NormalizeAsync(
      originalBytes,
      job.ContentType,
      job.FileName,
      cancellationToken);

    if (normalizeResult.PdfBytes.Length > 0)
    {
      await using MemoryStream pdfStream = new(normalizeResult.PdfBytes);
      await blobStore.PutAsync(pdfKey, pdfStream, cancellationToken);
    }

    return normalizeResult;
  }

  private static bool ShouldSkipExtraction(Archive archive)
  {
    if (archive.ProcessingStep is ArchiveIngestSteps.FanOutSheets or "ready")
    {
      return true;
    }

    if (archive.Status is ArchiveStatus.Rendering or ArchiveStatus.Ready)
    {
      return true;
    }

    return archive.SheetCount > 1
      || archive.ProcessingStep is ArchiveIngestSteps.ExtractionComplete;
  }

  private IngestStepContext CreateArchiveStepContext(ArchiveIngestJob job, string stepName)
    => new(
      job.ArchiveId,
      null,
      null,
      stepName,
      "archive",
      _options.ConsumerName,
      job.TraceId);

  private static async Task<byte[]> LoadOriginalBytesAsync(
    ArchiveIngestJob job,
    IBlobStore blobStore,
    string originalKey,
    CancellationToken cancellationToken)
  {
    if (File.Exists(job.StagingPath))
    {
      return await File.ReadAllBytesAsync(job.StagingPath, cancellationToken);
    }

    if (await blobStore.ExistsAsync(originalKey, cancellationToken))
    {
      await using Stream stream = await blobStore.GetAsync(originalKey, cancellationToken);
      return await ReadAllBytesAsync(stream, cancellationToken);
    }

    throw new InvalidOperationException(
      $"Staging file and original blob are both missing for archive {job.ArchiveId}.");
  }

  private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
  {
    using MemoryStream memoryStream = new();
    await stream.CopyToAsync(memoryStream, cancellationToken);
    return memoryStream.ToArray();
  }

  private static void CleanupStagingFile(ArchiveIngestJob job)
  {
    if (File.Exists(job.StagingPath))
    {
      File.Delete(job.StagingPath);
    }
  }

  private async Task MarkArchiveErrorAsync(
    ArchiveIngestJob job,
    string error,
    CancellationToken cancellationToken)
  {
    try
    {
      using IServiceScope scope = _scopeFactory.CreateScope();
      IArchiveRepository archives = scope.ServiceProvider.GetRequiredService<IArchiveRepository>();
      Archive? archive = await archives.GetByIdAsync(job.ArchiveId, cancellationToken);
      if (archive is null)
      {
        return;
      }

      archive.Status = ArchiveStatus.Error;
      archive.LastError = error;
      archive.ProcessingStep = "error";
      await archives.UpdateAsync(archive, cancellationToken);
      await archives.SaveChangesAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to mark archive {ArchiveId} as error.", job.ArchiveId);
    }
  }
}
