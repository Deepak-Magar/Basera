# Basera — Progress

## Session 1

Goal: from an empty folder to a running host with building blocks and tenancy (features 0–6). Identity is not started this session.

### Done

- **Feature 0 — folder and docs.** `D:/basera` created; `docs/ARCHITECTURE.md`, `docs/PRODUCT_SPEC.md`, `docs/DECISIONS.md` copied in (source of truth); `docs/learning/` created; `.gitignore` from `dotnet new gitignore`.
- **Feature 1 — solution skeleton.** `Basera.sln`; host `Basera.Web` (Serilog console, `/health`, areas Owner/Staff/Resident/Parent/Api with placeholder pages and one `_Layout` each, Bootstrap 5.3.8 and HTMX 2.0.11 in `wwwroot/lib`); `Basera.Shared` with `IModule`, `ModuleLoader`, `ModuleCatalog`; eleven empty modules with the §2 folder layout; unit and integration test projects (8 tests passing); `docker-compose.yml`; `scripts/dev.ps1`; `Directory.Build.props`, `Directory.Packages.props`, `global.json`. Landing page lists the 11 loaded modules.
- **Feature 2 — architecture tests.** NetArchTest.Rules 1.3.2 in `Basera.UnitTests`: per-module test that no type uses another module outside its `Contracts` namespace; per-module test (and one for `Basera.Shared`) that `...Domain` namespaces depend on neither EF Core nor ASP.NET Core; a guard that all 11 modules are inspected; self-tests against fixtures proving each rule can fail. 29 unit + 7 integration tests passing.
- **Feature 3 — value objects and results.** In `Basera.Shared`: `Domain/Money`, `Domain/NepaliDigits`, `Domain/PhoneNumber`, `Domain/Calendar/{NepaliCalendar, NepaliDate, BillingPeriod, calendar-bs.csv}`, `Results/{Result, Result<T>, DomainError, ErrorCode}`, `Time/{IClock, SystemClock}`, `AddBaseraShared()` registered in the host. 134 unit + 7 integration tests passing, including a round trip of every day 2000–2099 BS and the three reference dates.

- **Feature 4 — tenancy and audit.** In `Basera.Shared`: `Domain/{ITenantOwned, IAuditable, ISoftDeletable, GuidV7}`, `Tenancy/{ITenantContext, TenantContext, TenantNotResolvedException, TenantResolutionMiddleware}`, `Users/{ICurrentUser, HttpCurrentUser, BaseraClaimTypes}`, `Persistence/{TenantDbContext, TenantInterceptor, AuditInterceptor, MoneyConverter, AddModuleDbContext}`; `app.UseBaseraTenancy()` in the host. Integration tests: `TestDatabase` fixture (local server via `BASERA_TEST_PG`, else Testcontainers) and `TenantIsolationTests` on a test-only `ProbeDbContext`. 153 unit + 19 integration tests passing against the local PostgreSQL 18 (`basera_test_7b2342e6` created and dropped in the final run).

- **Feature 5 — domain events, outbox, Hangfire.** In `Basera.Shared`: `Domain/{IDomainEvent, ReliableAttribute, Entity, IHasDomainEvents}`, `Events/{IDomainEventHandler, DomainEventDispatcher, DomainEventInterceptor, EventSerializer, AddDomainEventHandlers}`, `Outbox/{OutboxMessage, OutboxDbContext, OutboxProcessor, IOutboxSignal}` with migration `InitialOutbox` (schema `outbox`), `Jobs/{AddBaseraJobs, MapBaseraJobs, HangfireOutboxSignal, PolicyDashboardAuthorizationFilter}`, `Authorization/BaseraPolicies` (`PlatformAdmin`). Hangfire 1.8.25 + Hangfire.PostgreSql 1.21.1 (schema `hangfire`), dashboard `/jobs`, recurring job `notifications:outbox` every 10 s. Local `dotnet-ef` 8.0.32 tool manifest (`.config/dotnet-tools.json`) and EF Design in the host. 166 unit + 30 integration tests passing against the local PostgreSQL 18; Hangfire delivered a signalled event in 0.2 s and an unsignalled one through the 10 s schedule in 8.3 s.

