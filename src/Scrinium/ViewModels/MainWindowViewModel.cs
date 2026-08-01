using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrinium.Models;
using Scrinium.Options;
using Scrinium.Services;

namespace Scrinium.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
  private readonly ScriniumApiClient? _apiClient;
  private readonly IAccessTokenProvider? _authService;
  private readonly DispatcherTimer? _refreshTimer;
  private BundleDetailResponse? _selectedDetail;
  private CancellationTokenSource? _loadCts;
  private TopLevel? _topLevel;

  public MainWindowViewModel()
    : this(CreateDesignTimeClient(), new DesignTimeAuthService())
  {
  }

  public MainWindowViewModel(ScriniumApiClient apiClient, IAccessTokenProvider authService)
  {
    _apiClient = apiClient;
    _authService = authService;
    Documents = [];

    _refreshTimer = new DispatcherTimer
    {
      Interval = TimeSpan.FromSeconds(5),
    };
    _refreshTimer.Tick += async (_, _) => await RefreshDocumentsAsync(silent: true);
  }

  public ObservableCollection<DocumentItemViewModel> Documents { get; }

  [ObservableProperty]
  private DocumentItemViewModel? _selectedDocument;

  [ObservableProperty]
  private Bitmap? _currentPageImage;

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
  private int _currentPageNumber = 1;

  [ObservableProperty]
  private string _statusMessage = "Loading bundles...";

  [ObservableProperty]
  private string _selectedFileName = string.Empty;

  [ObservableProperty]
  private string _selectedStatusDetail = "Select a bundle to view details.";

  [ObservableProperty]
  private string _selectedTags = string.Empty;

  [ObservableProperty]
  private string _pageLabel = string.Empty;

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
  private bool _canNavigatePages;

  [ObservableProperty]
  private bool _isSignedIn;

  public bool CanSignIn => !IsSignedIn && _topLevel is not null;

  public bool CanGoPrevious => CanNavigatePages && CurrentPageNumber > 1;

  public bool CanGoNext =>
    CanNavigatePages
      && _selectedDetail is not null
      && CurrentPageNumber < _selectedDetail.SheetCount;

  partial void OnSelectedDocumentChanged(DocumentItemViewModel? value)
  {
    _ = LoadSelectedDocumentAsync();
  }

  partial void OnIsSignedInChanged(bool value)
  {
    OnPropertyChanged(nameof(CanSignIn));
    SignInCommand.NotifyCanExecuteChanged();
  }

  partial void OnCanNavigatePagesChanged(bool value)
  {
    OnPropertyChanged(nameof(CanGoPrevious));
    OnPropertyChanged(nameof(CanGoNext));
  }

  partial void OnCurrentPageNumberChanged(int value)
  {
    PageLabel = CanNavigatePages && _selectedDetail is not null
      ? $"Sheet {value} of {_selectedDetail.SheetCount}"
      : string.Empty;

    OnPropertyChanged(nameof(CanGoPrevious));
    OnPropertyChanged(nameof(CanGoNext));
  }

  [RelayCommand(CanExecute = nameof(CanSignIn))]
  private async Task SignInAsync()
  {
    if (_authService is null || _topLevel is null)
    {
      return;
    }

    try
    {
      StatusMessage = "Waiting for sign in…";
      await _authService.SignInAsync(_topLevel);
      IsSignedIn = _authService.IsSignedIn;
      StatusMessage = "Signed in.";
      await RefreshDocumentsAsync();
    }
    catch (Exception ex)
    {
      StatusMessage = $"Sign in failed: {ex.Message}";
    }
  }

  [RelayCommand]
  private Task RefreshAsync() => RefreshDocumentsAsync(silent: false);

  private async Task RefreshDocumentsAsync(bool silent = false)
  {
    if (_apiClient is null || _authService is null || !_authService.IsSignedIn)
    {
      if (!silent)
      {
        StatusMessage = "Sign in to load bundles.";
      }

      return;
    }

    try
    {
      if (!silent)
      {
        StatusMessage = "Refreshing bundles...";
      }

      BundleListResponse list = await _apiClient.ListBundlesAsync();
      Guid? selectedId = SelectedDocument?.BundleId;

      Documents.Clear();
      foreach (BundleSummaryResponse item in list.Items.OrderByDescending(x => x.CreatedAt))
      {
        Documents.Add(new DocumentItemViewModel(
          item.BundleId,
          item.Title,
          item.Status,
          item.SheetCount,
          item.CreatedAt,
          item.Tags));
      }

      if (selectedId.HasValue)
      {
        SelectedDocument = Documents.FirstOrDefault(x => x.BundleId == selectedId.Value);
      }

      bool hasInProgress = Documents.Any(x => !x.IsTerminal);
      if (hasInProgress)
      {
        _refreshTimer?.Start();
      }
      else
      {
        _refreshTimer?.Stop();
      }

      StatusMessage = $"{list.TotalCount} bundle(s)";
    }
    catch (Exception ex)
    {
      StatusMessage = $"Refresh failed: {ex.Message}";
      _refreshTimer?.Stop();
    }
  }

  [RelayCommand(CanExecute = nameof(CanGoPrevious))]
  private async Task PreviousPageAsync()
  {
    CurrentPageNumber--;
    await LoadCurrentPageAsync();
  }

  [RelayCommand(CanExecute = nameof(CanGoNext))]
  private async Task NextPageAsync()
  {
    CurrentPageNumber++;
    await LoadCurrentPageAsync();
  }

  public async Task InitializeAsync(TopLevel topLevel)
  {
    _topLevel = topLevel;
    IsSignedIn = _authService?.IsSignedIn ?? false;
    OnPropertyChanged(nameof(CanSignIn));
    SignInCommand.NotifyCanExecuteChanged();

    if (IsSignedIn)
    {
      await RefreshDocumentsAsync();
      return;
    }

    StatusMessage = "Sign in to load bundles.";
    await SignInAsync();
  }

  private async Task LoadSelectedDocumentAsync()
  {
    _loadCts?.Cancel();
    _loadCts = new CancellationTokenSource();
    CancellationToken cancellationToken = _loadCts.Token;

    CurrentPageImage?.Dispose();
    CurrentPageImage = null;
    CanNavigatePages = false;
    CurrentPageNumber = 1;
    _selectedDetail = null;

    if (SelectedDocument is null || _apiClient is null)
    {
      SelectedFileName = string.Empty;
      SelectedStatusDetail = "Select a bundle to view details.";
      SelectedTags = string.Empty;
      PageLabel = string.Empty;
      return;
    }

    try
    {
      StatusMessage = $"Loading {SelectedDocument.FileName}...";
      BundleDetailResponse? detail = await _apiClient.GetBundleStatusAsync(
        SelectedDocument.BundleId,
        cancellationToken);

      if (detail is null || cancellationToken.IsCancellationRequested)
      {
        return;
      }

      _selectedDetail = detail;
      SelectedDocument.Update(detail.Status, detail.SheetCount);

      SelectedFileName = detail.Title;
      SelectedTags = detail.Tags.Length == 0
        ? "No tags"
        : string.Join(", ", detail.Tags);

      if (detail.Status.Equals("ready", StringComparison.OrdinalIgnoreCase))
      {
        string quality = detail.IngestQuality is null ? string.Empty : $" · {detail.IngestQuality}";
        SelectedStatusDetail =
          $"Ready{quality} · {detail.SheetCount} sheet(s) · created {detail.CreatedAt.ToLocalTime():g}";

        if (detail.SheetsFailedCount > 0)
        {
          SelectedStatusDetail += $" · {detail.SheetsFailedCount} sheet(s) failed";
        }

        CanNavigatePages = detail.SheetCount > 0;
        CurrentPageNumber = 1;
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        await LoadCurrentPageAsync();
      }
      else if (detail.Status.Equals("error", StringComparison.OrdinalIgnoreCase))
      {
        SelectedStatusDetail = "Processing failed.";
      }
      else
      {
        SelectedStatusDetail = $"Status: {detail.Status}";
      }

      StatusMessage = $"{Documents.Count} bundle(s)";
    }
    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
    {
      SelectedStatusDetail = ex.Message;
      StatusMessage = $"Load failed: {ex.Message}";
    }
  }

  private async Task LoadCurrentPageAsync()
  {
    if (_apiClient is null || _selectedDetail is null || !CanNavigatePages || SelectedDocument is null)
    {
      return;
    }

    BundleSheetResponse? sheet = _selectedDetail.Sheets
      .OrderBy(x => x.SortOrder)
      .ElementAtOrDefault(CurrentPageNumber - 1);

    if (sheet is null || !sheet.RenderStatus.Equals("ready", StringComparison.OrdinalIgnoreCase))
    {
      SelectedStatusDetail = sheet is null
        ? "Sheet not found."
        : $"Sheet {CurrentPageNumber} is not ready ({sheet.RenderStatus}).";
      return;
    }

    try
    {
      await using Stream stream = await _apiClient.GetSheetRenderAsync(
        SelectedDocument.BundleId,
        sheet.ArchiveSheetId,
        cancellationToken: CancellationToken.None);

      using MemoryStream memoryStream = new();
      await stream.CopyToAsync(memoryStream);
      memoryStream.Position = 0;

      Bitmap bitmap = new(memoryStream);
      Bitmap? previous = CurrentPageImage;
      CurrentPageImage = bitmap;
      previous?.Dispose();

      PageLabel = $"Sheet {CurrentPageNumber} of {_selectedDetail.SheetCount}";
      OnPropertyChanged(nameof(CanGoPrevious));
      OnPropertyChanged(nameof(CanGoNext));
    }
    catch (Exception ex)
    {
      SelectedStatusDetail = $"Failed to load sheet: {ex.Message}";
    }
  }

  private static ScriniumApiClient CreateDesignTimeClient()
  {
    HttpClient apiHttpClient = new();
    return new ScriniumApiClient(apiHttpClient, new ApiOptions(), new DesignTimeAuthService());
  }

  private sealed class DesignTimeAuthService : IAccessTokenProvider
  {
    public bool IsSignedIn => false;

    public Task SignInAsync(TopLevel topLevel, CancellationToken cancellationToken = default)
      => Task.CompletedTask;

    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
      => Task.FromResult(string.Empty);
  }
}
