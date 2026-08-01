using System;

namespace Scrinium.Core.Rendering;

public interface IRasterizedPage : IAsyncDisposable
{
  int Width { get; }

  int Height { get; }
}