- **Feature 6 — web plumbing.** In `Basera.Shared`: `Web/{ApiEnvelope (+ApiResults), CorrelationIdMiddleware, BaseraExceptionHandler, HtmxExtensions, RateLimiting, NepaliDateModelBinder, WebServiceCollectionExtensions}`, `Web/TagHelpers/{BsDate, Money, Phone, StatusBadge}TagHelper`, `Localization/{SharedResource (+.resx, .ne.resx), BaseraCultures}`, `Messaging/{ISmsSender, SmsSegments, MockSmsSender, MockSmsInbox}`, `Files/{IFileStorage, FileKey, LocalDiskFileStorage}`, `Health/BaseraHealthChecks`. Host: new middleware order, `/api/ping`, `ErrorController` (`/error`, `/error/{status}`), `LanguageController` (`POST /language`), `DevController` (`/dev/sms`), language switch in every layout, `hx-headers` anti-forgery on every layout body, PWA manifest, icons and `sw.js`, HTMX error toast in `site.js`. 211 unit + 52 integration tests passing against the local PostgreSQL 18.

### Session 1 pause (2026-10-13)

- Features 0–3 are done and pushed to GitHub.
- Features 4–6 remain (listed below). Identity is still out of scope for this session.
- **Before Feature 4** (superseded 2026-10-18: local PostgreSQL used instead, see below): install Docker Desktop and make sure `docker` works from Git Bash. Feature 4's integration tests use Testcontainers PostgreSQL, and `scripts/dev.ps1` runs `docker compose up`. `docker-compose.yml` has not been run yet, so check it with `docker compose config` first.
- To resume: start the next session at Feature 4 with the same ground rules and checkpoint protocol.

### Session 1 resume (2026-10-18): local PostgreSQL instead of Docker

Docker is not installed. A local PostgreSQL 18 service (`postgresql-x64-18`) is used instead.

- **Dev connection.** `appsettings.Development.json` has `ConnectionStrings:Default` with the placeholder password `__SET_IN_USER_SECRETS__`; the real value is a Basera.Web user secret (`UserSecretsId` added to the csproj). `WebApplication.CreateBuilder` already loads user secrets in Development, so Program.cs only explains this in a comment. A second `AddUserSecrets` call would rank secrets above environment variables and command-line arguments. The old key `ConnectionStrings:Basera` (user `basera`) is gone; `docker-compose.yml` now uses user/password `postgres`/`postgres` to match.
- **Scripts.** `scripts/dev.ps1` skips `docker compose` when `docker` is not on PATH (or with `-SkipDocker`), then runs `create-db.ps1` and `migrate.ps1`, then `dotnet watch`. `scripts/create-db.ps1` reads the user secret and creates `basera` with psql if it is missing. `scripts/migrate.ps1` (named in ARCHITECTURE §6) lists module DbContexts; it is empty until the first migration.
- **Integration tests.** `TestDatabase` collection fixture: with `BASERA_TEST_PG` set, it creates `basera_test_<8 hex>` on that server, migrates, and drops it at the end; otherwise Testcontainers `postgres:16`. Documented in ARCHITECTURE §11 and `docs/learning/04-multitenancy-and-interceptors.md` (renamed 2026-10-19 at your request and cut to the 300–600 word format; the test-database detail lives in ARCHITECTURE §11). ARCHITECTURE §11 said "one container per test class"; it is now one database per run, with a tenant seeded per class.
- **Version gap.** The local server is PostgreSQL 18; ARCHITECTURE §6, CI (Testcontainers) and production use 16. Nothing we use differs between them. Don't use 17+/18-only SQL features.

### Session rules (from 2026-10-19)

