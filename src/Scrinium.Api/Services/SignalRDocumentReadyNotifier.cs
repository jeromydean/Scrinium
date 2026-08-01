using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Scrinium.Api.Hubs;
using Scrinium.Core.Ports;

namespace Scrinium.Api.Services;

public sealed class SignalRDocumentReadyNotifier : IDocumentReadyNotifier
{
  private readonly IHubContext<IngestionHub> _hubContext;

  public SignalRDocumentReadyNotifier(IHubContext<IngestionHub> hubContext)
  {
    _hubContext = hubContext;
  }

  public Task NotifyDocumentReadyAsync(Guid documentId, CancellationToken cancellationToken)
  {
    return _hubContext.Clients
      .Group(IngestionHub.DocumentGroup(documentId))
      .SendAsync(
        "DocumentReady",
        new { documentId },
        cancellationToken);
  }
}
