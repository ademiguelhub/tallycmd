# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project purpose

Tallycmd is a personal finance web app for importing bank statements incrementally, categorising and adjusting transactions (including split/settled expenses), and visualising spending. It is multi-user by design, with the primary user being the developer. Target hosting is a self-hosted homelab running Docker on TrueNAS, with Nginx Proxy Manager (NPM) as the reverse proxy.

## Technology stack

- **Runtime:** .NET 10
- **API:** ASP.NET Core Web API (Minimal API style)
- **Database:** SQL Server (Linux Docker image — `mcr.microsoft.com/mssql/server:2022-latest`)
- **ORM:** Entity Framework Core, code-first, single `DbContext` inheriting from `IdentityDbContext`
- **Frontend:** Blazor Server — `InteractiveServer` render mode, prerendering **disabled**
- **CSS:** Tailwind CSS
- **Auth:** ASP.NET Identity + JWT (access) + HttpOnly refresh token cookie

## Solution structure

Three projects, all in one solution (`Tallycmd.slnx`):

| Project           | Role                                                                                                                                                                                    |
| ----------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Tallycmd.Shared` | Class library — DTOs, service interfaces, shared utilities. No dependency on Api or Ui.                                                                                                 |
| `Tallycmd.Api`    | ASP.NET Core Web API — EF DbContext, Identity, JWT issuance, refresh token management, domain service classes, REST endpoints.                                                          |
| `Tallycmd.Ui`     | Blazor Server app — typed `HttpClient` services, `JwtAuthenticationStateProvider`, `InMemoryTokenStore`, auth delegating handler, Tailwind UI components. No DB or Identity references. |

Both `Api` and `Ui` reference `Shared`. `Ui` has **no project reference to `Api`** — all communication is over HTTP, keeping the door open for future clients (mobile, CLI importer) without refactoring.

## Key architecture decisions

**Blazor render mode — InteractiveServer, prerendering disabled**
Eliminates the double-lifecycle class of bugs (JS interop not ready, operations that throw during prerender). Reconnects automatically via SignalR.

**Frontend ↔ backend communication — typed HttpClient with a delegating handler**
Components inject service interfaces (e.g. `ITransactionService`). Implementations wrap `HttpClient` calls. Auth token attachment is transparent via the delegating handler. The interface contract is decoupled from transport.

**Database — single DbContext inheriting from IdentityDbContext**
Preserves FK integrity between domain entities and the user table. Enables LINQ joins across users and transactions.

**Auth model — JWT in-memory + HttpOnly refresh cookie**
The JWT access token lives in a scoped `InMemoryTokenStore` (never in browser storage — XSS-safe). The HttpOnly refresh cookie is invisible to JavaScript. On circuit reconnect, `GetAuthenticationStateAsync` finds no token and silently calls `POST /api/auth/refresh`; the browser sends the cookie automatically, a new JWT is issued, and the user is seamlessly re-authenticated. Session window is 30 days.

**Auth in Ui — custom `JwtAuthenticationStateProvider`**
No ASP.NET Identity references in the Ui project. Works with Blazor's `[Authorize]` and `<AuthorizeView>` via the `AuthenticationStateProvider` abstraction. Calls `NotifyAuthenticationStateChanged` after login/logout.

## Code conventions

**Usings — C# projects**
Each project has a single `GlobalUsings.cs` at its root. All `using` declarations go there. No `using` statements at the top of individual `.cs` files.

**Usings — Blazor components**
All `@using` directives go in the project's root `_Imports.razor`. No `@using` at the top of individual `.razor` files.

**HttpClient — typed clients, not named clients**
Register HTTP clients with `AddHttpClient<TClient>()` and inject the typed client directly. Never use `IHttpClientFactory.CreateClient("some-string")`. Each logical backend gets its own typed client class (e.g. `TallyApiClient : HttpClient`).

**No magic strings / literal keys**
Do not use raw string literals as identifiers (DI keys, route segments shared across projects, claim type names, policy names, etc.). If the same literal is used in both `Api` and `Ui`, define it as a `public const` in a `static class` in `Tallycmd.Shared`. If it is Ui-only or Api-only, the `static class` lives in that project. Prefer typed clients and strongly-typed options over keyed strings wherever the framework supports it.

## Development environment

**Strategy: infra in containers, apps on host.**
SQL Server runs in Docker (managed by `docker-compose.yml`). The .NET apps run natively so the VS Code debugger can attach normally. This keeps the debug cycle fast while the DB environment matches production.

**First-time setup**
```bash
# Start the SQL container (creates the named volume)
docker compose up -d

