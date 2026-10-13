# Basera — Architecture

Version 0.1 · 2026-10-13 · Status: agreed, pre-implementation

Basera is a multi-tenant SaaS for student hostels and paying-guest houses in Nepal, delivered as one ASP.NET Core 8 MVC web application built as a modular monolith on PostgreSQL. This document is the source of truth for structure, conventions and the rules every module follows. Product scope lives in `PRODUCT_SPEC.md`; the reasoning behind each choice lives in `DECISIONS.md`.

## 1. System overview

One deployable process serves four web experiences from one login:

| Area | Users | Device | Default language |
| --- | --- | --- | --- |
| `/owner` | hostel owner, manager | desktop first | English |
| `/staff` | warden, cook | phone first | Nepali |
| `/resident` | residents | phone first | Nepali |
| `/parent` | parents/guardians | phone first | Nepali |

Supporting pieces inside the same process: Hangfire server for background jobs, in-process domain event dispatcher with a transactional outbox, file storage adapter, SMS and email adapters, PDF renderer.

External systems: PostgreSQL 16 (all data, Hangfire storage, Data Protection keys), S3-compatible object storage (documents; local disk in development), SMS gateway (Sparrow; mock in development), SMTP (MailKit; MailHog in development), payment gateways (eSewa, Khalti, Fonepay; phase two).

```
Browser ──HTTPS──▶ Caddy/nginx ──▶ Basera.Web (Kestrel)
                                    ├─ MVC Areas (Owner/Staff/Resident/Parent) + /api
                                    ├─ Modules (Identity, Properties, Residents, Billing, Payments,
                                    │           Mess, Operations, Notifications, Compliance, Reporting, Audit)
                                    ├─ Hangfire server (recurring + enqueued jobs)
                                    └─ Adapters: PostgreSQL · S3/disk · SMS · SMTP · PDF
```

## 2. Solution layout

```
Basera.sln
src/
  Basera.Web/                         Host. Program.cs, Areas layouts, wwwroot, appsettings.
  BuildingBlocks/
    Basera.Shared/                    Cross-cutting code every module may use (section 5).
  Modules/
    Identity/Basera.Modules.Identity/
    Properties/Basera.Modules.Properties/
    Residents/Basera.Modules.Residents/
    Billing/Basera.Modules.Billing/
    Payments/Basera.Modules.Payments/
    Mess/Basera.Modules.Mess/
    Operations/Basera.Modules.Operations/
    Notifications/Basera.Modules.Notifications/
    Compliance/Basera.Modules.Compliance/
    Reporting/Basera.Modules.Reporting/
    Audit/Basera.Modules.Audit/
tests/
  Basera.UnitTests/                   Domain and building-block tests (no database).
  Basera.IntegrationTests/            WebApplicationFactory + Testcontainers PostgreSQL.
docs/                                 This folder.
scripts/                              dev.ps1, smoke.ps1, migrate.ps1
docker-compose.yml                    postgres, minio, mailhog for local development
Dockerfile                            multi-stage build of Basera.Web
.github/workflows/ci.yml              build, test, publish image on tag
```

Each module project has the same internal layout:

```
Basera.Modules.Billing/
  BillingModule.cs            implements IModule: registers services, maps routes, registers jobs
  Contracts/                  public interfaces other modules may call (IInvoiceLookup) + DTOs
  Domain/                     entities, value objects, domain events, domain services, rules
  Application/                application services (use-case methods), validators, view models
  Infrastructure/             BillingDbContext (schema "billing"), configurations, migrations,
                              repositories if needed, adapters specific to this module
  Presentation/
    Controllers/              MVC controllers, attributed with [Area("Owner")] etc.
    Views/                    Razor views for this module, under Areas/<Area>/Views/<Controller>/
    Api/                      minimal endpoints under /api/billing/... for HTMX/fetch partials
```

