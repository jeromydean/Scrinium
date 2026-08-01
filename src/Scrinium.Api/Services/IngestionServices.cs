using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Scrinium.Api.Options;

namespace Scrinium.Api.Services;

public interface IIngestionStagingStore
{
  Task<string> SaveAsync(
    IFormFile file,
    Guid documentId,
    CancellationToken cancellationToken);
}

public sealed class IngestionStagingStore : IIngestionStagingStore
{
  private readonly IngestionOptions _options;

  public IngestionStagingStore(Microsoft.Extensions.Options.IOptions<IngestionOptions> options)
  {
    _options = options.Value;
  }

  public async Task<string> SaveAsync(
    IFormFile file,
    Guid documentId,
    CancellationToken cancellationToken)
  {
    string root = string.IsNullOrWhiteSpace(_options.StagingPath)
      ? Path.Combine(Path.GetTempPath(), "scrinium-ingestion")
      : _options.StagingPath;

    string directory = Path.Combine(root, documentId.ToString("N"));
    Directory.CreateDirectory(directory);

    string extension = Path.GetExtension(file.FileName);
    string stagingPath = Path.Combine(directory, $"original{extension}");

    await using FileStream stream = File.Create(stagingPath);
    await file.CopyToAsync(stream, cancellationToken);

    return stagingPath;
  }
}

public interface IClientMetadataReader
{
  Dictionary<string, string> Read(IFormCollection form);
}

public sealed class ClientMetadataReader : IClientMetadataReader
{
  public Dictionary<string, string> Read(IFormCollection form)
  {
    Dictionary<string, string> fields = form.Keys
      .ToDictionary(key => key, key => form[key].ToString(), StringComparer.Ordinal);

    string? metadataJson = form["metadata"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(metadataJson))
    {
      return Scrinium.Core.Ingestion.ClientMetadataParser.ParseFromJson(metadataJson);
    }

    return Scrinium.Core.Ingestion.ClientMetadataParser.ParseFromForm(fields);
  }
}
