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

## Open design questions (decide before implementing)

- **Domain model:** Transaction, Category, and split/settle adjustment schema — sketch before writing any EF migrations.
- **Bank statement import:** Format support (CSV, OFX, PDF), incremental deduplication strategy, API endpoint vs. background service.
- **Tailwind build pipeline:** npm + Tailwind CLI integration with .NET 10 / Blazor, or CDN play-mode for early prototyping.
- **Hosting:** CORS origin configuration for NPM reverse proxy. Docker Compose stack needed for Api + Ui + SQL Server containers.
