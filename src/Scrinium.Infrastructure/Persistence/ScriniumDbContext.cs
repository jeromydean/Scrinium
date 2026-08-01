using Microsoft.EntityFrameworkCore;
using Scrinium.Core.Domain;

namespace Scrinium.Infrastructure.Persistence;

public class ScriniumDbContext : DbContext
{
  public ScriniumDbContext(DbContextOptions<ScriniumDbContext> options)
    : base(options)
  {
  }

  public DbSet<Document> Documents => Set<Document>();

  public DbSet<DocumentPage> DocumentPages => Set<DocumentPage>();

  public DbSet<Tag> Tags => Set<Tag>();

  public DbSet<DocumentTag> DocumentTags => Set<DocumentTag>();

  public DbSet<IngestStepLog> IngestStepLogs => Set<IngestStepLog>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScriniumDbContext).Assembly);
  }
}
