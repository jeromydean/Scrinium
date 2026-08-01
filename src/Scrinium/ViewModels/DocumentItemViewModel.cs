using System;

namespace Scrinium.ViewModels;

public sealed class DocumentItemViewModel : ViewModelBase
{
  public DocumentItemViewModel(
    Guid bundleId,
    string title,
    string status,
    int sheetCount,
    DateTimeOffset createdAt,
    string[] tags)
  {
    BundleId = bundleId;
    FileName = title;
    Status = status;
    PageCount = sheetCount;
    UploadedAt = createdAt;
    Tags = tags;
  }

  public Guid BundleId { get; }

  public string FileName { get; }

  public string Status { get; private set; }

  public string? ProcessingStep { get; private set; }

  public int PageCount { get; private set; }

  public DateTimeOffset UploadedAt { get; }

  public string[] Tags { get; }

  public string StatusLabel => ProcessingStep is null
    ? Status
    : $"{Status} · {ProcessingStep}";

  public string UploadedAtLabel => UploadedAt.ToLocalTime().ToString("g");

  public string TagsLabel => Tags.Length == 0 ? string.Empty : string.Join(", ", Tags);

  public bool IsReady => Status.Equals("ready", StringComparison.OrdinalIgnoreCase);

  public bool IsTerminal =>
    IsReady || Status.Equals("error", StringComparison.OrdinalIgnoreCase);

  public void Update(string status, int sheetCount)
  {
    Status = status;
    PageCount = sheetCount;
    OnPropertyChanged(nameof(Status));
    OnPropertyChanged(nameof(PageCount));
    OnPropertyChanged(nameof(StatusLabel));
    OnPropertyChanged(nameof(IsReady));
    OnPropertyChanged(nameof(IsTerminal));
  }
}
