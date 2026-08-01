using Microsoft.EntityFrameworkCore;
using Scrinium.Core.Domain;

namespace Scrinium.Infrastructure.Persistence;

public class ScriniumDbContext : DbContext
{
  public ScriniumDbContext(DbContextOptions<ScriniumDbContext> options)
    : base(options)
  {
  }

  public DbSet<Archive> Archives => Set<Archive>();

  public DbSet<ArchiveSheet> ArchiveSheets => Set<ArchiveSheet>();

  public DbSet<Bundle> Bundles => Set<Bundle>();

  public DbSet<Sheet> Sheets => Set<Sheet>();

  public DbSet<SheetBarcode> SheetBarcodes => Set<SheetBarcode>();

  public DbSet<Tag> Tags => Set<Tag>();

  public DbSet<BundleTag> BundleTags => Set<BundleTag>();

  public DbSet<IngestStepLog> IngestStepLogs => Set<IngestStepLog>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScriniumDbContext).Assembly);
  }
}
