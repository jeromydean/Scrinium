# Document Management System — Architecture & Design Notes

## Table of Contents
1. [Project Name](#project-name)
2. [Technology Stack Overview](#technology-stack-overview)
3. [Database — PostgreSQL](#database--postgresql)
4. [Full Text Search — Apache Solr](#full-text-search--apache-solr)
5. [Document Extraction — Apache Tika](#document-extraction--apache-tika)
6. [Document Conversion — Gotenberg](#document-conversion--gotenberg)
7. [Document Rendering — PDFium](#document-rendering--pdfium)
8. [Pre-Rendering Strategy](#pre-rendering-strategy)
9. [File Storage — MinIO](#file-storage--minio)
10. [Caching — Redis](#caching--redis)
11. [Authentication — Keycloak](#authentication--keycloak)
12. [UI — Avalonia](#ui--avalonia)
13. [Workflow Engine](#workflow-engine)
14. [Full Docker Compose Stack](#full-docker-compose-stack)
15. [Complete Architecture Diagram](#complete-architecture-diagram)

---

## Project Name

Several names were considered for the project. Key criteria included reflecting the three
core pillars of the system: document storage/archiving, workflow automation, and full-text
search powered by Apache Solr/Lucene.

**Candidates considered:**
- Archiflow, DocHarbor, Filament, PaperTrail
- Lucerna *(rejected — multiple existing software companies use this name)*
- Solarch, DocLens, Omnidoc, Lucidoc

**Recommended name: `Solarch`**
Directly references Solr, implies archiving, unique, and unlikely to have naming conflicts.

---

## Technology Stack Overview

| Component | Technology | Purpose |
|---|---|---|
| Language / Runtime | C# .NET 10 | All backend and desktop client code |
| UI Framework | Avalonia 11 | Cross-platform native desktop UI |
| Database | PostgreSQL 16 | Metadata, audit logs, permissions (ACID) |
| Full Text Search | Apache Solr 9 | Document content indexing and search |
| Text Extraction | Apache Tika | DOCX, RTF, XLSX, EML metadata/text extraction |
| Document Conversion | Gotenberg 8 | Convert all formats to PDF via LibreOffice/Chromium |
| PDF Rendering | PDFium | Render PDF pages to images, barcode extraction |
| File Storage | MinIO | Object storage for originals, PDFs, rendered images |
| Cache | Redis 7 | Rendered page image cache, job queuing |
| Authentication | Keycloak 24 | SSO, RBAC, OpenID Connect / OAuth2 |
| Containers | Docker / Docker Compose | All infrastructure services |

---

## Database — PostgreSQL

### Why PostgreSQL

PostgreSQL is the backbone of the system, chosen for:

- **ACID compliance** — essential for audit logs, workflow state transitions, and document
  metadata. When a document moves folders, updates metadata, and logs the action, all three
  must succeed or fail together.
- **JSONB support** — ideal for storing flexible workflow definitions and user-defined task
  configurations without a rigid schema.
- **Transactional integrity** — no partial writes in audit trails or workflow state.
- **Entity Framework Core** — first-class .NET support.
- **Shared instance** — both the application database and Keycloak share the same PostgreSQL
  server using separate databases, reducing operational overhead.

### Databases

| Database | Owner |
|---|---|
| `docmanager` | Application (documents, workflows, audit logs, permissions) |
| `keycloak` | Keycloak authentication server |

### Document Status Lifecycle

```sql
CREATE TYPE document_status AS ENUM (
    'uploading',    -- file transfer in progress
    'extracting',   -- Tika/PDFium extraction running
    'rendering',    -- pre-render job in progress
    'indexing',     -- Solr indexing in progress
    'ready',        -- fully processed, all tiers available
    'error'         -- something failed, check error log
);
```

### Audit Log Schema

```sql
CREATE TABLE audit_log (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    occurred_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    user_id       UUID NOT NULL,
    user_name     VARCHAR NOT NULL,
    action        VARCHAR NOT NULL,   -- 'document.view', 'document.delete' etc
    resource_type VARCHAR NOT NULL,   -- 'document', 'folder', 'workflow'
    resource_id   UUID NOT NULL,
    ip_address    INET,
    details       JSONB
);

CREATE INDEX idx_audit_user     ON audit_log(user_id, occurred_at DESC);
CREATE INDEX idx_audit_resource ON audit_log(resource_type, resource_id, occurred_at DESC);
```

### Permission Schema

```sql
-- Folder permissions
CREATE TABLE folder_permissions (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    folder_id      UUID NOT NULL REFERENCES folders(id),
    principal_id   UUID NOT NULL,
    principal_type VARCHAR(10) NOT NULL CHECK (principal_type IN ('user', 'group')),
    can_read       BOOLEAN NOT NULL DEFAULT false,
    can_write      BOOLEAN NOT NULL DEFAULT false,
    can_delete     BOOLEAN NOT NULL DEFAULT false,
    granted_by     UUID NOT NULL,
    granted_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Document permissions (overrides folder if set)
CREATE TABLE document_permissions (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id    UUID NOT NULL REFERENCES documents(id),
    principal_id   UUID NOT NULL,
    principal_type VARCHAR(10) NOT NULL CHECK (principal_type IN ('user', 'group')),
    can_read       BOOLEAN NOT NULL DEFAULT false,
    can_write      BOOLEAN NOT NULL DEFAULT false,
    can_delete     BOOLEAN NOT NULL DEFAULT false,
    granted_by     UUID NOT NULL,
    granted_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

---

## Full Text Search — Apache Solr

### Why Solr

| Feature | PostgreSQL FTS | Solr |
|---|---|---|
| Setup complexity | None (built-in) | Separate service |
| PDF content indexing | Limited | Excellent (Tika integration) |
| Faceted search | Basic | Very powerful |
| Fuzzy / phonetic search | Limited | Excellent |
| Scalability (millions of docs) | Moderate | Very high |
| .NET integration | EF Core native | SolrNet / HTTP API |

Solr's built-in **Tika integration** is a particularly strong reason to include it in a DMS —
it can extract and index text directly from PDFs, Word docs, and other formats out of the box.

### Solr as Source of Truth

Postgres is always the **source of truth**. Solr is always a **derived index**:

- If Solr goes down or needs a rebuild, nothing is lost.
- A `reindex` admin command replays all documents through the indexing pipeline from Postgres.
- Document ID in Postgres is the authoritative identifier; Solr stores a reference to it.

### Containerization

Solr runs in a container, eliminating most operational overhead:

```yaml
solr:
  image: solr:9
  ports:
    - "8983:8983"
  volumes:
    - solrdata:/var/solr
  command:
    - solr-precreate
    - documents
```

For production scale-out, **SolrCloud** mode runs natively in Kubernetes.

---

## Document Extraction — Apache Tika

### Running as a Standalone Container

Tika runs independently of Solr, allowing extraction without indexing:

```yaml
tika:
  image: apache/tika:latest
  ports:
    - "9998:9998"
```

### C# HTTP API Usage

```csharp
var client = new HttpClient();
var pdfBytes = File.ReadAllBytes("document.pdf");

// Extract text
var content = new ByteArrayContent(pdfBytes);
content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
var response = await client.PutAsync("http://tika:9998/tika", content);
var extractedText = await response.Content.ReadAsStringAsync();

// Extract metadata as JSON
var metaResponse = await client.PutAsync("http://tika:9998/meta", content);
var metadata = await metaResponse.Content.ReadAsStringAsync();
```

### Supported Formats

| Format | Support |
|---|---|
| DOCX / DOC | Excellent |
| RTF | Excellent |
| XLSX / XLS | Excellent |
| PPTX / PPT | Excellent |
| PDF | Good |
| ODT / ODS | Excellent |
| HTML / XML | Excellent |
| Email (MSG, EML) | Excellent |
| XPS | Partial — route through Gotenberg instead |

---

## Document Conversion — Gotenberg

### Purpose

Gotenberg is a purpose-built document conversion API with two rendering engines:

- **LibreOffice** — DOCX, XLSX, PPTX, RTF, XPS, ODF
- **Chromium** — HTML, URLs, Markdown, EML

Both output PDF, which then flows into PDFium for consistent image rendering.

```yaml
gotenberg:
  image: gotenberg/gotenberg:8
  ports:
    - "3000:3000"
  environment:
    - GOTENBERG_CHROMIUM_DISABLE_JAVASCRIPT=true
    - GOTENBERG_LIBREOFFICE_AUTO_START=true
```

### C# Usage

```csharp
using var form = new MultipartFormDataContent();
using var fileStream = File.OpenRead("document.xps");
form.Add(new StreamContent(fileStream), "files", "document.xps");

var response = await client.PostAsync(
    "http://gotenberg:3000/forms/libreoffice/convert", form);

var pdfBytes = await response.Content.ReadAsByteArrayAsync();
// Hand off to PDFium for rendering
```

---

## Document Rendering — PDFium

### Responsibilities

PDFium handles all PDF-specific operations:

| Task | Support |
|---|---|
| Extract text from PDF | Excellent |
| Extract images from PDF | Excellent |
| Read PDF metadata | Yes |
| Render PDF pages to images | Excellent |
| Barcode extraction (with ZXing) | Yes |
| Office formats | No — route through Gotenberg first |
| OCR scanned PDFs | No — route through Tesseract |

### Format Routing

| Input Format | Extraction Path | Rendering Path |
|---|---|---|
| PDF | PDFium | PDFium |
| DOCX / RTF / XLSX / PPTX | Tika | Gotenberg → PDFium |
| XPS | Gotenberg → PDFium | Gotenberg → PDFium |
| HTML / EML | Tika | Gotenberg (Chromium) → PDFium |
| JPEG / PNG / TIFF | ImageSharp | ImageSharp |
| Scanned PDF | PDFium → Tesseract (OCR) | PDFium |

### Format Router Pattern (C#)

```csharp
public interface IDocumentExtractor
{
    bool CanHandle(string mimeType);
    Task<ExtractionResult> ExtractAsync(Stream document);
}

public class ExtractionRouter
{
    private readonly IEnumerable<IDocumentExtractor> _extractors;

    public async Task<ExtractionResult> RouteAsync(Stream doc, string mimeType)
    {
        var extractor = _extractors.FirstOrDefault(e => e.CanHandle(mimeType))
            ?? throw new UnsupportedFormatException(mimeType);
        return await extractor.ExtractAsync(doc);
    }
}

// Implementations
public class PdfExtractor       : IDocumentExtractor { } // PDFium
public class TikaExtractor      : IDocumentExtractor { } // DOCX, RTF, etc.
public class GotenbergExtractor : IDocumentExtractor { } // XPS, etc.
public class OcrExtractor       : IDocumentExtractor { } // Scanned docs
```

---

## Pre-Rendering Strategy

### Why Pre-Render

- Document pages **never change** after ingest — there is no reason to pay the rendering
  cost more than once.
- All format complexity becomes a **one-time ingest cost** rather than a per-view cost.
- Every page view, regardless of original format, becomes a simple file fetch.
- Trades storage (cheap) for CPU time (expensive and slow).

### Render Tiers

| Tier | Width | DPI | Approx Size/Page | Use Case |
|---|---|---|---|---|
| Thumbnail | ~150px | 72 | 15–30 KB | Panel, grid browsing, search results |
| Preview | ~800px | 120 | 80–150 KB | Quick view, progressive load |
| Full | ~1920px | 150 | 200–400 KB | Main document viewer |
| Print | Native | 300 | 1–3 MB | On-demand only, not pre-rendered |

### Image Format

**WebP** is the recommended format — roughly 30–40% smaller than JPEG at equivalent quality,
with 95%+ modern browser support. PNG/JPEG are also acceptable for the Avalonia native client.

### MinIO Storage Layout

```
/documents
  /{documentId}
    /original           ← original uploaded file, immutable
    /pdf                ← normalized PDF (always generated)
    /pages
      /thumb            ← page_0001.webp, page_0002.webp ...
      /preview          ← page_0001.webp, page_0002.webp ...
      /full             ← page_0001.webp, page_0002.webp ...
```

### Pre-Render Pipeline (C#)

```csharp
public class PreRenderJobHandler
{
    public async Task HandleAsync(PreRenderJob job)
    {
        var pdfBytes = await _store.GetNormalizedPdfAsync(job.DocumentId);
        var semaphore = new SemaphoreSlim(4); // max 4 pages concurrent

        var tasks = Enumerable.Range(1, job.PageCount).Select(async pageNumber =>
        {
            await semaphore.WaitAsync();
            try   { await RenderPageAllTiersAsync(job.DocumentId, pdfBytes, pageNumber); }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks);

        await _repository.UpdateStatusAsync(job.DocumentId, DocumentStatus.Ready);
        await _hub.Clients.All.SendAsync("DocumentReady", job.DocumentId);
        await _jobQueue.EnqueueAsync(new SolrIndexJob { DocumentId = job.DocumentId });
    }
}
```

### Avalonia Viewer with Progressive Loading

```csharp
public async Task LoadPageAsync(int pageNumber)
{
    // Show preview tier immediately
    CurrentPageImage = await _api.GetPageImageAsync(
        DocumentId, pageNumber, RenderTier.Preview);

    // Swap in full resolution when loaded
    CurrentPageImage = await _api.GetPageImageAsync(
        DocumentId, pageNumber, RenderTier.Full);
}
```

---

## File Storage — MinIO

MinIO is an S3-compatible object store that runs on-premise in a container. It stores:

- Original uploaded documents (immutable)
- Normalized PDFs
- All pre-rendered page images (thumbnail / preview / full)

```yaml
minio:
  image: minio/minio:latest
  ports:
    - "9000:9000"
    - "9001:9001"   # web console
  volumes:
    - miniodata:/data
  environment:
    MINIO_ROOT_USER: admin
    MINIO_ROOT_PASSWORD: secret
  command: server /data --console-address ":9001"
```

MinIO is S3-compatible, so migrating to AWS S3 or Azure Blob Storage in the future requires
only a configuration change, not a code change.

---

## Caching — Redis

Redis provides two functions in the stack:

- **Rendered page cache** — hot pages served from memory, LRU eviction when memory limit reached
- **Job queue** — background jobs for pre-rendering and Solr indexing

```yaml
redis:
  image: redis:7-alpine
  ports:
    - "6379:6379"
  volumes:
    - redisdata:/data
  command: redis-server --maxmemory 512mb --maxmemory-policy allkeys-lru
```

---

## Authentication — Keycloak

### Why Keycloak

- **RBAC** — role-based access control for viewers, editors, admins, workflow managers
- **SSO** — single sign-on for enterprise integration
- **OpenID Connect / OAuth2** — first-class ASP.NET Core and Avalonia support
- **Fine-grained authorization** — document, folder, and workflow level control
- **Auth audit logging** — complements the document audit trail in Postgres
- **Containerized** — shares the existing Postgres instance (separate database)

### Realm Design

```
Realm: docmanager
  ├── Clients
  │     ├── docmanager-api        (ASP.NET Core backend)
  │     └── docmanager-avalonia   (Avalonia desktop client — PKCE flow)
  │
  ├── System Roles
  │     ├── admin
  │     ├── manager
  │     └── user
  │
  └── Document Roles
        ├── document:read
        ├── document:write
        ├── document:delete
        ├── workflow:execute
        ├── workflow:manage
        └── folder:manage
```

### ASP.NET Core Integration

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://keycloak:8080/realms/docmanager";
        options.Audience  = "docmanager-api";
        options.RequireHttpsMetadata = false; // true in production
        options.TokenValidationParameters = new TokenValidationParameters
        {
            RoleClaimType = "realm_access.roles"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly",           p => p.RequireRole("admin"));
    options.AddPolicy("CanManageWorkflows",  p => p.RequireRole("admin", "manager", "workflow:manage"));
    options.AddPolicy("CanReadDocuments",    p => p.RequireRole("admin", "manager", "user", "document:read"));
    options.AddPolicy("CanDeleteDocuments",  p => p.RequireRole("admin", "document:delete"));
});
```

### Avalonia PKCE Auth Flow

Avalonia uses the **Authorization Code + PKCE** flow (desktop-appropriate, no client secret):

```csharp
_oidcClient = new OidcClient(new OidcClientOptions
{
    Authority   = "http://keycloak:8080/realms/docmanager",
    ClientId    = "docmanager-avalonia",
    Scope       = "openid profile email roles offline_access",
    RedirectUri = "http://localhost:5000/callback",
    Browser     = new SystemBrowser(5000)
});
```

### Two-Layer Permission Model

```
Layer 1 — Keycloak
  Authentication, SSO, system-wide roles (admin / manager / user)

Layer 2 — PostgreSQL
  Folder permissions, document permissions, workflow access
  Checked per-request by the API after Keycloak token validation
```

---

## UI — Avalonia

### Why Avalonia

- **Cross-platform** — Windows, Linux, macOS from a single codebase
- **Skia-based rendering** — fast, high-quality native image display
- **MVVM first-class** — pairs naturally with ReactiveUI
- **In-process PDFium** — can call PDFium directly without HTTP, for faster page rendering
- **SignalR support** — real-time workflow notifications via `Microsoft.AspNetCore.SignalR.Client`

### Key NuGet Packages

```xml
<PackageReference Include="Avalonia"                              Version="11.*" />
<PackageReference Include="Avalonia.Desktop"                     Version="11.*" />
<PackageReference Include="Avalonia.ReactiveUI"                  Version="11.*" />
<PackageReference Include="ReactiveUI"                           Version="20.*" />
<PackageReference Include="ReactiveUI.Fody"                      Version="19.*" />
<PackageReference Include="PDFtoImage"                           Version="4.*"  />
<PackageReference Include="IdentityModel.OidcClient"             Version="6.*"  />
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client"  Version="8.*"  />
<PackageReference Include="Refit"                                Version="7.*"  />
<PackageReference Include="SixLabors.ImageSharp"                 Version="3.*"  />
```

### Document Viewer Component Structure

```
DocumentViewer/
  ├── DocumentViewerView.axaml          # Page display, zoom, scroll
  ├── DocumentViewerViewModel.cs        # Page navigation, zoom state
  ├── ThumbnailPanelView.axaml          # Left panel thumbnails
  ├── ThumbnailPanelViewModel.cs        # Lazy thumbnail loading
  ├── DocumentToolbarView.axaml         # Zoom, rotate, search bar
  └── DocumentToolbarViewModel.cs
```

### SignalR Real-Time Notifications

```csharp
// Server pushes workflow events to connected Avalonia clients
await hubContext.Clients.User(userId).SendAsync("DocumentProcessed", new
{
    DocumentId = doc.Id,
    Status     = "Indexed",
    FolderPath = doc.FolderPath,
    PageCount  = doc.PageCount
});

// Avalonia receives and updates UI on UI thread
_hubConnection.On<DocumentProcessedEvent>("DocumentProcessed", evt =>
{
    Dispatcher.UIThread.Post(() =>
    {
        Documents.First(d => d.Id == evt.DocumentId).UpdateStatus(evt.Status);
    });
});
```

---

## Workflow Engine

### Overview

The workflow engine allows users to define automated tasks that fire when documents are
ingested or updated. Examples include:

- Moving documents to folders based on barcode values
- Tagging documents based on extracted metadata
- Notifying users when specific document types arrive
- Triggering external system integrations

### Two-Phase Ingest Pipeline

```
Phase 1 — Ingest (synchronous, immediate)
  Document arrives
    → PDFium / Tika extracts text + metadata
    → Barcode values extracted (PDFium + ZXing)
    → Everything written to Postgres (ACID)
    → Workflow rules fire (move folders, tag, notify)

Phase 2 — Background (asynchronous, queued via Redis)
  → Pre-render all page tiers → MinIO
  → Update Postgres status to 'ready'
  → Notify Avalonia clients via SignalR
  → Index content to Solr
```

### Full Ingest Pipeline (C#)

```csharp
public class DocumentIngestionPipeline
{
    public async Task IngestAsync(IngestRequest request)
    {
        var documentId = Guid.NewGuid();

        // 1. Store original
        await _store.SaveOriginalAsync(documentId, request.FileBytes, request.FileName);

        // 2. Normalize to PDF
        var pdfBytes = await NormalizeToPdfAsync(request.FileBytes, request.MimeType);
        await _store.SaveNormalizedPdfAsync(documentId, pdfBytes);

        // 3. Extract text + metadata
        var extraction = await _extractor.ExtractAsync(pdfBytes, request.MimeType);

        // 4. Write to Postgres (ACID)
        await _repository.CreateDocumentAsync(new Document
        {
            Id            = documentId,
            FileName      = request.FileName,
            MimeType      = request.MimeType,
            PageCount     = extraction.PageCount,
            ExtractedText = extraction.Text,
            Metadata      = extraction.Metadata,
            Status        = DocumentStatus.Rendering
        });

        // 5. Queue pre-render and indexing jobs
        await _jobQueue.EnqueueAsync(new PreRenderJob
        {
            DocumentId = documentId,
            PageCount  = extraction.PageCount
        });

        // 6. Fire workflow rules immediately
        await _workflowEngine.ProcessAsync(documentId, extraction);
    }
}
```

---

## Full Docker Compose Stack

```yaml
services:

  postgres:
    image: postgres:16
    volumes:
      - pgdata:/var/lib/postgresql/data
    ports:
      - "5432:5432"
    environment:
      POSTGRES_USER: appuser
      POSTGRES_PASSWORD: secret
      POSTGRES_MULTIPLE_DATABASES: docmanager,keycloak

  keycloak:
    image: quay.io/keycloak/keycloak:24
    ports:
      - "8080:8080"
    environment:
      KEYCLOAK_ADMIN: admin
      KEYCLOAK_ADMIN_PASSWORD: secret
      KC_DB: postgres
      KC_DB_URL: jdbc:postgresql://postgres:5432/keycloak
      KC_DB_USERNAME: appuser
      KC_DB_PASSWORD: secret
    command: start-dev
    depends_on:
      - postgres

  tika:
    image: apache/tika:latest
    ports:
      - "9998:9998"

  gotenberg:
    image: gotenberg/gotenberg:8
    ports:
      - "3000:3000"
    environment:
      - GOTENBERG_CHROMIUM_DISABLE_JAVASCRIPT=true
      - GOTENBERG_LIBREOFFICE_AUTO_START=true

  solr:
    image: solr:9
    ports:
      - "8983:8983"
    volumes:
      - solrdata:/var/solr
    command:
      - solr-precreate
      - documents

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redisdata:/data
    command: redis-server --maxmemory 512mb --maxmemory-policy allkeys-lru

  minio:
    image: minio/minio:latest
    ports:
      - "9000:9000"
      - "9001:9001"
    volumes:
      - miniodata:/data
    environment:
      MINIO_ROOT_USER: admin
      MINIO_ROOT_PASSWORD: secret
    command: server /data --console-address ":9001"

volumes:
  pgdata:
  solrdata:
  redisdata:
  miniodata:
```

---

## Complete Architecture Diagram

```
┌──────────────────────────────────────────────────────┐
│                 Avalonia Desktop Client               │
│                                                      │
│  ┌──────────────────┐   ┌───────────────────────┐   │
│  │  Document Viewer  │   │  Workflow / Admin UI  │   │
│  │  (PDFium direct) │   │  (SignalR real-time)  │   │
│  └──────────────────┘   └───────────────────────┘   │
│                                                      │
│  ┌──────────────────┐   ┌───────────────────────┐   │
│  │  Keycloak Login  │   │  Search UI (Solr)     │   │
│  │  (PKCE / OIDC)  │   │                       │   │
│  └──────────────────┘   └───────────────────────┘   │
└────────────────────┬─────────────────────────────────┘
                     │  REST / gRPC / SignalR
┌────────────────────▼─────────────────────────────────┐
│              ASP.NET Core API (.NET 10)               │
│         Workflow Engine — Format Router               │
│         Document Ingestion Pipeline                   │
└───┬──────────┬──────────┬──────────┬─────────────────┘
    │          │          │          │
    ▼          ▼          ▼          ▼
┌───────┐ ┌───────┐ ┌─────────┐ ┌──────────┐
│Postgres│ │ Solr  │ │  Tika   │ │Gotenberg │
│(ACID) │ │Search │ │Extract  │ │ Convert  │
└───┬───┘ └───────┘ └─────────┘ └──────────┘
    │
    ├──────────────┐
    ▼              ▼
┌───────┐     ┌───────┐
│ Redis │     │ MinIO │
│ Cache │     │ Store │
│ Queue │     │       │
└───────┘     └───────┘
    │              │
    └──────┬────────┘
           ▼
    ┌────────────┐
    │  Keycloak  │
    │    Auth    │
    └────────────┘
```

### Data Flow Summary

```
Document arrives at API
  │
  ├── Save original                     → MinIO
  ├── Normalize to PDF                  → Gotenberg / PDFium
  ├── Extract text + metadata           → Tika / PDFium
  ├── Extract barcodes                  → PDFium + ZXing
  ├── Write metadata + text to Postgres → ACID guaranteed
  ├── Fire workflow rules               → folder routing, tagging, notifications
  │
  └── Background jobs (Redis queue):
        ├── Pre-render pages (thumb / preview / full)  → MinIO
        ├── Update Postgres status → 'ready'
        ├── Notify Avalonia clients                    → SignalR
        └── Index content to Solr                     → async
```

---

## Production Considerations

### Scaling

- **Single server** — Docker Compose is sufficient for small/medium deployments
- **Multi-server** — Kubernetes with SolrCloud for horizontal Solr scaling
- **Managed Postgres** — consider Azure Database for PostgreSQL or AWS RDS in production
  to offload backups, failover, and patching (MinIO, Solr, Redis, Tika, Gotenberg remain
  containerized)

### Security Checklist

- Enable HTTPS for all services in production (`KC_HOSTNAME`, API, MinIO)
- Set `RequireHttpsMetadata = true` in ASP.NET Core JWT configuration
- Rotate all default passwords (Postgres, MinIO, Keycloak admin)
- Use Docker secrets or a vault (HashiCorp Vault / Azure Key Vault) for credentials
- Enable Keycloak brute-force protection and MFA for admin accounts

### Reindex Strategy

Since Solr is a derived index, always maintain a reindex path:

```
Admin triggers reindex
  → Fetch all document IDs from Postgres
  → For each document, replay text content through Solr indexing pipeline
  → Solr is fully rebuilt without any data loss
```

---

*Document generated from architecture design session — Solarch DMS Project*
