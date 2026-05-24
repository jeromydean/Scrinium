using Microsoft.AspNetCore.Http;

namespace Scrinium.Api.Services
{
  public interface IIngestionStagingStore
  {
    Task<string> SaveAsync(
      IFormFile file,
      Guid documentId,
      CancellationToken cancellationToken = default);
  }
}
