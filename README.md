# Scrinium

Enterprise document management system. The name comes from Latin (*scrinium* — a chest or case for storing documents).

Scrinium combines **document archiving**, **workflow automation**, and **full-text search** in a cross-platform desktop-first stack built on .NET 10, PostgreSQL, Apache Solr, Keycloak, and supporting services (Tika, Gotenberg, MinIO, Redis, and others).

**Architecture and design** — see **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** for system diagrams, ingest pipeline, data models, auth model, and production notes.

## Repository layout

| Path | Description |
|------|-------------|
| `src/Scrinium/` | Avalonia desktop shell (.NET 10), MVVM starter |
| `docs/ARCHITECTURE.md` | Architecture and design reference |

Planned projects (`Scrinium.Api`, `Scrinium.Core`, `Scrinium.Infrastructure`, `Scrinium.Workers`) are described in the architecture doc.

## Getting started

**Prerequisites:** [.NET SDK](https://dotnet.microsoft.com/download) matching `net10.0`, and a desktop OS supported by [Avalonia](https://avaloniaui.net/).

```bash
cd src
dotnet build
dotnet run --project Scrinium/Scrinium.csproj
```

Running the full platform (Postgres, Solr, Keycloak, MinIO, etc.) will be documented when `docker-compose` is added; see [docs/ARCHITECTURE.md#local-infrastructure](docs/ARCHITECTURE.md#local-infrastructure).

## License

See [LICENSE](LICENSE).
