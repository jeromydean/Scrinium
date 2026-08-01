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
using Scrinium.Core.Rendering;
using Scrinium.Core.Storage;
using Scrinium.Core.Telemetry;
using Scrinium.Infrastructure.Persistence;
using Scrinium.Workers.Options;

namespace Scrinium.Workers.Ingestion;

public sealed class ArchiveSheetIngestionWorker : BackgroundService
{
  private const string ConsumerGroup = "sheet-workers";

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IJobQueue _jobQueue;
  private readonly WorkerOptions _options;
  private readonly ILogger<ArchiveSheetIngestionWorker> _logger;

  public ArchiveSheetIngestionWorker(
    IServiceScopeFactory scopeFactory,
    IJobQueue jobQueue,
    IOptions<WorkerOptions> options,
    ILogger<ArchiveSheetIngestionWorker> logger)
  {
    _scopeFactory = scopeFactory;
    _jobQueue = jobQueue;
    _options = options.Value;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await _jobQueue.EnsureConsumerGroupAsync(
      IngestStreamName.Sheets,
      ConsumerGroup,
      stoppingToken);

    _logger.LogInformation("Archive sheet ingestion worker started as {ConsumerName}.", _options.ConsumerName);

    while (!stoppingToken.IsCancellationRequested)
    {
      QueueMessage<ArchiveSheetIngestJob>? message = await _jobQueue.DequeueAsync<ArchiveSheetIngestJob>(
        IngestStreamName.Sheets,
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
        IngestStreamName.Sheets,
        ConsumerGroup,
        _jobQueue,
        _options,
        _logger,
        ProcessAsync,
        giveUpAsync: null,
        stoppingToken);
    }
  }