The host discovers modules through assembly scanning for `IModule` and calls `AddModule` during startup and `MapModule` after routing. The host project references every module project; modules never reference each other's projects except through `Basera.Shared` and the other module's `Contracts` namespace (enforced by an architecture test).

## 3. Module rules

1. A module owns its schema. Its `DbContext` maps only its own tables, in a PostgreSQL schema named after the module (`identity`, `properties`, `billing`, ...), with its own `__EFMigrationsHistory` table in that schema.
2. No module queries another module's tables, not even read-only. Cross-module reads go through the owning module's `Contracts` interface (for example `IResidentLookup.GetActiveAsync(tenantId, residentId)`), registered in DI by the owning module.
3. Cross-module effects go through domain events. `ResidentCheckedIn` is raised by Residents and handled by Billing (create first pro-rated invoice) and Notifications (welcome SMS). Handlers in other modules never call back into the raising module synchronously.
4. Billing and Payments are append-only ledgers. Nothing financial is updated or deleted; mistakes are corrected with reversing entries that reference the original.
5. Every tenant-owned entity implements `ITenantOwned { Guid TenantId }`. The base `DbContext` applies the global filter and sets `TenantId` on insert. A query that must cross tenants (platform admin) uses `IgnoreQueryFilters()` explicitly and is audited.
6. Presentation code (controllers, views) lives in the module, not the host. The host owns only layouts, shared partials and the landing/account pages that belong to Identity.
7. Modules register their Hangfire recurring jobs in `MapModule` with a stable job id prefixed by module name (`billing:generate-monthly`).

## 4. Request flow

1. Caddy/nginx terminates TLS and forwards to Kestrel with forwarded headers.
2. Middleware order: exception handler → HSTS/HTTPS redirect → static files → forwarded headers → request logging (Serilog) → routing → rate limiter → authentication (cookie) → tenant context (reads `tenant_id` claim into `ITenantContext`) → authorization → localisation → endpoints.
3. Controller action: FluentValidation runs via an action filter; invalid input returns the view with `ModelState` errors (full page) or a partial with errors (HTMX request, detected by the `HX-Request` header).
4. Action calls one application service method. The service loads aggregates through the module's `DbContext`, applies domain rules, raises domain events, calls `SaveChangesAsync`.
5. `SaveChangesAsync` interceptor: sets audit columns, assigns `TenantId`, collects raised domain events, writes outbox rows for events marked `[Reliable]`, commits.
6. After commit, in-process handlers run for the same request; the outbox processor (Hangfire recurring job every 10 s, plus immediate enqueue) delivers reliable events with retries.
7. The action returns a view, a partial (HTMX), a redirect, a file (PDF), or a JSON envelope (`/api`).

## 5. Building blocks (`Basera.Shared`)

| Concern | Type(s) | Notes |
| --- | --- | --- |
| Module registration | `IModule`, `ModuleLoader` | `AddModule(IServiceCollection, IConfiguration)`, `MapModule(IEndpointRouteBuilder)` |
| Tenancy | `ITenantContext`, `ITenantOwned`, `TenantDbContext` | global filter, insert interceptor, `TenantNotResolvedException` |
| Auditing | `IAuditable`, `AuditInterceptor` | `CreatedAt/By`, `UpdatedAt/By`, soft delete via `DeletedAt` where allowed |
| Money | `Money` (long paisa), `MoneyConverter` | formatting `रु 12,500.00`, Nepali digits option, arithmetic with overflow checks |
| Calendar | `NepaliDate`, `NepaliCalendar`, `BillingPeriod` | BS↔AD with verified table 2000–2099 BS, month length, first/last day, Nepali month names |
| Phone | `PhoneNumber` | E.164, Nepal mobile rules (97/98 prefixes), display formatting |
| Results | `Result`, `Result<T>`, `DomainError`, `ErrorCode` | services return results for expected failures; exceptions for bugs |
| Events | `IDomainEvent`, `IDomainEventHandler<T>`, `DomainEventDispatcher`, `OutboxMessage` | reliable events marked with `[Reliable]` go through the outbox |
| Time | `IClock` → `TimeProvider` | injectable time for tests |
| Files | `IFileStorage`, `LocalDiskFileStorage`, `S3FileStorage` | stored under `tenant/{tenantId}/{module}/{id}`; never public URLs |
| Messaging | `ISmsSender`, `MockSmsSender`, `SparrowSmsSender`, `IEmailSender`, `MailKitEmailSender` | mock writes to an inbox visible at `/dev/sms` in Development |
| PDF | `IPdfRenderer` (QuestPDF) | shared fonts (Noto Sans Devanagari), header/footer with hostel identity |
| Web | `ApiEnvelope`, `HtmxExtensions`, tag helpers (`bs-date`, `money`, `phone`, `status-badge`), `NepaliDateModelBinder` | |
| Localisation | `SharedResource`, `ILocalizedText` | resource files `ne` and `en`; per-user preference persisted |

