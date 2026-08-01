using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Scrinium.Core.Domain;
using Scrinium.Core.Enums;
using Scrinium.Core.Ports;

namespace Scrinium.Infrastructure.Persistence.Repositories;

public sealed class DocumentRepository : IDocumentRepository
{
  private readonly ScriniumDbContext _dbContext;

  public DocumentRepository(ScriniumDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public Task<Document?> GetByIdAsync(Guid documentId, CancellationToken cancellationToken)
  {
    return _dbContext.Documents
      .Include(x => x.Pages)
      .Include(x => x.DocumentTags)
      .ThenInclude(x => x.Tag)
      .FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken);
  }

  public Task<Document?> GetByIdempotencyKeyAsync(
    string idempotencyKey,
    CancellationToken cancellationToken)
  {
    return _dbContext.Documents
      .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
  }

  public async Task AddAsync(Document document, CancellationToken cancellationToken)
  {
    await _dbContext.Documents.AddAsync(document, cancellationToken);
  }

  public Task UpdateAsync(Document document, CancellationToken cancellationToken)
  {
    _dbContext.Documents.Update(document);
    return Task.CompletedTask;
  }

  public Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    return _dbContext.SaveChangesAsync(cancellationToken);
  }

  public Task<int> CountPendingPagesAsync(Guid documentId, CancellationToken cancellationToken)
  {
    return _dbContext.DocumentPages.CountAsync(
      x => x.DocumentId == documentId
        && (x.RenderStatus == PageProcessingStatus.Pending
          || x.ProcessingStatus == PageProcessingStatus.Pending),
      cancellationToken);
  }

  public async Task<bool> TryMarkFinalizeEnqueuedAsync(
    Guid documentId,
    CancellationToken cancellationToken)
  {
    int updated = await _dbContext.Documents
      .Where(x => x.Id == documentId && !x.FinalizeEnqueued)
      .ExecuteUpdateAsync(
        setters => setters.SetProperty(x => x.FinalizeEnqueued, true),
        cancellationToken);

    return updated > 0;
  }
}
