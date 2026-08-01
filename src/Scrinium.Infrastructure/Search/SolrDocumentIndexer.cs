using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrinium.Core.Domain;
using Scrinium.Core.Ports;
using Scrinium.Infrastructure.Options;
using Scrinium.Infrastructure.Persistence;

namespace Scrinium.Infrastructure.Search;

public sealed class SolrDocumentIndexer : ISearchIndexer
{
  private readonly HttpClient _httpClient;
  private readonly ScriniumDbContext _dbContext;
  private readonly SolrOptions _options;
  private readonly ILogger<SolrDocumentIndexer> _logger;

  public SolrDocumentIndexer(
    HttpClient httpClient,
    ScriniumDbContext dbContext,
    IOptions<SolrOptions> options,
    ILogger<SolrDocumentIndexer> logger)
  {
    _httpClient = httpClient;
    _dbContext = dbContext;
    _options = options.Value;
    _logger = logger;
    _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
  }

  public async Task IndexBundleAsync(Guid bundleId, CancellationToken cancellationToken)
  {
    Bundle? bundle = await _dbContext.Bundles
      .AsNoTracking()
      .Include(x => x.BundleTags)
      .ThenInclude(x => x.Tag)
      .Include(x => x.Sheets)
      .ThenInclude(x => x.ArchiveSheet)
      .ThenInclude(x => x.Barcodes)
      .FirstOrDefaultAsync(x => x.Id == bundleId, cancellationToken);

    if (bundle is null)
    {
      throw new InvalidOperationException($"Bundle {bundleId} was not found for indexing.");
    }

    List<string> tags = bundle.BundleTags
      .Select(x => x.Tag.Name)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList();

    string bundleIdValue = bundle.Id.ToString("D");
    string? archiveIdValue = bundle.SourceArchiveId?.ToString("D");

    List<Dictionary<string, object?>> solrDocs = bundle.Sheets
      .OrderBy(x => x.SortOrder)
      .Select(membership =>
      {
        ArchiveSheet sheet = membership.ArchiveSheet;
        List<string> barcodes = sheet.Barcodes
          .Select(x => x.Value)
          .Where(x => !string.IsNullOrWhiteSpace(x))
          .Distinct(StringComparer.Ordinal)
          .ToList();

        return new Dictionary<string, object?>
        {
          ["id"] = sheet.Id.ToString("D"),
          ["archive_sheet_id_s"] = sheet.Id.ToString("D"),
          ["archive_id_s"] = archiveIdValue ?? sheet.ArchiveId.ToString("D"),
          ["bundle_ids_ss"] = new[] { bundleIdValue },
          ["sequence_in_archive_i"] = sheet.SequenceInArchive,
          ["sort_order_i"] = membership.SortOrder,
          ["title_s"] = bundle.Title,
          ["content_txt"] = sheet.PlainText ?? string.Empty,
          ["has_text_layer_b"] = sheet.HasTextLayer,
          ["barcodes_ss"] = barcodes,
          ["tags_ss"] = tags,
          ["created_at_dt"] = bundle.CreatedAt.UtcDateTime.ToString("o"),
        };
      })
      .ToList();

    if (solrDocs.Count == 0)
    {
      _logger.LogWarning("Bundle {BundleId} has no sheets to index.", bundleId);
      return;
    }

    // Drop the legacy one-doc-per-bundle id if it was indexed by an earlier build.
    await DeleteByIdAsync(bundleIdValue, cancellationToken);

    string updateUrl = $"{_options.BaseUrl.TrimEnd('/')}/{_options.CoreName}/update?commit=true";
    string payload = JsonSerializer.Serialize(solrDocs);
    using StringContent content = new(payload, Encoding.UTF8, "application/json");

    HttpResponseMessage response = await _httpClient.PostAsync(updateUrl, content, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
      string body = await response.Content.ReadAsStringAsync(cancellationToken);
      throw new InvalidOperationException(
        $"Solr indexing failed ({(int)response.StatusCode}): {body}");
    }

    _logger.LogInformation(
      "Indexed {SheetCount} archive sheet(s) for bundle {BundleId} in Solr.",
      solrDocs.Count,
      bundleId);
  }

  private async Task DeleteByIdAsync(string id, CancellationToken cancellationToken)
  {
    string deleteUrl = $"{_options.BaseUrl.TrimEnd('/')}/{_options.CoreName}/update?commit=true";
    string payload = JsonSerializer.Serialize(new { delete = new { id } });
    using StringContent content = new(payload, Encoding.UTF8, "application/json");
    HttpResponseMessage response = await _httpClient.PostAsync(deleteUrl, content, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
      string body = await response.Content.ReadAsStringAsync(cancellationToken);
      _logger.LogWarning(
        "Failed to delete legacy Solr doc {Id} ({Status}): {Body}",
        id,
        (int)response.StatusCode,
        body);
    }
  }
}
