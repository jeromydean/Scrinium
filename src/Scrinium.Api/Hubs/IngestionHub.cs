using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Scrinium.Api.Hubs;

public sealed class IngestionHub : Hub
{
  public Task SubscribeBundle(Guid bundleId)
  {
    return Groups.AddToGroupAsync(Context.ConnectionId, BundleGroup(bundleId));
  }

  public static string BundleGroup(Guid bundleId) => $"bundle:{bundleId:D}";
}
