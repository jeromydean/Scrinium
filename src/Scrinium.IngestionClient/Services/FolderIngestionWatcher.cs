using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrinium.IngestionClient.Api;
using Scrinium.IngestionClient.Options;

namespace Scrinium.IngestionClient.Services;

public sealed class FolderIngestionWatcher : BackgroundService
{
  private readonly IngestionApiClient _apiClient;
  private readonly WatcherOptions _options;
  private readonly ILogger<FolderIngestionWatcher> _logger;
  private readonly IHostApplicationLifetime _lifetime;
  private readonly ConcurrentDictionary<string, byte> _queuedPaths = new(StringComparer.OrdinalIgnoreCase);
  private FileSystemWatcher? _watcher;

  public FolderIngestionWatcher(
    IngestionApiClient apiClient,
    IOptions<WatcherOptions> options,
    ILogger<FolderIngestionWatcher> logger,
    IHostApplicationLifetime lifetime)
  {
    _apiClient = apiClient;
    _options = options.Value;
    _logger = logger;
    _lifetime = lifetime;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    string watchFolder = ResolveWatchFolder();
    EnsureWatchFolders(watchFolder);

    _logger.LogInformation("Watching folder: {WatchFolder}", watchFolder);
    _logger.LogInformation(
      "Drop files here to ingest. Processed -> {ProcessedFolder}, failed -> {FailedFolder}",
      Path.Combine(watchFolder, _options.ProcessedSubfolder),
      Path.Combine(watchFolder, _options.FailedSubfolder));

    await ScanExistingFilesAsync(watchFolder, stoppingToken);

    _watcher = new FileSystemWatcher(watchFolder)
    {
      IncludeSubdirectories = _options.IncludeSubdirectories,
      NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
      EnableRaisingEvents = true,
    };

    FileSystemEventHandler handler = (_, args) => QueueFile(args.FullPath);
    _watcher.Created += handler;
    _watcher.Changed += handler;
    _watcher.Renamed += (_, args) => QueueFile(args.FullPath);

    try
    {
      await Task.Delay(Timeout.Infinite, stoppingToken);
    }
    catch (OperationCanceledException)
    {
      // Expected on shutdown.
    }
    finally
    {
      if (_watcher is not null)
      {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
      }
    }
  }

  private string ResolveWatchFolder()
  {
    if (!string.IsNullOrWhiteSpace(_options.WatchFolder))
    {
      return Path.GetFullPath(_options.WatchFolder);
    }

    string defaultFolder = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
      "Scrinium",
      "inbox");

