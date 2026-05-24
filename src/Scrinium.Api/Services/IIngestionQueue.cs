using Scrinium.Api.Models;

namespace Scrinium.Api.Services
{
  public interface IIngestionQueue
  {
    ValueTask EnqueueAsync(IngestionJob job, CancellationToken cancellationToken = default);

    ValueTask<IngestionJob> DequeueAsync(CancellationToken cancellationToken = default);
  }
}
