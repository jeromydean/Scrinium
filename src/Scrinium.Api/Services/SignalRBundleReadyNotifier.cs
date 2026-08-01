using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Scrinium.Api.Hubs;
using Scrinium.Core.Ports;

namespace Scrinium.Api.Services;

public sealed class SignalRBundleReadyNotifier : IBundleReadyNotifier
{
  private readonly IHubContext<IngestionHub> _hubContext;

  public SignalRBundleReadyNotifier(IHubContext<IngestionHub> hubContext)
  {
    _hubContext = hubContext;
  }

  public Task NotifyBundleReadyAsync(Guid bundleId, CancellationToken cancellationToken)
  {
    return _hubContext.Clients
      .Group(IngestionHub.BundleGroup(bundleId))
      .SendAsync(
        "BundleReady",
        new { bundleId },
        cancellationToken);
  }
}