## 6. Data

PostgreSQL 16, one database `basera`, one schema per module plus `hangfire` and `dataprotection`. EF Core 8 with Npgsql, `DbContext` per module, migrations per module, applied by `scripts/migrate.ps1` (development) or a deployment step (production), never on app start.

Conventions:

- Primary keys: `Guid` v7 (`Guid.CreateVersion7()`), column `id`. Human codes where people read them: invoice `INV-{BSyear}-{BSmonth}-{seq}`, receipt `RCP-...`, bed `B{floor}-{room}-{slot}`.
- Snake_case table and column names via a naming convention; table names singular per module (`billing.invoice`, `billing.invoice_line`).
- Money columns `bigint` paisa; `Money` value object converter.
- Calendar dates `date` (`DateOnly`); timestamps `timestamptz` (`DateTimeOffset`, UTC).
- Every tenant-owned table: `tenant_id uuid not null` with a composite index `(tenant_id, ...)` leading on tenant.
- Concurrency: `xmin` as concurrency token on editable aggregates (Resident, Room, Bed, Complaint).
- Soft delete only for Resident, Property, Room, Bed, Notice; never for financial rows.
- Seed data in a `DevSeeder` run by `scripts/dev.ps1`, never in migrations (except code lists).

## 7. Identity and authorization

ASP.NET Core Identity (`IdentityUser<Guid>` extended with `TenantId`, `PreferredLanguage`, `Phone` verified flag) with cookie authentication, schema `identity`.

- Owners/managers/wardens/cooks: email or phone + password, optional 2FA later.
- Residents/parents: phone + OTP. OTP: 6 digits, 5-minute expiry, 5 attempts, 3 sends per 10 minutes per phone, delivered through `ISmsSender`. User is created on first successful OTP and linked to the Resident or Guardian record by phone.
- Claims: `sub`, `tenant_id`, `role` (one of Owner, Manager, Warden, Cook, Resident, Parent), `name`, `lang`.
- Policies: `RequireOwner`, `RequireManagement` (Owner|Manager), `RequireStaff` (Owner|Manager|Warden|Cook), `RequireResident`, `RequireParent`. Resource authorization handlers check that a resident/parent can only access their own records.
- Platform admin (Basera staff) is a separate role outside tenants with a separate `/admin` area; added later.

## 8. Background jobs (Hangfire)

Storage in PostgreSQL schema `hangfire`. Dashboard at `/jobs`, Owner-of-tenant cannot see it; platform admin only. Jobs:

| Job id | Schedule | Module |
| --- | --- | --- |
| `billing:generate-monthly` | 1st of each BS month, 00:30 NPT | Billing |
| `billing:apply-late-fees` | daily 01:00 NPT | Billing |
| `notifications:reminders` | hourly | Notifications |
| `notifications:outbox` | every 10 s + on demand | Shared/Notifications |
| `payments:reconcile-gateways` | every 15 min (phase two) | Payments |
| `compliance:monthly-register-snapshot` | 1st of each BS month | Compliance |
| `reporting:refresh-read-models` | nightly | Reporting |