# Grant write permission to the mssql user (uid 10001) on the backups bind mount
chmod o+w backups/

# Apply EF migrations to create the schema
dotnet ef database update --project Tallycmd.Api
```

**F5 (VS Code "Web App" compound)**
Runs `DEBUG: Prepare` as `preLaunchTask`, which fans out in parallel to `SQL: Start` and `DOTNET: Build Solution`. Both must complete before the UI and API processes launch with the debugger attached.

**VS Code tasks — DOTNET**

| Task | What it does |
| ---- | ------------ |
| `DOTNET: Build Solution` | `dotnet build` on the full solution |
| `DOTNET: Rebuild Solution` | `dotnet build -t:Rebuild` |

**VS Code tasks — EF**

| Task | What it does |
| ---- | ------------ |
| `EF: Check Changes` | Reports whether the model has pending migration changes |
| `EF: Add Migration` | Prompts for a name and adds a migration to `Tallycmd.Api/Data/Migrations/` |
| `EF: Remove Migration` | Removes the last unapplied migration |
| `EF: Check Database` | Lists applied/pending migrations |
| `EF: Migrate Database` | Applies all pending migrations (`database update`) |
| `EF: Drop Database` | Drops the dev database |

**VS Code tasks — SQL**

| Task | What it does |
| ---- | ------------ |
| `SQL: Start` | `docker compose up -d` — idempotent, safe to run when already running |
| `SQL: Stop` | `docker compose down` |
| `SQL: Backup` | Creates a timestamped `.bak` in `backups/` on the host |
| `SQL: Restore` | Prompts for a filename, kicks all connections, restores |
| `SQL: List Backups` | Lists `.bak` files in `backups/` with sizes |

**DB data management**
`backups/` is bind-mounted into the container at `/var/opt/mssql/backup`. Backup files land directly on the host and are portable — copy a `.bak` to another machine and run `SQL: Restore` there. The named volume `tallycmd_sql-data` holds the live database and survives `docker compose down`; only `docker volume rm` destroys it.

## Commands

```bash
# Build entire solution
dotnet build

# Run the API (https, port 11443)
dotnet run --project Tallycmd.Api

# Watch mode (API)
dotnet watch --project Tallycmd.Api

# Run tests (once test projects exist)
dotnet test

# Run a single test class / method
dotnet test --filter "FullyQualifiedName~MyTestClass.MyTestMethod"
```

## JWT signing key

The `Jwt:Key` is the HMAC-SHA256 signing secret. Anyone with it can forge valid tokens — treat it like a password.

- **Minimum length:** 32 bytes (256 bits). Generate with `openssl rand -base64 32`.
- **Dev key** lives in `appsettings.Development.json`.
- **Prod key** must never be committed. Inject via Docker env var: `Jwt__Key=<generated-value>` in `docker-compose.yml`.
- `appsettings.json` (committed) should have no key; the app should throw on startup if one is missing.
- **Rotation:** No built-in expiry. Rotate immediately if leaked. Routine rotation is optional for a homelab — rotating invalidates all active refresh tokens (forces re-login for all users).

## Open design questions (decide before implementing)

- **Domain model:** Transaction, Category, and split/settle adjustment schema — sketch before writing any EF migrations.
- **Bank statement import:** Format support (CSV, OFX, PDF), incremental deduplication strategy, API endpoint vs. background service.
- **Tailwind build pipeline:** npm + Tailwind CLI integration with .NET 10 / Blazor, or CDN play-mode for early prototyping.
- **Hosting:** CORS origin configuration for NPM reverse proxy. Docker Compose currently manages SQL Server only (dev). A production compose stack adding Api + Ui containers is still needed.