    return Path.GetFullPath(defaultFolder);
  }

  private void EnsureWatchFolders(string watchFolder)
  {
    Directory.CreateDirectory(watchFolder);
    Directory.CreateDirectory(Path.Combine(watchFolder, _options.ProcessedSubfolder));
    Directory.CreateDirectory(Path.Combine(watchFolder, _options.FailedSubfolder));

    if (_options.DownloadPreviewOnReady)
    {
      Directory.CreateDirectory(Path.Combine(watchFolder, _options.PreviewOutputFolder));
    }
  }

  private async Task ScanExistingFilesAsync(string watchFolder, CancellationToken cancellationToken)
  {
    IEnumerable<string> existingFiles = Directory.EnumerateFiles(watchFolder)
      .Where(path => ShouldProcess(path, watchFolder));

    foreach (string filePath in existingFiles)
    {
      QueueFile(filePath);
    }

    await Task.CompletedTask;
  }

  private void QueueFile(string fullPath)
  {
    if (!File.Exists(fullPath))
    {
      return;
    }

    if (!_queuedPaths.TryAdd(fullPath, 0))
    {
      return;
    }

    _ = Task.Run(async () =>
    {
      try
      {
        await ProcessFileAsync(fullPath, _lifetime.ApplicationStopping);
      }
      finally
      {
        _queuedPaths.TryRemove(fullPath, out _);
      }
    });
  }

  private async Task ProcessFileAsync(string filePath, CancellationToken cancellationToken)
  {
    string watchFolder = ResolveWatchFolder();
    if (!ShouldProcess(filePath, watchFolder))
    {
      return;
    }

    _logger.LogInformation("Detected {FilePath}", filePath);

    try
    {
      await WaitForStableFileAsync(filePath, cancellationToken);

      IngestionAcceptedResponse accepted = await _apiClient.UploadAsync(
        filePath,
        _options.DefaultTags,
        cancellationToken);

      if (_options.WaitForReady)
      {
        await _apiClient.WaitForReadyAsync(
          accepted.ArchiveId,
          _options.ReadyPollIntervalMs,
          _options.ReadyTimeoutSeconds,
          cancellationToken);

        if (_options.DownloadPreviewOnReady)
        {
          BundleStatusResponse? status = await _apiClient.TryGetDefaultBundleAsync(
            accepted.ArchiveId,
            cancellationToken);
          BundleSheetResponse? firstSheet = status?.Sheets
            .OrderBy(x => x.SortOrder)
            .FirstOrDefault(x => x.RenderStatus.Equals("ready", StringComparison.OrdinalIgnoreCase));

          if (status?.Status == "ready" && firstSheet is not null)
          {
            string previewPath = Path.Combine(
              watchFolder,
              _options.PreviewOutputFolder,
              $"{accepted.ArchiveId:N}_sheet1.webp");
            await _apiClient.DownloadPreviewAsync(
              status.BundleId,
              firstSheet.ArchiveSheetId,
              previewPath,
              cancellationToken);
          }
        }
      }

      MoveFile(filePath, Path.Combine(watchFolder, _options.ProcessedSubfolder, Path.GetFileName(filePath)));
      _logger.LogInformation("Finished {FileName} -> archive {ArchiveId}", Path.GetFileName(filePath), accepted.ArchiveId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to ingest {FilePath}", filePath);
      TryMoveToFailed(filePath, watchFolder);
    }
  }

  private async Task WaitForStableFileAsync(string filePath, CancellationToken cancellationToken)
  {
    long lastSize = -1;
    int stableCount = 0;

    while (stableCount < _options.StableChecksRequired)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (!File.Exists(filePath))
      {
        throw new FileNotFoundException("File disappeared before upload.", filePath);
      }

      if (!IsFileReady(filePath, out long size))
      {
        stableCount = 0;
        lastSize = -1;
      }
      else if (size == lastSize)
      {
        stableCount++;
      }
      else
      {
        lastSize = size;
        stableCount = 1;
      }

      await Task.Delay(_options.PollIntervalMs, cancellationToken);
    }
  }

  private static bool IsFileReady(string filePath, out long size)
  {
    size = 0;
    try
    {
      using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
      size = stream.Length;
      return size > 0;
    }
    catch (IOException)
    {
      return false;
    }
    catch (UnauthorizedAccessException)
    {
      return false;
    }
  }

  private bool ShouldProcess(string filePath, string watchFolder)
  {
    string fullPath = Path.GetFullPath(filePath);
    string fileName = Path.GetFileName(fullPath);

    if (string.IsNullOrWhiteSpace(fileName))
    {
      return false;
    }

    if (fileName.StartsWith('.'))
    {
      return false;
    }

    if (fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
      || fileName.StartsWith('~'))
    {
      return false;
    }

    string processedFolder = Path.GetFullPath(Path.Combine(watchFolder, _options.ProcessedSubfolder));
    string failedFolder = Path.GetFullPath(Path.Combine(watchFolder, _options.FailedSubfolder));
    string previewFolder = Path.GetFullPath(Path.Combine(watchFolder, _options.PreviewOutputFolder));

    if (fullPath.StartsWith(processedFolder, StringComparison.OrdinalIgnoreCase)
      || fullPath.StartsWith(failedFolder, StringComparison.OrdinalIgnoreCase)
      || fullPath.StartsWith(previewFolder, StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    return File.Exists(fullPath);
  }

  private void MoveFile(string sourcePath, string destinationPath)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
    if (File.Exists(destinationPath))
    {
      string stamped = Path.Combine(
        Path.GetDirectoryName(destinationPath)!,
        $"{Path.GetFileNameWithoutExtension(destinationPath)}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(destinationPath)}");
      destinationPath = stamped;
    }

    File.Move(sourcePath, destinationPath);
  }

  private void TryMoveToFailed(string sourcePath, string watchFolder)
  {
    try
    {
      if (!File.Exists(sourcePath))
      {
        return;
      }

      MoveFile(sourcePath, Path.Combine(watchFolder, _options.FailedSubfolder, Path.GetFileName(sourcePath)));
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Could not move failed file {FilePath} to failed folder.", sourcePath);
    }
  }
}