- Claude runs no git commands; you commit and push.
- No README.
- Every feature gets `docs/learning/<nn>-<topic>.md`, 300–600 words: what was built, why, 3–5 files to read, two questions.
- Checkpoint after each feature: `dotnet build` and `dotnet test` clean, then a beep and the READY TO COMMIT block, then stop until you reply "pushed" or "skip".
- After Feature 6: list deviations and stop. Identity is not started.

### Remaining

Nothing left in Session 1. Next session: Identity (not started, per the session rules).

### Decisions and notes

- Where the session prompt and the docs disagree, the docs win. Differences found while reading:
  - **Guid v7:** ARCHITECTURE §6 names `Guid.CreateVersion7()`, which only exists from .NET 9. On .NET 8 we will add a small `GuidV7.New()` helper in Basera.Shared that produces the same RFC 9562 layout, so we can switch to the built-in when we move to .NET 9.
  - **Api area:** the prompt asks for an `Api` MVC area with a placeholder controller and view. ARCHITECTURE §2 places API endpoints as minimal endpoints under `/api/<module>/...` inside each module. The `Api` area was created as asked, with a single placeholder help page; module endpoints will follow §2.
  - **Placeholder controllers in the host:** ARCHITECTURE §3.6 says presentation lives in modules and the host owns only layouts. The feature-1 placeholder controllers are temporary host scaffolding; real screens will live in their modules.
  - **Language preference:** ARCHITECTURE §9 stores it on the user. Until Identity exists, it is stored in a cookie, as the prompt says.
  - **Warnings as errors:** ARCHITECTURE §14 says "in CI". The prompt puts it in Directory.Build.props, which applies to local builds too. We follow the prompt because it is stricter.
  - **Middleware order:** ARCHITECTURE §4 lists forwarded headers after static files. ASP.NET Core guidance puts `UseForwardedHeaders` first so HTTPS redirect and logging see the real scheme and IP. We will put it first and record this as a doc correction.
  - **Outbox job id:** ARCHITECTURE §8 names it `notifications:outbox`. The processor lives in Shared (feature 5) but keeps that job id.
- **Module discovery** scans `Basera.Modules.*.dll` in the app base directory, because referenced assemblies are not loaded until first use.
- **Module projects are plain class libraries** with a framework reference to ASP.NET Core. When the first module adds Razor views, that module switches to the Razor SDK and we decide the view location convention for `Presentation/Views`.
- **FluentAssertions pinned to 7.2.2:** 8.x needs a commercial licence; 7.x is Apache-2.0.
- **global.json** pins SDK 8.0.417 (roll forward to later 8.0.x feature bands).
- **Boundary rule is type-based:** forbidden = every type of another module outside `<Module>.Contracts` (sub-namespaces of Contracts allowed). The Domain rule also covers any `.Domain` namespace in `Basera.Shared`.
- **BS calendar data (needs your attention).** Built from four independent sources, cross-checked 2026-10-13; provenance is in the header of `calendar-bs.csv`.
  - 2000–2061 and 2063–2082: all four agree.
  - 2062: we use nepali-datetime's deliberate 2024 correction (Baisakh 31, Jestha 31, Asar 31); three JS libraries still have 30/32/31. Matters for dates of birth in 2062 BS. Verify against a printed calendar if possible.
  - 2083: three of four agree, including both libraries published in 2026. Today (2026-10-13 = 2083-06-27) checks out.
  - 2084–2099: **provisional**; sources disagree because these are projections. Replace each year from the official calendar when published (data edit only).
