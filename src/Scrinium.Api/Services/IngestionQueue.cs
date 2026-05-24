using System.Threading.Channels;
using Scrinium.Api.Models;

namespace Scrinium.Api.Services
{
  public sealed class IngestionQueue : IIngestionQueue
  {
    private readonly Channel<IngestionJob> _channel = Channel.CreateUnbounded<IngestionJob>(
      new UnboundedChannelOptions
      {
        SingleReader = true,
        SingleWriter = false,
      });

    public ValueTask EnqueueAsync(IngestionJob job, CancellationToken cancellationToken = default)
    {
      if (job is null)
      {
        throw new ArgumentNullException(nameof(job));
      }

      return _channel.Writer.WriteAsync(job, cancellationToken);
    }

    public ValueTask<IngestionJob> DequeueAsync(CancellationToken cancellationToken = default)
    {
      return _channel.Reader.ReadAsync(cancellationToken);
    }
  }
}