  private async Task ProcessAsync(ArchiveSheetIngestJob job, CancellationToken cancellationToken)
  {
    using IServiceScope scope = _scopeFactory.CreateScope();
    ScriniumDbContext dbContext = scope.ServiceProvider.GetRequiredService<ScriniumDbContext>();
    IArchiveRepository archives = scope.ServiceProvider.GetRequiredService<IArchiveRepository>();
    IBundleRepository bundles = scope.ServiceProvider.GetRequiredService<IBundleRepository>();
    IBlobStore blobStore = scope.ServiceProvider.GetRequiredService<IBlobStore>();
    IPageRenderer pageRenderer = scope.ServiceProvider.GetRequiredService<IPageRenderer>();
    IBarcodeScanner barcodeScanner = scope.ServiceProvider.GetRequiredService<IBarcodeScanner>();
    IFormatRouter formatRouter = scope.ServiceProvider.GetRequiredService<IFormatRouter>();
    IIngestTelemetry telemetry = scope.ServiceProvider.GetRequiredService<IIngestTelemetry>();

    ArchiveSheet? sheet = await dbContext.ArchiveSheets
      .FirstOrDefaultAsync(x => x.Id == job.ArchiveSheetId, cancellationToken);

    if (sheet is null)
    {
      throw new InvalidOperationException($"Archive sheet {job.ArchiveSheetId} was not found.");
    }

    if (sheet.RenderStatus is PageProcessingStatus.Ready)
    {
      return;
    }

    if (sheet.RenderStatus is PageProcessingStatus.Error)
    {
      sheet.ProcessingStatus = PageProcessingStatus.Pending;
      sheet.RenderStatus = PageProcessingStatus.Pending;
      sheet.LastError = null;
    }

    Archive? archive = await archives.GetByIdAsync(job.ArchiveId, cancellationToken);
    if (archive is null)
    {
      throw new InvalidOperationException($"Archive {job.ArchiveId} was not found.");
    }

    try
    {
      string pdfKey = BlobKeys.NormalizedPdf(job.ArchiveId);
      string originalKey = BlobKeys.Original(job.ArchiveId, archive.OriginalFileName);
      DocumentFormatKind formatKind = formatRouter.Classify(archive.ContentType);

      byte[] sourceBytes = await IngestStepRunner.RunAsync(
        telemetry,
        CreateSheetStepContext(job, ArchiveSheetIngestSteps.LoadSource),
        async () =>
        {
          if (formatKind is DocumentFormatKind.Image)
          {
            await using Stream imageStream = await blobStore.GetAsync(originalKey, cancellationToken);
            return await ReadAllBytesAsync(imageStream, cancellationToken);
          }

          string sourceKey = await blobStore.ExistsAsync(pdfKey, cancellationToken)
            ? pdfKey
            : originalKey;

          await using Stream pdfStream = await blobStore.GetAsync(sourceKey, cancellationToken);
          return await ReadAllBytesAsync(pdfStream, cancellationToken);
        });

      if (formatKind is DocumentFormatKind.Image)
      {
        await using IRasterizedPage rasterizedPage = await IngestStepRunner.RunAsync(
          telemetry,
          CreateSheetStepContext(job, ArchiveSheetIngestSteps.DecodeImage),
          () => pageRenderer.DecodeImageAsync(sourceBytes, cancellationToken));

        await SaveBarcodesAsync(
          dbContext,
          job,
          await IngestStepRunner.RunAsync(
            telemetry,
            CreateSheetStepContext(job, ArchiveSheetIngestSteps.ScanBarcodes),
            () => barcodeScanner.ScanAsync(rasterizedPage, cancellationToken)),
          cancellationToken);

        _ = await IngestStepRunner.RunAsync(
          telemetry,
          CreateSheetStepContext(job, ArchiveSheetIngestSteps.UploadRenders),
          () => pageRenderer.UploadRenderTiersAsync(
            rasterizedPage,
            job.ArchiveId,
            job.ArchiveSheetId,
            job.SequenceInArchive,
            cancellationToken));
      }
      else
      {
        await using IRasterizedPage rasterizedPage = await IngestStepRunner.RunAsync(
          telemetry,
          CreateSheetStepContext(job, ArchiveSheetIngestSteps.Rasterize),
          () => pageRenderer.RasterizePdfPageAsync(sourceBytes, job.SequenceInArchive, cancellationToken));

        await SaveBarcodesAsync(
          dbContext,
          job,
          await IngestStepRunner.RunAsync(
            telemetry,
            CreateSheetStepContext(job, ArchiveSheetIngestSteps.ScanBarcodes),
            () => barcodeScanner.ScanAsync(rasterizedPage, cancellationToken)),
          cancellationToken);

        _ = await IngestStepRunner.RunAsync(
          telemetry,
          CreateSheetStepContext(job, ArchiveSheetIngestSteps.UploadRenders),
          () => pageRenderer.UploadRenderTiersAsync(
            rasterizedPage,
            job.ArchiveId,
            job.ArchiveSheetId,
            job.SequenceInArchive,
            cancellationToken));

        sheet.PlainText = await IngestStepRunner.RunAsync(
          telemetry,
          CreateSheetStepContext(job, ArchiveSheetIngestSteps.ExtractText),
          () => Task.FromResult(pageRenderer.ExtractPdfPageText(sourceBytes, job.SequenceInArchive)));

        sheet.HasTextLayer = !string.IsNullOrWhiteSpace(sheet.PlainText);
      }

      sheet.ProcessingStatus = PageProcessingStatus.Ready;
      sheet.RenderStatus = PageProcessingStatus.Ready;
      await dbContext.SaveChangesAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      sheet.ProcessingStatus = PageProcessingStatus.Error;
      sheet.RenderStatus = PageProcessingStatus.Error;
      sheet.LastError = ex.Message;
      await dbContext.SaveChangesAsync(cancellationToken);
      throw;
    }

    int pending = await bundles.CountPendingMemberSheetsAsync(job.BundleId, cancellationToken);
    if (pending > 0)
    {
      return;
    }

    if (await bundles.TryMarkFinalizeEnqueuedAsync(job.BundleId, cancellationToken))
    {
      await _jobQueue.EnqueueAsync(
        IngestStreamName.Finalize,
        new FinalizeBundleJob
        {
          BundleId = job.BundleId,
          ArchiveId = job.ArchiveId,
          TraceId = job.TraceId,
        },
        cancellationToken);
    }
  }

  private static async Task SaveBarcodesAsync(
    ScriniumDbContext dbContext,
    ArchiveSheetIngestJob job,
    IReadOnlyList<BarcodeResult> barcodes,
    CancellationToken cancellationToken)
  {
    List<SheetBarcode> existing = await dbContext.SheetBarcodes
      .Where(x => x.ArchiveSheetId == job.ArchiveSheetId)
      .ToListAsync(cancellationToken);

    if (existing.Count > 0)
    {
      dbContext.SheetBarcodes.RemoveRange(existing);
    }

    foreach (BarcodeResult barcode in barcodes)
    {
      dbContext.SheetBarcodes.Add(new SheetBarcode
      {
        Id = Guid.CreateVersion7(),
        ArchiveSheetId = job.ArchiveSheetId,
        Symbology = barcode.Symbology,
        Value = barcode.Value,
        BoundingBox = barcode.Bbox,
        Confidence = barcode.Confidence,
      });
    }

    if (existing.Count > 0 || barcodes.Count > 0)
    {
      await dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  private IngestStepContext CreateSheetStepContext(ArchiveSheetIngestJob job, string stepName)
    => new(
      job.ArchiveId,
      job.ArchiveSheetId,
      job.BundleId,
      stepName,
      "sheet",
      _options.ConsumerName,
      job.TraceId);

  private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
  {
    using MemoryStream memoryStream = new();
    await stream.CopyToAsync(memoryStream, cancellationToken);
    return memoryStream.ToArray();
  }
}
