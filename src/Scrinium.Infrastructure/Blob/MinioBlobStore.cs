using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Scrinium.Core.Ports;

namespace Scrinium.Infrastructure.Blob;

public sealed class MinioOptions
{
  public const string SectionName = "Minio";

  public string Endpoint { get; set; } = "localhost:9000";

  public string AccessKey { get; set; } = "minioadmin";

  public string SecretKey { get; set; } = "minioadmin";

  public string Bucket { get; set; } = "scrinium";

  public bool UseSsl { get; set; }
}

public sealed class MinioBlobStore : IBlobStore
{
  private readonly IMinioClient _client;
  private readonly MinioOptions _options;
  private readonly ILogger<MinioBlobStore> _logger;
  private bool _bucketEnsured;

  public MinioBlobStore(
    IMinioClient client,
    IOptions<MinioOptions> options,
    ILogger<MinioBlobStore> logger)
  {
    _client = client;
    _options = options.Value;
    _logger = logger;
  }

  public async Task PutAsync(string key, Stream content, CancellationToken cancellationToken)
  {
    await EnsureBucketAsync(cancellationToken);

    PutObjectArgs args = new PutObjectArgs()
      .WithBucket(_options.Bucket)
      .WithObject(key)
      .WithStreamData(content)
      .WithObjectSize(content.Length);

    await _client.PutObjectAsync(args, cancellationToken);
  }

  public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken)
  {
    await EnsureBucketAsync(cancellationToken);

    MemoryStream memoryStream = new();
    GetObjectArgs args = new GetObjectArgs()
      .WithBucket(_options.Bucket)
      .WithObject(key)
      .WithCallbackStream(stream => stream.CopyTo(memoryStream));

    await _client.GetObjectAsync(args, cancellationToken);
    memoryStream.Position = 0;
    return memoryStream;
  }

  public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
  {
    await EnsureBucketAsync(cancellationToken);

    try
    {
      StatObjectArgs args = new StatObjectArgs()
        .WithBucket(_options.Bucket)
        .WithObject(key);

      await _client.StatObjectAsync(args, cancellationToken);
      return true;
    }
    catch (Minio.Exceptions.ObjectNotFoundException)
    {
      return false;
    }
  }

  public async Task DeleteAsync(string key, CancellationToken cancellationToken)
  {
    RemoveObjectArgs args = new RemoveObjectArgs()
      .WithBucket(_options.Bucket)
      .WithObject(key);

    await _client.RemoveObjectAsync(args, cancellationToken);
  }

  private async Task EnsureBucketAsync(CancellationToken cancellationToken)
  {
    if (_bucketEnsured)
    {
      return;
    }

    BucketExistsArgs existsArgs = new BucketExistsArgs().WithBucket(_options.Bucket);
    bool exists = await _client.BucketExistsAsync(existsArgs, cancellationToken);
    if (!exists)
    {
      MakeBucketArgs makeArgs = new MakeBucketArgs().WithBucket(_options.Bucket);
      await _client.MakeBucketAsync(makeArgs, cancellationToken);
      _logger.LogInformation("Created MinIO bucket {Bucket}.", _options.Bucket);
    }

    _bucketEnsured = true;
  }
}
