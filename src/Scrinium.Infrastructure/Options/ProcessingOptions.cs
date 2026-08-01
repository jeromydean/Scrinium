namespace Scrinium.Infrastructure.Options;

public sealed class TikaOptions
{
  public const string SectionName = "Tika";

  public string BaseUrl { get; set; } = "http://localhost:9998";

  public int TimeoutSeconds { get; set; } = 120;
}

public sealed class GotenbergOptions
{
  public const string SectionName = "Gotenberg";

  public string BaseUrl { get; set; } = "http://localhost:3000";

  public int TimeoutSeconds { get; set; } = 180;
}

public sealed class SolrOptions
{
  public const string SectionName = "Solr";

  public string BaseUrl { get; set; } = "https://localhost:8983/solr";

  public string CoreName { get; set; } = "documents";

  public bool SkipCertificateValidation { get; set; } = true;

  public int TimeoutSeconds { get; set; } = 60;
}

public sealed class RenderingOptions
{
  public const string SectionName = "Rendering";

  public int PdfRenderDpi { get; set; } = 150;

  public int WebpQuality { get; set; } = 80;

  public int MaxBarcodeScanPages { get; set; } = 10;
}
