using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Scrinium.Core.Domain;
using Scrinium.Core.Enums;
using Scrinium.Core.Jobs;

namespace Scrinium.Core.Ports;

public interface IArchiveRepository
{
  Task<Archive?> GetByIdAsync(Guid archiveId, CancellationToken cancellationToken);

  Task<Archive?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

  Task AddAsync(Archive archive, CancellationToken cancellationToken);

  Task UpdateAsync(Archive archive, CancellationToken cancellationToken);

  Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IBundleRepository
{
  Task<Bundle?> GetByIdAsync(Guid bundleId, CancellationToken cancellationToken);

  Task<Bundle?> GetDefaultByArchiveIdAsync(Guid archiveId, CancellationToken cancellationToken);

  Task<BundleListResult> ListReadyAsync(
    BundleListQuery query,
    CancellationToken cancellationToken);

  Task AddAsync(Bundle bundle, CancellationToken cancellationToken);

  Task UpdateAsync(Bundle bundle, CancellationToken cancellationToken);

  Task SaveChangesAsync(CancellationToken cancellationToken);

  Task<int> CountPendingMemberSheetsAsync(Guid bundleId, CancellationToken cancellationToken);

  Task<bool> TryMarkFinalizeEnqueuedAsync(Guid bundleId, CancellationToken cancellationToken);
}

public interface ITagService
{
  Task ApplyTagsAsync(
    Guid bundleId,
    IEnumerable<string> tagNames,
    TagSource source,
    Guid appliedBy,
    CancellationToken cancellationToken);
}

public interface IJobQueue
{
  Task EnqueueAsync<T>(
    string streamName,
    T payload,
    CancellationToken cancellationToken);

  Task<QueueMessage<T>?> DequeueAsync<T>(
    string streamName,
    string consumerGroup,
    string consumerName,
    CancellationToken cancellationToken);

  Task AcknowledgeAsync(
    string streamName,
    string consumerGroup,
    string messageId,
    CancellationToken cancellationToken);

  Task EnsureConsumerGroupAsync(
    string streamName,
    string consumerGroup,
    CancellationToken cancellationToken);
}

public interface IBlobStore
{
  Task PutAsync(string key, Stream content, CancellationToken cancellationToken);

  Task<Stream> GetAsync(string key, CancellationToken cancellationToken);

  Task<bool> ExistsAsync(string key, CancellationToken cancellationToken);

  Task DeleteAsync(string key, CancellationToken cancellationToken);
}
