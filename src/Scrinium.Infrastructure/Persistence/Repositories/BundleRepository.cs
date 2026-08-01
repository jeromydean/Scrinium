using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Scrinium.Core.Domain;
using Scrinium.Core.Enums;
using Scrinium.Core.Ports;

namespace Scrinium.Infrastructure.Persistence.Repositories;

public sealed class BundleRepository : IBundleRepository
{
  private readonly ScriniumDbContext _dbContext;

  public BundleRepository(ScriniumDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public Task<Bundle?> GetByIdAsync(Guid bundleId, CancellationToken cancellationToken)
  {
    return _dbContext.Bundles
      .Include(x => x.Sheets)
      .ThenInclude(x => x.ArchiveSheet)
      .Include(x => x.BundleTags)
      .ThenInclude(x => x.Tag)
      .FirstOrDefaultAsync(x => x.Id == bundleId, cancellationToken);
  }

  public Task<Bundle?> GetDefaultByArchiveIdAsync(Guid archiveId, CancellationToken cancellationToken)
  {
    return _dbContext.Bundles
      .Include(x => x.Sheets)
      .ThenInclude(x => x.ArchiveSheet)
      .Include(x => x.BundleTags)
      .ThenInclude(x => x.Tag)
      .FirstOrDefaultAsync(
        x => x.SourceArchiveId == archiveId && x.IsDefault,
        cancellationToken);
  }

  public async Task<BundleListResult> ListReadyAsync(
    BundleListQuery query,
    CancellationToken cancellationToken)
  {
    IQueryable<Bundle> bundles = _dbContext.Bundles
      .AsNoTracking()
      .Include(x => x.Sheets)
      .Include(x => x.BundleTags)
      .ThenInclude(x => x.Tag);

    if (query.Status.HasValue)
    {
      bundles = bundles.Where(x => x.Status == query.Status.Value);
    }
    else
    {
      bundles = bundles.Where(x => x.Status == BundleStatus.Ready);
    }

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      string search = query.Search.Trim();
      bundles = bundles.Where(x => x.Title.Contains(search));
    }

    int totalCount = await bundles.CountAsync(cancellationToken);
    List<Bundle> items = await bundles
      .OrderByDescending(x => x.ReadyAt)
      .ThenByDescending(x => x.CreatedAt)
      .Skip(query.Skip)
      .Take(query.Take)
      .ToListAsync(cancellationToken);

    return new BundleListResult
    {
      Items = items,
      TotalCount = totalCount,
    };
  }

  public async Task AddAsync(Bundle bundle, CancellationToken cancellationToken)
  {
    await _dbContext.Bundles.AddAsync(bundle, cancellationToken);
  }

  public Task UpdateAsync(Bundle bundle, CancellationToken cancellationToken)
  {
    _dbContext.Bundles.Update(bundle);
    return Task.CompletedTask;
  }

  public Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    return _dbContext.SaveChangesAsync(cancellationToken);
  }

  public Task<int> CountPendingMemberSheetsAsync(Guid bundleId, CancellationToken cancellationToken)
  {
    return _dbContext.Sheets.CountAsync(
      x => x.BundleId == bundleId
        && (x.ArchiveSheet.RenderStatus == PageProcessingStatus.Pending
          || x.ArchiveSheet.ProcessingStatus == PageProcessingStatus.Pending),
      cancellationToken);
  }

  public async Task<bool> TryMarkFinalizeEnqueuedAsync(
    Guid bundleId,
    CancellationToken cancellationToken)
  {
    int updated = await _dbContext.Bundles
      .Where(x => x.Id == bundleId && !x.FinalizeEnqueued)
      .ExecuteUpdateAsync(
        setters => setters.SetProperty(x => x.FinalizeEnqueued, true),
        cancellationToken);

    return updated > 0;
  }
}
