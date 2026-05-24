# Scrinium — Architecture

Design reference for the Scrinium document management system. Implementation is in progress; details may change as the codebase grows.

## Table of contents

1. [Overview](#overview)
2. [Technology stack](#technology-stack)
3. [System diagram](#system-diagram)
4. [Responsibility split](#responsibility-split)
5. [Data flow](#data-flow)
6. [Format routing](#format-routing)
7. [Pre-rendering and storage](#pre-rendering-and-storage)
8. [PostgreSQL](#postgresql)
9. [Keycloak](#keycloak)
10. [Apache Solr](#apache-solr)
11. [Supporting services](#supporting-services)
12. [Workflow engine](#workflow-engine)
13. [Local infrastructure](#local-infrastructure)
14. [Solution layout](#solution-layout)
15. [Open design decisions](#open-design-decisions)
16. [Production](#production)

---

## Overview

Scrinium is built around three pillars:

- **Document storage and archiving** — immutable originals, normalized PDFs, tiered page renders in object storage
- **Workflow automation** — rules on ingest (barcodes, metadata, routing, notifications)
- **Full-text search** — Apache Solr/Lucene for discovery across extracted content

Clients (Avalonia desktop first) talk only to the ASP.NET Core API. The API orchestrates PostgreSQL, MinIO, Redis, Keycloak, and background processing (Tika, Gotenberg, PDFium, Solr).

---

## Technology stack

| Component | Technology | Purpose |
|-----------|------------|---------|
| Language / runtime | C# [.NET 10](https://dotnet.microsoft.com/) | API, workers, desktop client |
| UI | [Avalonia](https://avaloniaui.net/) | Cross-platform desktop (Windows, Linux, macOS) |
| Database | PostgreSQL 16 | Metadata, ACLs, workflows, audit (ACID); [EF Core](https://learn.microsoft.com/ef/core/) |
| Full-text search | Apache Solr 9 | Content indexing, faceted and fuzzy search |
| Text extraction | Apache Tika | Office, email, HTML, etc. — metadata and text |
| Conversion | Gotenberg 8 | Normalize formats to PDF (LibreOffice + Chromium) |
| PDF operations | PDFium (+ ZXing) | Text/images from PDF, page render, barcodes |
| Images | ImageSharp | JPEG, PNG, TIFF viewing path |
| OCR | Tesseract | Scanned PDFs (when needed) |
| Object storage | MinIO (S3-compatible) | Originals, normalized PDFs, pre-rendered pages |
| Cache / queue | Redis 7 | Hot page cache, background job queue |
| Authentication | Keycloak 24 | SSO, RBAC, OIDC/OAuth2 |
| Real-time | SignalR | Processing status, workflow notifications to clients |
| Infrastructure | Docker / Docker Compose | Local and on-prem service orchestration |

---

## System diagram

```
┌─────────────────────────────────────────────────────────┐
│              Avalonia desktop client                     │
│  Document viewer · Search · Workflow/admin · OIDC (PKCE) │
│  (PDFium in-process where useful · SignalR client)       │
└──────────────────────────┬──────────────────────────────┘
                           │ REST / gRPC / SignalR
┌──────────────────────────▼──────────────────────────────┐
│           ASP.NET Core API (.NET 10)                   │
│  Ingestion pipeline · format router · workflow engine  │
└──┬────────┬────────┬────────┬────────┬──────────────────┘
   │        │        │        │        │
   ▼        ▼        ▼        ▼        ▼
Postgres  Solr    Tika   Gotenberg  Keycloak
   │                              (OIDC)
   ├──────────────┐
   ▼              ▼
 Redis          MinIO
 (cache/queue)  (blobs)
```

### End-to-end data flow

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
        └── Index content to Solr                      → async
```

---

## Responsibility split

| Layer | Responsibility |
|-------|----------------|
| **Keycloak** | Authentication, SSO, system-wide roles (`admin`, `manager`, `user`), coarse document roles (`document:read`, `workflow:execute`, etc.) |
| **PostgreSQL** | Source of truth — folder/document ACL, versions, workflow state, audit. Checked on every API request after JWT validation |
| **MinIO** | Immutable originals, normalized PDF, pre-rendered page images (S3 API — swap to AWS/Azure via config) |
| **Solr** | Derived search index only; rebuild from Postgres via admin reindex |
| **Redis** | LRU cache for hot rendered pages; job queue for pre-render and Solr indexing |
| **Tika / Gotenberg / PDFium** | Extraction and normalization; format-specific routing |

Solr is never authoritative. If Solr is lost or corrupted, replay indexing from Postgres — document IDs in Postgres are the stable identifiers.

### Two-layer permissions

```
Layer 1 — Keycloak
  Authentication, SSO, system-wide and coarse document roles

Layer 2 — PostgreSQL
  Folder permissions, document permissions, workflow access
  Enforced per request by the API after JWT validation
```

---

## Data flow

### Two-phase ingest

**Phase 1 — ingest (synchronous, immediate)**

1. Save original → MinIO
2. Normalize to PDF → Gotenberg and/or PDFium
3. Extract text, metadata, barcodes → Tika / PDFium / ZXing
4. Write document row and extraction results to Postgres (ACID)
5. Run workflow rules (folder routing from barcodes, tags, notifications)

**Phase 2 — background (async via Redis queue)**

1. Pre-render page tiers → MinIO
2. Update Postgres status → `ready`
3. Notify clients → SignalR
4. Index content → Solr

### Document status lifecycle

```sql
CREATE TYPE document_status AS ENUM (
    'uploading',    -- file transfer in progress
    'extracting',   -- Tika / PDFium extraction
    'rendering',    -- pre-render job in progress
    'indexing',     -- Solr indexing
    'ready',        -- fully processed
    'error'         -- failed; see error log
);
```

---

## Format routing

| Input | Extraction | Rendering / preview |
|-------|------------|---------------------|
| PDF | PDFium | PDFium |
| DOCX, RTF, XLSX, PPTX, ODT, etc. | Tika | Gotenberg → PDFium |
| XPS | Gotenberg → PDFium | Gotenberg → PDFium |
| HTML, EML | Tika | Gotenberg (Chromium) → PDFium |
| JPEG, PNG, TIFF | ImageSharp | ImageSharp |
| Scanned PDF | PDFium → Tesseract (OCR) | PDFium |

XPS and similar edge cases go through Gotenberg rather than Tika alone.

The API uses a **format router** pattern: implementations of a common extractor interface (`PdfExtractor`, `TikaExtractor`, `GotenbergExtractor`, `OcrExtractor`, etc.) selected by MIME type.

---

## Pre-rendering and storage

### Rationale

Document pages do not change after ingest. Rendering is a **one-time ingest cost**; every view is a simple fetch from object storage. This trades storage (cheap) for CPU (expensive at view time).

### Render tiers

| Tier | Width | DPI | Use case |
|------|-------|-----|----------|
| Thumbnail | ~150px | 72 | Grid, search results |
| Preview | ~800px | 120 | Quick view, progressive load |
| Full | ~1920px | 150 | Main viewer |
| Print | Native | 300 | On-demand only (not pre-rendered) |

Prefer **WebP** for pre-rendered tiers (~30–40% smaller than JPEG at similar quality). The Avalonia client loads the preview tier first, then swaps to full resolution.

### MinIO layout (per document)

```
/documents/{documentId}/
  original/          ← uploaded file (immutable)
  pdf/               ← normalized PDF
  pages/thumb/       ← page_0001.webp, ...
  pages/preview/
  pages/full/
```

MinIO is S3-compatible; production can point the same abstraction at AWS S3 or Azure Blob with configuration only.

---

## PostgreSQL

### Why PostgreSQL

- **ACID** — audit logs, workflow transitions, and metadata updates succeed or fail together
- **JSONB** — flexible workflow definitions and extensible metadata
- **EF Core** — first-class .NET integration
- **Shared instance** — application DB and Keycloak DB on one server (separate databases)

### Databases (shared server)

| Database | Owner |
|----------|--------|
| `scrinium` (name TBD) | Application |
| `keycloak` | Keycloak |

### Core schemas (target)

**Audit log** — append-only:

```sql
CREATE TABLE audit_log (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    occurred_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    user_id       UUID NOT NULL,
    user_name     VARCHAR NOT NULL,
    action        VARCHAR NOT NULL,   -- e.g. 'document.view', 'document.delete'
    resource_type VARCHAR NOT NULL,   -- 'document', 'folder', 'workflow'
    resource_id   UUID NOT NULL,
    ip_address    INET,
    details       JSONB
);

CREATE INDEX idx_audit_user     ON audit_log(user_id, occurred_at DESC);
CREATE INDEX idx_audit_resource ON audit_log(resource_type, resource_id, occurred_at DESC);
```

**Permissions** — folder-level and document-level (document overrides folder when set):

```sql
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

Authoritative DDL will live in EF Core migrations as the API project is added.

---

## Keycloak

### Realm design (target)

```
Realm: scrinium
  ├── Clients
  │     ├── scrinium-api        (ASP.NET Core — confidential)
  │     └── scrinium-avalonia   (desktop — public, PKCE)
  │
  ├── System roles
  │     ├── admin
  │     ├── manager
  │     └── user
  │
  └── Document roles (examples)
        ├── document:read
        ├── document:write
        ├── document:delete
        ├── workflow:execute
        ├── workflow:manage
        └── folder:manage
```

- **API**: JWT validation via `Microsoft.AspNetCore.Authentication.JwtBearer` (`Authority`, `Audience`, JWKS). Policies map realm roles; per-document ACL comes from Postgres.
- **Desktop**: Authorization Code + PKCE (`IdentityModel.OidcClient`), system browser, tokens in OS credential storage.
- **Workers**: Client credentials or dedicated service accounts — not end-user refresh tokens.

---

## Apache Solr

| Concern | Notes |
|---------|--------|
| Role | Derived index for full text, facets, fuzzy/phonetic search |
| Dev setup | `solr:9`, core `documents` via `solr-precreate` |
| .NET | SolrNet or HTTP API |
| Tika | Solr can index many formats; standalone Tika still used for extraction pipeline control |
| Scale-out | SolrCloud on Kubernetes for large deployments |
| Recovery | Admin reindex: load all document IDs from Postgres, replay indexing pipeline |

---

## Supporting services

### Apache Tika

Runs as a standalone container (`apache/tika`, port 9998). HTTP API for text (`/tika`) and metadata (`/meta`). Used when extraction should happen without Solr indexing.

### Gotenberg

LibreOffice (office formats, XPS, ODF) and Chromium (HTML, EML, Markdown) → PDF. Environment flags in dev: disable Chromium JS, auto-start LibreOffice.

### PDFium

PDF text, images, metadata, page rasterization, barcodes (with ZXing). Office formats route through Gotenberg first.

### Redis

- Rendered page cache (LRU, e.g. `--maxmemory 512mb --maxmemory-policy allkeys-lru`)
- Job queue for pre-render and Solr indexing

---

## Workflow engine

User-defined rules on ingest or update, for example:

- Move documents to folders based on barcode values
- Tag from extracted metadata
- Notify when specific document types arrive
- Trigger external integrations

Rules run in **phase 1** after extraction so routing and tagging use consistent Postgres state. Background jobs do not gate workflow decisions that need immediate consistency.

---

## Local infrastructure

Target Docker Compose services (file to be added to the repo):

| Service | Image (indicative) | Port |
|---------|-------------------|------|
| PostgreSQL | `postgres:16` | 5432 |
| Keycloak | `quay.io/keycloak/keycloak:24` | 8080 |
| Tika | `apache/tika:latest` | 9998 |
| Gotenberg | `gotenberg/gotenberg:8` | 3000 |
| Solr | `solr:9` | 8983 |
| Redis | `redis:7-alpine` | 6379 |
| MinIO | `minio/minio:latest` | 9000, 9001 (console) |

Example Solr service definition:

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

---

## Solution layout

| Project | Status | Purpose |
|---------|--------|---------|
| `Scrinium.Api` | In repo | ASP.NET Core Web API — health, OpenAPI (dev); JWT, ingest, search, SignalR planned |
| `Scrinium` | In repo | Avalonia desktop UI (thin client over API) |
| `Scrinium.Core` | Planned | Domain models, ACL, workflow rules, format router |
| `Scrinium.Infrastructure` | Planned | EF Core, MinIO, Solr, Redis, optional Keycloak admin |
| `Scrinium.Workers` | Planned | Pre-render, indexing, Tika/Gotenberg orchestration |

**Planned desktop packages** — ReactiveUI, `IdentityModel.OidcClient`, SignalR client, Refit, PDFtoImage (PDFium), ImageSharp.

**Document viewer (UI)** — separate views/view models for main viewer, thumbnail panel, toolbar; SignalR handlers update status on the UI thread.

---

## Open design decisions

- Application database name (`scrinium` vs legacy prototype names)
- Multi-tenancy (`tenant_id` vs schema-per-tenant vs DB-per-tenant)
- Versioning (immutable blob per version vs history table)
- Search scope (all versions vs latest; trash/recycle)
- Keycloak group → Postgres principal sync (on login vs periodic vs event-driven)
- Compliance (retention, legal hold, export)

---

## Production

### Scaling

- **Small/medium** — Docker Compose on one or few hosts
- **Search at scale** — Kubernetes + SolrCloud
- **Database** — managed Postgres (RDS, Azure Database) for backups and failover; MinIO, Solr, Redis, Tika, Gotenberg remain containerized or managed per environment

### Security

- HTTPS for API, Keycloak, MinIO (`RequireHttpsMetadata = true` for JWT)
- Rotate all default passwords; use secrets manager or vault for credentials
- Keycloak brute-force protection and MFA for admin accounts
- Document ACL enforced in API from Postgres, not inferred from Solr alone

### Solr reindex

```
Admin triggers reindex
  → Fetch all document IDs from Postgres
  → For each document, replay text/content through Solr indexing pipeline
  → Index rebuilt with no loss of source data
```
