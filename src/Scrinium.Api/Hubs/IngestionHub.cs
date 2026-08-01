using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Scrinium.Api.Hubs;

public sealed class IngestionHub : Hub
{
  public Task SubscribeDocument(Guid documentId)
  {
    return Groups.AddToGroupAsync(Context.ConnectionId, DocumentGroup(documentId));
  }

  public static string DocumentGroup(Guid documentId) => $"document:{documentId:D}";
}