All jobs are idempotent: generating invoices for a month that already has them is a no-op per resident; reminders record what was sent.

## 9. Front end

Razor views with a shared `_Layout` per area, Bootstrap 5.3, HTMX 2 for dynamic fragments, a small `site.js` for things HTMX can't do (camera capture preview, QR display, service worker registration). No jQuery, no SPA.

- HTMX convention: controller actions that serve fragments return `PartialView` when `Request.IsHtmx()`; otherwise a full page. Forms post with `hx-post` and swap the form or a target region.
- Mobile-first for Staff/Resident/Parent: large tap targets, bottom action bar, no hover-only controls.
- Localisation through `IStringLocalizer` and resource files; language switch stored on the user.
- Dates rendered by the `bs-date` tag helper: BS primary, AD secondary.
- PWA: `manifest.webmanifest`, service worker caching static assets only (no offline data).
- Accessibility baseline: labels on all inputs, colour never the only signal (status badges carry text).

## 10. Observability

Serilog with request logging (method, path, status, duration, tenant, user, correlation id), console in development, JSON files/stdout in production for the host to collect. `/health` with database, storage and Hangfire checks. Exceptions handled by one `IExceptionHandler` that renders an error page (web) or envelope (`/api`), logs with the correlation id, hides details outside Development. OpenTelemetry traces and metrics are a later addition.

## 11. Testing

- Unit tests: domain rules (pro-rating, late fees, settlement), `NepaliCalendar` against known dates, `Money`, `PhoneNumber`, validators. No database.
- Integration tests: `WebApplicationFactory<Program>` with a Testcontainers PostgreSQL, one container per test class, migrations applied, seeded tenant. Cover each module's main flows and the tenant isolation test (tenant A cannot see tenant B).
- Architecture tests (NetArchTest): modules do not reference each other outside `Contracts`; Domain has no EF or ASP.NET references.
- Smoke script: health, login, floor map, create resident, generate invoice, record payment, download register PDF.

## 12. Security checklist

Anti-forgery tokens on all forms (HTMX sends them via `hx-headers`). Data Protection keys in PostgreSQL. Rate limiting on `/account/*` and `/api/*`. Cookies `Secure`, `HttpOnly`, `SameSite=Lax`. Documents served only through `DocumentsController` with authorization; object storage private. ID document images encrypted at rest (`IFileStorage` encrypting adapter with a per-tenant key). Security headers (CSP allowing self + HTMX inline attributes via nonce, X-Content-Type-Options, Referrer-Policy). Password policy per Identity defaults; OTP limits above. Audit log on financial, resident-profile and role changes. Dependency scanning in CI.

## 13. Deployment

- Local: `docker compose up` (postgres, minio, mailhog); `scripts/dev.ps1` migrates, seeds, runs with `dotnet watch`.
- CI (GitHub Actions): on push, restore/build/test; on tag `v*`, build and push the Docker image.
- Production (phase one): one VPS, Docker Compose with `basera-web`, `postgres` (volume + nightly `pg_dump` to object storage), `caddy` (automatic TLS). Environment variables for all secrets. Migrations run by `docker compose run basera-web migrate` before the new image starts.
- Backups restored and verified monthly. Log retention 30 days.

## 14. Naming and code conventions

C# latest, nullable enabled, warnings as errors in CI. `sealed` by default. Services end in `Service`, contracts in `Contracts/` start with `I` and are small. View models end in `ViewModel`, form inputs end in `Input`. Controllers are thin: bind, call one service, map result to view/redirect/envelope. Razor views named after actions. Resource keys `Area.Page.Element`. Commit messages follow conventional commits with module scope (`feat(billing): ...`).
