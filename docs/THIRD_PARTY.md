# Third-party components and licenses

Curated inventory of **direct** dependencies Scrinium uses today (NuGet packages and Docker/runtime services).  
This is an engineering checklist, **not legal advice**. Re-verify SPDX/license text when upgrading versions or changing distribution plans.

**Last reviewed:** 2026-08-01

## How to maintain this

| When | Action |
|------|--------|
| Add a NuGet package | Add a row under [Direct NuGet packages](#direct-nuget-packages) |
| Add a Compose service / image | Add a row under [Runtime services](#runtime-services-docker-compose) |
| Bump a “watch closely” component | Re-check its license page and update the **Notes** cell |
| Approach customer distribution / compliance | Generate an SBOM (`dotnet` / container tooling) for the full transitive tree |

Transitive NuGet packages (EF Core, SkiaSharp, etc.) are **not** fully listed here yet.

## Watch closely

| Component | Why |
|-----------|-----|
| **MinIO server** (`minio/minio`) | Community source historically **AGPLv3**; commercial product (AIStor) is proprietary. Evaluate AGPL obligations for how you ship/operate Scrinium. App code talks S3 via `IBlobStore` — [RustFS](https://github.com/rustfs/rustfs) (**Apache-2.0**) is a candidate swap. |
| **MinIO .NET SDK** (`Minio` NuGet) | **Apache-2.0** — separate from the server license. Can talk to any S3-compatible store. |
| **Redis** (`redis:7-*`) | Redis moved from BSD to dual **RSALv2 / SSPLv1** (from 7.4). Pin an explicitly reviewed tag, or consider **Valkey** (BSD) if source-available terms are a problem. |

---

## Runtime services (Docker Compose)

Sources: [`docker-compose.yml`](../docker-compose.yml), [`docker/README.md`](../docker/README.md).

| Service | Image (as composed) | Role | License (typical) | Notes |
|---------|---------------------|------|-------------------|--------|
| PostgreSQL | `postgres:18` | Primary DB | [PostgreSQL License](https://www.postgresql.org/about/licence/) | Permissive, BSD-like |
| pgAdmin | `dpage/pgadmin4:9` | Dev DB UI | [PostgreSQL License](https://www.pgadmin.org/licence/) | Dev-only tooling |
| Keycloak | `quay.io/keycloak/keycloak:26.7` | OIDC / IdP | [Apache-2.0](https://github.com/keycloak/keycloak/blob/main/LICENSE.txt) | |
| Apache Solr | `solr:10` | Full-text search | [Apache-2.0](https://www.apache.org/licenses/LICENSE-2.0) | |
| Redis | `redis:7-alpine` | Job streams / cache | [RSALv2 / SSPLv1](https://redis.io/legal/licenses/) (recent 7.x) | See [Watch closely](#watch-closely); confirm exact tag |
| MinIO | `minio/minio:latest` | Object storage (S3 API) | [AGPLv3](https://github.com/minio/minio/blob/master/LICENSE) (community); commercial AIStor separate | Prefer pinned tags in prod; RustFS alternative |
| Apache Tika | `apache/tika:latest` | Metadata / text extract | [Apache-2.0](https://github.com/apache/tika/blob/main/LICENSE) | Prefer pinned tags |
| Gotenberg | `gotenberg/gotenberg:8` | Office/HTML → PDF | [MIT](https://github.com/gotenberg/gotenberg/blob/main/LICENSE) | Bundles LibreOffice / Chromium — those have their own licenses |

### Object-storage alternatives (not composed yet)

| Component | License | Notes |
|-----------|---------|--------|
| [RustFS](https://github.com/rustfs/rustfs) | Apache-2.0 | S3-compatible; app already abstracted behind `IBlobStore` |
| Amazon S3 / other S3 APIs | Vendor terms | Swap adapter / endpoint config as needed |

---

## Direct NuGet packages

Grouped by project. Versions are those declared in `.csproj` files as of the review date.

### Scrinium.Infrastructure

| Package | Version | Role | License (typical) |
|---------|---------|------|-------------------|
| Microsoft.EntityFrameworkCore | 10.0.10 | ORM | MIT |
| Microsoft.EntityFrameworkCore.Design | 10.0.10 | Migrations tooling | MIT |
| Microsoft.Extensions.* (DI, Hosting, Logging, Http, Options) | 10.0.10 | Hosting / options | MIT |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | Postgres provider | [PostgreSQL License](https://www.npgsql.org/license.html) |
| Minio | 7.0.0 | S3 client used by `MinioBlobStore` | [Apache-2.0](https://github.com/minio/minio-dotnet/blob/master/LICENSE) |
| StackExchange.Redis | 3.1.0 | Redis client | MIT |
| PDFtoImage | 5.3.0 | PDF rasterization (PDFium) | MIT (PDFium: BSD-3 / Apache-2.0) |
| PdfPig | 0.1.15 | PDF text / metadata | Apache-2.0 |
| ZXing.Net | 0.16.11 | Barcode decode | Apache-2.0 |
| ZXing.Net.Bindings.SkiaSharp | 0.16.22 | SkiaSharp bridge for ZXing | Apache-2.0 |
| System.Security.Cryptography.Xml | 10.0.10 | Crypto XML (framework) | MIT |

### Scrinium.Api

| Package | Version | Role | License (typical) |
|---------|---------|------|-------------------|
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.10 | JWT auth | MIT |
| Microsoft.AspNetCore.OpenApi | 10.0.10 | OpenAPI | MIT |
| Microsoft.OpenApi | 2.3.9 | OpenAPI model | MIT |
| Microsoft.EntityFrameworkCore.Design | 10.0.10 | Design-time EF | MIT |

### Scrinium.Workers

| Package | Version | Role | License (typical) |
|---------|---------|------|-------------------|
| Microsoft.EntityFrameworkCore.Relational | 10.0.10 | EF relational | MIT |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.10 | Hosted services | MIT |
| Microsoft.Extensions.Logging.Abstractions | 10.0.10 | Logging | MIT |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.10 | Options binding | MIT |

### Scrinium (Avalonia desktop)

| Package | Version | Role | License (typical) |
|---------|---------|------|-------------------|
| Avalonia | 12.1.1 | UI framework | MIT |
| Avalonia.Desktop | 12.1.1 | Desktop host | MIT |
| Avalonia.Themes.Fluent | 12.1.1 | Fluent theme | MIT |
| Avalonia.Fonts.Inter | 12.1.1 | Inter font package | MIT (font: SIL OFL) |
| Avalonia.Diagnostics | 11.3.18 | Dev diagnostics | MIT (Debug only) |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM helpers | MIT |
| Microsoft.Extensions.Configuration.* | 10.0.0 | Config | MIT |

### Avalonia.WebView.Extensions

| Package | Version | Role | License (typical) |
|---------|---------|------|-------------------|
| Avalonia | 12.1.1 | UI | MIT |
| Avalonia.Controls.WebView | 12.0.1 | Embedded browser (OIDC) | Verify on NuGet / vendor page (Avalonia Accelerate packaging) |

### Scrinium.IngestionClient

| Package | Version | Role | License (typical) |
|---------|---------|------|-------------------|
| Microsoft.Extensions.Hosting | 10.0.0 | Worker host | MIT |
| Microsoft.Extensions.Http | 10.0.0 | HttpClient factory | MIT |
| Microsoft.Extensions.Configuration.Binder | 10.0.0 | Options binding | MIT |

### Scrinium.Core

No direct third-party NuGet packages (project references only).

---

## Planned / mentioned but not direct deps yet

From [ARCHITECTURE.md](ARCHITECTURE.md) — track when introduced:

| Component | Likely role | Typical license to confirm |
|-----------|-------------|----------------------------|
| Tesseract | OCR | Apache-2.0 |
| ImageSharp | Image decode/process | Apache-2.0 (Six Labors; confirm current dual-license / commercial terms) |
| SharpCompress | Package (zip/7z) extract | MIT |

---

## Abstraction notes (license flexibility)

- **Object storage:** domain/workers use `IBlobStore`. Swapping MinIO → RustFS / S3 should not require domain changes; may replace `MinioBlobStore` and/or point an S3 client at a new endpoint.
- **Queue / cache:** Redis usage is behind `IJobQueue` (and related Infrastructure). Valkey is usually API-compatible for basic Redis protocols.
- **Auth:** Keycloak is an external OIDC provider; clients use standard OAuth2/OIDC, not Keycloak-specific SDKs for core auth.

---

## Related docs

- [Architecture](ARCHITECTURE.md)
- [Archive / Sheet / Bundle model](ARCHIVE_SHEET_BUNDLE_MODEL.md)
- [Docker README](../docker/README.md)
