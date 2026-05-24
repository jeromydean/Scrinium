<p align="center">
  <img src="logo.png" alt="Scrinium logo" width="220" />
</p>

<p align="center">
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" /></a>
  <a href="https://avaloniaui.net/"><img src="https://img.shields.io/badge/UI-Avalonia-7B68EE?logo=dotnet&logoColor=white" alt="Avalonia" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green" alt="MIT License" /></a>
  <img src="https://img.shields.io/badge/status-work%20in%20progress-yellow" alt="Work in progress" />
</p>

# Scrinium

Enterprise document management system.

> **🚧 Work in progress** — Scrinium is in early development. The [architecture](docs/ARCHITECTURE.md) describes the target platform; most infrastructure (Postgres, Solr, Keycloak, MinIO, workers) and product features are not implemented yet. What exists today is a starter API (health check, document ingestion queue) and an Avalonia desktop shell.

| | |
|---|---|
| 📦 | **Archiving** — immutable storage, normalized PDFs, tiered page renders |
| ⚙️ | **Workflows** — rules on ingest (routing, tagging, notifications) |
| 🔍 | **Search** — full-text discovery via Apache Solr |

Built on **.NET 10**, **PostgreSQL**, **Apache Solr**, **Keycloak**, and supporting services (Tika, Gotenberg, MinIO, Redis, and others).

## 📐 Architecture

See **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** for system diagrams, ingest pipeline, data models, auth model, and production notes.

## 📂 Repository layout

| | Path | Description |
|---|------|-------------|
| 🌐 | `src/Scrinium.Api/` | ASP.NET Core Web API (.NET 10) — ingestion queue, health, OpenAPI |
| 🖥️ | `src/Scrinium/` | Avalonia desktop shell (.NET 10), MVVM starter |
| 📐 | `docs/ARCHITECTURE.md` | Architecture and design reference |

Planned projects (`Scrinium.Core`, `Scrinium.Infrastructure`, `Scrinium.Workers`) are described in the architecture doc.

## 🚀 Getting started

**Prerequisites:** [.NET SDK](https://dotnet.microsoft.com/download) matching `net10.0`, and a desktop OS supported by [Avalonia](https://avaloniaui.net/).

```bash
cd src
dotnet build

# API (http://localhost:5243, /health)
dotnet run --project Scrinium.Api/Scrinium.Api.csproj

# Desktop client
dotnet run --project Scrinium/Scrinium.csproj
```

> 🐳 **Full stack** — Postgres, Solr, Keycloak, MinIO, and related services will be documented when `docker-compose` is added. See [Local infrastructure](docs/ARCHITECTURE.md#local-infrastructure) in the architecture doc.

## 📄 License

See [LICENSE](LICENSE) (MIT).
