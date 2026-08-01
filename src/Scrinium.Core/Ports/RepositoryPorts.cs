using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Scrinium.Core.Domain;
using Scrinium.Core.Enums;
using Scrinium.Core.Jobs;

namespace Scrinium.Core.Ports;

public interface IDocumentRepository
{
  Task<Document?> GetByIdAsync(Guid documentId, CancellationToken cancellationToken);

  Task<Document?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

  Task AddAsync(Document document, CancellationToken cancellationToken);

  Task UpdateAsync(Document document, CancellationToken cancellationToken);

  Task SaveChangesAsync(CancellationToken cancellationToken);

  Task<int> CountPendingPagesAsync(Guid documentId, CancellationToken cancellationToken);

  Task<bool> TryMarkFinalizeEnqueuedAsync(Guid documentId, CancellationToken cancellationToken);
}

public interface ITagService
{
  Task ApplyTagsAsync(
    Guid documentId,
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
