using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Scrinium.Core.Domain;
using Scrinium.Core.Ports;

namespace Scrinium.Infrastructure.Persistence.Repositories;

public sealed class ArchiveRepository : IArchiveRepository
{
  private readonly ScriniumDbContext _dbContext;

  public ArchiveRepository(ScriniumDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public Task<Archive?> GetByIdAsync(Guid archiveId, CancellationToken cancellationToken)
  {
    return _dbContext.Archives
      .Include(x => x.ArchiveSheets)
      .ThenInclude(x => x.Barcodes)
      .FirstOrDefaultAsync(x => x.Id == archiveId, cancellationToken);
  }

  public Task<Archive?> GetByIdempotencyKeyAsync(
    string idempotencyKey,
    CancellationToken cancellationToken)
  {
    return _dbContext.Archives
      .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
  }

  public async Task AddAsync(Archive archive, CancellationToken cancellationToken)
  {
    await _dbContext.Archives.AddAsync(archive, cancellationToken);
  }

  public Task UpdateAsync(Archive archive, CancellationToken cancellationToken)
  {
    _dbContext.Archives.Update(archive);
    return Task.CompletedTask;
  }

  public Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    return _dbContext.SaveChangesAsync(cancellationToken);
  }
}
