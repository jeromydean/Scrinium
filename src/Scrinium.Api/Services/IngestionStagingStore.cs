using Microsoft.Extensions.Options;
using Scrinium.Api.Options;

namespace Scrinium.Api.Services
{
  public sealed class IngestionStagingStore : IIngestionStagingStore
  {
    private readonly IngestionOptions _options;

    public IngestionStagingStore(IOptions<IngestionOptions> options)
    {
      _options = options.Value;
    }

    public async Task<string> SaveAsync(
      IFormFile file,
      Guid documentId,
      CancellationToken cancellationToken = default)
    {
      if (file is null)
      {
        throw new ArgumentNullException(nameof(file));
      }

      string stagingRoot = ResolveStagingRoot();
      string documentDirectory = Path.Combine(stagingRoot, documentId.ToString("N"));
      Directory.CreateDirectory(documentDirectory);

      string extension = Path.GetExtension(file.FileName);
      string stagingPath = Path.Combine(documentDirectory, "original" + extension);

      await using (FileStream stream = new(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
      {
        await file.CopyToAsync(stream, cancellationToken);
      }

      return stagingPath;
    }

    private string ResolveStagingRoot()
    {
      if (!string.IsNullOrWhiteSpace(_options.StagingPath))
      {
        return _options.StagingPath;
      }

      return Path.Combine(Path.GetTempPath(), "scrinium-ingestion");
    }
  }
}
