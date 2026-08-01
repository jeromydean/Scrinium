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

  public async Task IndexDocumentAsync(Guid documentId, CancellationToken cancellationToken)
  {
    Core.Domain.Document? document = await _dbContext.Documents
      .Include(x => x.DocumentTags)
      .ThenInclude(x => x.Tag)
      .Include(x => x.Pages)
      .FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken);

    if (document is null)
    {
      throw new InvalidOperationException($"Document {documentId} was not found for indexing.");
    }

    IEnumerable<string> pageTexts = document.Pages
      .OrderBy(x => x.PageNumber)
      .Select(x => x.PlainText)
      .Where(x => !string.IsNullOrWhiteSpace(x))
      .Select(x => x!);

    string combinedText = string.Join(
      "\n\n",
      new[] { document.ExtractedText ?? string.Empty }.Concat(pageTexts)
        .Where(x => !string.IsNullOrWhiteSpace(x)));

    List<string> tags = document.DocumentTags
      .Select(x => x.Tag.Name)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList();

    Dictionary<string, object?> solrDoc = new()
    {
      ["id"] = document.Id.ToString("D"),
      ["document_id_s"] = document.Id.ToString("D"),
      ["file_name_s"] = document.OriginalFileName,
      ["content_type_s"] = document.ContentType,
      ["content_txt"] = combinedText,
      ["page_count_i"] = document.PageCount,
      ["uploaded_at_dt"] = document.UploadedAt.UtcDateTime.ToString("o"),
      ["tags_ss"] = tags,
    };

    foreach (KeyValuePair<string, string> entry in document.ClientMetadata)
    {
      solrDoc[$"client_{SanitizeField(entry.Key)}_s"] = entry.Value;
    }

    string updateUrl = $"{_options.BaseUrl.TrimEnd('/')}/{_options.CoreName}/update?commit=true";
    string payload = JsonSerializer.Serialize(new[] { solrDoc });
    using StringContent content = new(payload, Encoding.UTF8, "application/json");

    HttpResponseMessage response = await _httpClient.PostAsync(updateUrl, content, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
      string body = await response.Content.ReadAsStringAsync(cancellationToken);
      throw new InvalidOperationException(
        $"Solr indexing failed ({(int)response.StatusCode}): {body}");
    }

    _logger.LogInformation("Indexed document {DocumentId} in Solr.", documentId);
  }

  private static string SanitizeField(string key)
  {
    char[] chars = key.ToLowerInvariant()
      .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
      .ToArray();
    return new string(chars).Trim('_');
  }
}