- **Money formatting** uses Nepali/Indian lakh grouping (`रु 1,25,000.00`); the spec's example `रु 12,500.00` is the same in both systems. Negative amounts are `-रु 500.00`. `FromRupees` rejects fractions of a paisa rather than rounding; `Multiply`/`Percent`/`RoundToRupee` round half away from zero.
- **Namespaces:** value objects live in `Basera.Shared.Domain(.Calendar)`, so the feature-2 Domain rule keeps them framework-free; results in `Basera.Shared.Results`; clock in `Basera.Shared.Time`.
- **Nepal time** is a fixed UTC+05:45 offset in `SystemClock` (no DST, avoids Windows/Linux time zone id differences).
- **PhoneNumber** accepts only 97/98-prefixed 10-digit mobiles, per ARCHITECTURE §5. Foreign numbers (e.g. a guardian abroad) are rejected for now.
- **Feature 4 notes:**
  - **`tenant_id` is a concurrency token** on every tenant-owned entity, so UPDATE/DELETE include `AND tenant_id = @tenant`. Without it, an entity attached with another tenant's id bypasses the query filter. Added to ARCHITECTURE §6.
  - **Unresolved tenant throws** (`TenantNotResolvedException`) on both query and save; it never returns an empty result.
  - **`TenantContext.Set` refuses to switch** to a different tenant within one DI scope. Jobs use one scope per tenant.
  - **`ICurrentUser`** is new, not in ARCHITECTURE §5. The audit columns need a user id, and tenant and user are separate concerns. Null means a background job.
  - **One query filter per entity** (EF Core 8 limit): tenant and soft delete are combined, so `IgnoreQueryFilters()` drops both.
  - **Two interceptors** (`TenantInterceptor`, then `AuditInterceptor`), as named in §5. Feature 5 adds domain-event collection and the outbox to the save pipeline.
  - **History table columns are snake_case** (`migration_id`, `product_version`) because of EFCore.NamingConventions. They are the same for every module.
  - **The connection string is read lazily** when the first DbContext is created, so the host starts (and HostTests run) without a database.
  - **Not done yet:** the dotnet-ef local tool manifest (the global `dotnet-ef` here is 3.1.1, too old) and `Microsoft.EntityFrameworkCore.Design` in the host. Both get added with the first module migration.
  - **Mutation check skipped:** I tried to temporarily disable the filter and the concurrency token to prove the isolation tests fail. Claude Code's auto mode blocked it as "weakening security". The negative tests (unresolved tenant, cross-tenant insert/move, stub attack) do assert the failure paths directly.
