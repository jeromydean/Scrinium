# Local infrastructure (Podman / Docker)

Compose file: [`docker-compose.yml`](../docker-compose.yml) at the repo root.

Works with **Podman** (preferred) or **Docker**. Helper scripts in this folder prefer Podman when both are installed.

## Quick start

```powershell
# One-time: TLS cert for Keycloak (Administrator, or approve elevation)
.\docker\generate-certificates.ps1

# Start (creates .env if needed, ensures cert exists)
.\docker\up.ps1

# Stop
.\docker\down.ps1
```

Manual equivalent:

```powershell
copy .env.example .env
podman compose up -d
```

On Windows/macOS, if Podman cannot connect: `podman machine start`.

Default credentials: [`.env.example`](../.env.example) (local dev only).

Named Docker volumes: `scrinium-postgres-data`, `scrinium-pgadmin-data`, `scrinium-redis-data`.

**Host bind mounts** under `SCRINIUM_DATA_DIR` (default `C:\ProgramData\Scrinium`):

| Subfolder | Service |
|-----------|---------|
| `solr\` | Solr cores / indexes |
| `minio\` | Object storage blobs |

**PostgreSQL stays on a named volume** on Windows/Podman. `initdb` must `chmod` its data directory; that fails on NTFS bind mounts (`Operation not permitted`). Use `compose down -v` (or delete `scrinium-postgres-data`) to wipe the DB. Solr/MinIO host folders are **not** removed by `-v` — delete those directories manually.

Optional: if you run Compose on Linux (or mount a Linux filesystem path that supports Unix permissions), you can switch Postgres to `${SCRINIUM_DATA_DIR}/postgres:/var/lib/postgresql` the same way as Solr/MinIO.

### TLS certificates and Firefox

`generate-certificates.ps1` adds the dev cert to the **Windows Trusted Root** store. That covers Edge and Chrome on Windows, but **Firefox uses its own certificate store by default**.

If Firefox shows an HSTS error for https://localhost:8983 (or :8443) and will not let you add a temporary exception, the cert is not trusted in Firefox yet. HSTS requires a valid trusted certificate — it blocks bypass for “insecure” sites.

**Option A (recommended on Windows):** In Firefox, open `about:config`, set `security.enterprise_roots.enabled` to `true`, and restart Firefox. Firefox will then honor the Windows trust store updated by the script.

**Option B:** Import `docker/certs/localhost.cer` in Firefox → Settings → Privacy & Security → Certificates → View Certificates → Authorities → Import → trust for websites.

Re-run `.\docker\generate-certificates.ps1` if you need a fresh `localhost.cer`. If HSTS was cached from an earlier failed visit, clear site data for `localhost` (Settings → Privacy → Cookies and Site Data → Manage Data) and try again after trusting the cert.

---

## Service URLs

### Keycloak

| | |
|---|---|
| **Admin console** | https://localhost:8443/admin |
| **Realm (OIDC)** | https://localhost:8443/realms/scrinium |
| **OpenID configuration** | https://localhost:8443/realms/scrinium/.well-known/openid-configuration |
| **Admin login** | `admin` / `secret` (from `.env`: `KEYCLOAK_ADMIN`, `KEYCLOAK_ADMIN_PASSWORD`) |

HTTPS uses the self-signed cert from `generate-certificates.ps1` (trusted on the local machine after that script runs).

**Bootstrap the `scrinium` realm** (idempotent — safe to re-run):

```powershell
.\docker\setup-keycloak.ps1
```

This creates or updates:

| Resource | ID / name |
|----------|-----------|
| Realm | `scrinium` |
| API client (confidential) | `scrinium-api` — direct access grants for local dev |
| Desktop client (public, PKCE) | `scrinium-avalonia` |
| Dev user | `devuser` / `devpass` (roles: `user`, `document:read`, `document:write`) |
| Realm roles | `admin`, `manager`, `user`, `document:*`, `workflow:*`, `folder:manage` |

The API client secret is written to `.env` as `SCRINIUM_API_CLIENT_SECRET`. Get a test JWT:

```powershell
.\docker\get-dev-token.ps1
# or for scripts:
$token = .\docker\get-dev-token.ps1 -Quiet
```

Matches [`appsettings.json`](../src/Scrinium.Api/appsettings.json) (`Authority`, `Audience`).

### pgAdmin

| | |
|---|---|
| **Web UI** | http://localhost:5050 |
| **Login** | `admin@local.dev` / `secret` (from `.env`: `PGADMIN_DEFAULT_EMAIL`, `PGADMIN_DEFAULT_PASSWORD`) |

The **Scrinium Postgres** server is preconfigured (connects to `postgres:5432` inside the compose network).

### PostgreSQL

| | |
|---|---|
| **Host (from host machine)** | `localhost:5432` |
| **Host (from containers)** | `postgres:5432` |
| **User / password** | `appuser` / `secret` (from `.env`) |
| **Data** | Named volume `scrinium-postgres-data` (not a Windows host folder — NTFS bind mounts break `initdb`) |

Connection strings (host machine):

```
# Application database
Host=localhost;Port=5432;Database=scrinium;Username=appuser;Password=secret

