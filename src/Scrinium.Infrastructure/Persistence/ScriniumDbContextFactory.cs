using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Scrinium.Infrastructure.Persistence;

internal sealed class ScriniumDbContextFactory : IDesignTimeDbContextFactory<ScriniumDbContext>
{
  public ScriniumDbContext CreateDbContext(string[] args)
  {
    DbContextOptions<ScriniumDbContext> options = new DbContextOptionsBuilder<ScriniumDbContext>()
      .UseNpgsql("Host=localhost;Database=scrinium;Username=scrinium;Password=scrinium")
      .Options;

    return new ScriniumDbContext(options);
  }
}