- **Feature 5 notes:**
  - **Reliable events go only through the outbox;** they are not also dispatched in-process, so a handler never runs twice for one save. After a save that wrote messages, `HangfireOutboxSignal` enqueues the processor at once (ARCHITECTURE §4.6 "plus immediate enqueue"); the 10 s recurring job catches anything missed.
  - **One `outbox.outbox_message` table, not one per module.** `OutboxDbContext` owns it and its migration. Each module DbContext maps it with `ExcludeFromMigrations`, so the insert is in the module's own transaction. One table means one processor query and one dead-letter view. New schema `outbox`, added to ARCHITECTURE §6.
  - **Retries are per message,** in the row: 10 s doubling to a 1 h cap, dead-lettered (`is_dead_lettered`) after 10 attempts. The Hangfire job itself has `[AutomaticRetry(Attempts = 0)]`.
  - **Claiming uses `FOR UPDATE SKIP LOCKED`,** one transaction per message, so overlapping runs do not double-deliver and one failure doesn't block the batch. Delivery is at least once: reliable handlers must be idempotent.
  - **Handlers run in a fresh DI scope** with the raising tenant set (from the request for in-process events, from `outbox_message.tenant_id` for reliable ones). In-process handler failures are logged, not thrown, because the change is already committed.
  - **Events are cleared from entities only after a successful save;** on failure the added outbox rows are detached, so a retried save neither loses nor duplicates events.
  - **Explicit transactions:** inside `BeginTransaction`, "after save" means before commit, so in-process handlers would run for a change that may roll back. Services should not open explicit transactions; reliable events are unaffected (test `Rolled_back_change_leaves_no_outbox_row`).
  - **Event payloads are JSON** (System.Text.Json web defaults, camelCase) with `Money` as a paisa number. The type is stored as "Full.Name, Assembly" without a version, and only types implementing `IDomainEvent` are deserialized.
  - **Hangfire creates its own schema at startup** (`PrepareSchemaIfNecessary`). This is the one exception to "migrations never on app start"; it is Hangfire's internal storage, not ours. The host now needs the database at startup (recurring job registration), so `HostTests` moved onto `TestDatabase`.
  - **Polling tuned for 10 s:** `QueuePollInterval` and `SchedulePollingInterval` are 1 s (defaults 15 s). Workers: processor count clamped to 2..8. `Jobs:ServerEnabled=false` turns the server off; the test factory does this except in `HangfireTests`.
  - **Dashboard authorization:** a Hangfire `IDashboardAsyncAuthorizationFilter` evaluates the ASP.NET `PlatformAdmin` policy, so a refusal is a plain 401 even though no authentication scheme exists yet. (`RequireAuthorization` on the endpoint would try to challenge a scheme that isn't there, and throw.) The policy allows Development only; Identity will add the platform admin role.
  - **Job ids:** `notifications:outbox` per ARCHITECTURE §8, though the processor lives in Shared.
  - **Base types:** `Entity` gives every entity a `GuidV7` id and the event list; EF ignores `DomainEvents` by convention in `TenantDbContext`.
- **Feature 6 notes:**
  - **Envelope shape** was not specified in the docs; chosen as `{ success, data, error: { code, message, target }, correlationId }` and added to ARCHITECTURE §5. Pipeline statuses use three codes outside `ErrorCode`: `UNAUTHORIZED`, `METHOD_NOT_ALLOWED`, `RATE_LIMITED`.
  - **Exception handling is the same in every environment** (no developer exception page): the error page shows the exception only in Development. `IExceptionHandler` must read `IExceptionHandlerPathFeature.Path`, because the middleware rewrites `Request.Path` to `/error` before calling it.
  - **Re-executed error pages use the original page's language** (`BaseraCultures` reads the original path from the re-execute and exception features).
  - **Devanagari is emitted as text:** `WebEncoderOptions` allows Basic Latin + Devanagari, otherwise Razor writes every Nepali letter as `&#x...;` (about 3x the bytes).
  - **Anti-forgery cookie `SameAsRequest`, not `Always`:** `Always` throws on any plain-HTTP request (a misconfigured proxy, the test server) and takes the error page down with it. Behind the HTTPS redirect and HSTS the cookie is still always Secure. Recorded in ARCHITECTURE §12.
  - **Rate limits by path, not by endpoint attribute,** so Identity's `/account/*` endpoints are covered the day they appear. Defaults: account 10/min, api 120/min, per user (`sub`) else per IP; configurable under `RateLimits:`.
  - **Language cookie** (`.AspNetCore.Culture`) until Identity stores the preference on the user; `returnUrl` must be local. Area defaults: `/owner` and `/` English, `/staff`, `/resident`, `/parent` Nepali.
  - **Nepali strings need a native speaker's review.** The resource file was written by Claude: status names (e.g. "Paid" = भुक्तानी भयो), error texts, area titles.
  - **Mock SMS inbox is in memory** (last 500), so a restart empties it. `SmsSegments` counts billable parts (GSM-7 160/153, UCS-2 70/67): a Nepali SMS over 70 characters costs two.
  - **File storage:** only `LocalDiskFileStorage` (`App_Data/files`, ignored in `.gitignore`). The S3 adapter and the per-tenant encrypting adapter for ID documents (§12) are not built; MinIO isn't available here and Compliance owns the encryption design.
  - **Security headers:** `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy` only. The nonce-based CSP of §12 is not done; the Hangfire dashboard's inline scripts need an exception.
  - **Not done from §5/§12:** Data Protection keys in PostgreSQL (needed before Identity cookies go to more than one instance), `IEmailSender`/MailKit, `IPdfRenderer`. They are listed for Identity and later features.
  - **PWA icons** are generated placeholders (white "B" on #0d6efd); replace with real artwork.
- **Docker not found** on this machine's PATH (`docker` command missing). Resolved 2026-10-18 by using the local PostgreSQL (see resume section); Testcontainers stays as the fallback and the CI path.