# Keycloak database (usually only needed for debugging)
Host=localhost;Port=5432;Database=keycloak;Username=appuser;Password=secret
```

### Apache Solr

| | |
|---|---|
| **Admin UI** | https://localhost:8983/solr/ |
| **Core** | `documents` (precreated on first start) |
| **Ping** | https://localhost:8983/solr/documents/admin/ping |
| **Host (from containers)** | `https://solr:8983/solr/documents` |
| **Host data directory** | `%SCRINIUM_DATA_DIR%\solr` (default `C:\ProgramData\Scrinium\solr`) |

HTTPS uses the self-signed cert from `generate-certificates.ps1` (`docker/certs/solr.pfx`, same localhost cert as Keycloak).

### Redis

| | |
|---|---|
| **Host (from host machine)** | `localhost:6379` |
| **Host (from containers)** | `redis:6379` |

### MinIO

| | |
|---|---|
| **S3 API** | http://localhost:9000 |
| **Console** | http://localhost:9001 |
| **Default credentials** | `minioadmin` / `minioadmin` (from `.env`: `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`) |
| **Host data directory** | `%SCRINIUM_DATA_DIR%\minio` (default `C:\ProgramData\Scrinium\minio`) |

Object keys (e.g. `archives/{id}/original/...`) appear under that folder after ingest. Recreate containers after changing `SCRINIUM_DATA_DIR` (`.\docker\up.ps1`). Named-volume data is not migrated automatically.

### Apache Tika

| | |
|---|---|
| **HTTP API** | http://localhost:9998 |
| **Host (from containers)** | `http://tika:9998` |

### Gotenberg

| | |
|---|---|
| **HTTP API** | http://localhost:3000 |
| **Health** | http://localhost:3000/health |
| **Host (from containers)** | `http://gotenberg:3000` |

> **Future:** Investigate Solr authentication and RBAC (Basic Auth, rule-based authorization, integration with Keycloak/API document ACL). Dev Solr is HTTPS-only with no login today.

### Scrinium API (not in Compose — run with `dotnet run`)

| | |
|---|---|
| **HTTP** | http://localhost:5243 |
| **HTTPS** | https://localhost:7299 |
| **Health check** | http://localhost:5243/health |
| **OpenAPI (Development)** | http://localhost:5243/openapi/v1.json |
| **SignalR ingestion hub** | ws://localhost:5243/hubs/ingestion |
| **Folder watcher client** | `dotnet run --project src/Scrinium.IngestionClient` (see below) |

### Folder watcher test client

Drop files into a watch folder; the client uploads via the ingestion API with Keycloak auth.

**Visual Studio (17.11+):** Select the **Api + Ingestion Client** launch profile from the toolbar dropdown (from [`Scrinium.slnLaunch`](../src/Scrinium.slnLaunch)), then F5. Both projects start together.

**Cursor / VS Code:** Run the **Api + Ingestion Client** compound launch configuration.

```powershell
# Or manually in two terminals:
# Terminal 1: API
dotnet run --project src\Scrinium.Api\Scrinium.Api.csproj

# Terminal 2: folder watcher (reads .env for client secret)
dotnet run --project src\Scrinium.IngestionClient\Scrinium.IngestionClient.csproj
```

Default watch folder: `%USERPROFILE%\Documents\Scrinium\inbox`  
Processed files move to `processed\`; failures to `failed\`.

---

## Scripts in this folder

| Script | Purpose |
|--------|---------|
| `generate-certificates.ps1` | Self-signed TLS certs for Keycloak and Solr → `docker/certs/*.pfx` |
| `setup-keycloak.ps1` | Idempotent Keycloak realm, clients, roles, and dev user bootstrap |
| `get-dev-token.ps1` | Fetch a dev JWT for API testing (uses `.env` client secret) |
| `Compose.ps1` | Shared Podman/Docker compose detection |
| `up.ps1` | Bootstrap `.env` + cert, then `compose up -d` |
| `down.ps1` | `compose down` (pass `-v` to remove volumes) |

---

## Notes

See [Local infrastructure](../docs/ARCHITECTURE.md#local-infrastructure) in the architecture doc for the full service map.
