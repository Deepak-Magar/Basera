# Basera — Progress

## Session 1

Goal: from an empty folder to a running host with building blocks and tenancy (features 0–6). Identity is not started this session.

### Done

- **Feature 0 — folder and docs.** `D:/basera` created; `docs/ARCHITECTURE.md`, `docs/PRODUCT_SPEC.md`, `docs/DECISIONS.md` copied in (source of truth); `docs/learning/` created; `.gitignore` from `dotnet new gitignore`.
- **Feature 1 — solution skeleton.** `Basera.sln`; host `Basera.Web` (Serilog console, `/health`, areas Owner/Staff/Resident/Parent/Api with placeholder pages and one `_Layout` each, Bootstrap 5.3.8 and HTMX 2.0.11 in `wwwroot/lib`); `Basera.Shared` with `IModule`, `ModuleLoader`, `ModuleCatalog`; eleven empty modules with the §2 folder layout; unit and integration test projects (8 tests passing); `docker-compose.yml`; `scripts/dev.ps1`; `Directory.Build.props`, `Directory.Packages.props`, `global.json`. Landing page lists the 11 loaded modules.
- **Feature 2 — architecture tests.** NetArchTest.Rules 1.3.2 in `Basera.UnitTests`: per-module test that no type uses another module outside its `Contracts` namespace; per-module test (and one for `Basera.Shared`) that `...Domain` namespaces depend on neither EF Core nor ASP.NET Core; a guard that all 11 modules are inspected; self-tests against fixtures proving each rule can fail. 29 unit + 7 integration tests passing.
- **Feature 3 — value objects and results.** In `Basera.Shared`: `Domain/Money`, `Domain/NepaliDigits`, `Domain/PhoneNumber`, `Domain/Calendar/{NepaliCalendar, NepaliDate, BillingPeriod, calendar-bs.csv}`, `Results/{Result, Result<T>, DomainError, ErrorCode}`, `Time/{IClock, SystemClock}`, `AddBaseraShared()` registered in the host. 134 unit + 7 integration tests passing, including a round trip of every day 2000–2099 BS and the three reference dates.

### Session 1 pause (2026-10-13)

- Features 0–3 are done and pushed to GitHub.
- Features 4–6 remain (listed below). Identity is still out of scope for this session.
- **Before Feature 4:** install Docker Desktop and make sure `docker` works from Git Bash. Feature 4's integration tests use Testcontainers PostgreSQL, and `scripts/dev.ps1` runs `docker compose up`. `docker-compose.yml` has not been run yet, so check it with `docker compose config` first.
- To resume: start the next session at Feature 4 with the same ground rules and checkpoint protocol.

### Session 1 resume (2026-10-18): local PostgreSQL instead of Docker

Docker is not installed. A local PostgreSQL 18 service (`postgresql-x64-18`) is used instead.

- **Dev connection.** `appsettings.Development.json` has `ConnectionStrings:Default` with the placeholder password `__SET_IN_USER_SECRETS__`; the real value is a Basera.Web user secret (`UserSecretsId` added to the csproj). `WebApplication.CreateBuilder` already loads user secrets in Development, so Program.cs only explains this in a comment. A second `AddUserSecrets` call would rank secrets above environment variables and command-line arguments. The old key `ConnectionStrings:Basera` (user `basera`) is gone; `docker-compose.yml` now uses user/password `postgres`/`postgres` to match.
- **Scripts.** `scripts/dev.ps1` skips `docker compose` when `docker` is not on PATH (or with `-SkipDocker`), then runs `create-db.ps1` and `migrate.ps1`, then `dotnet watch`. `scripts/create-db.ps1` reads the user secret and creates `basera` with psql if it is missing. `scripts/migrate.ps1` (named in ARCHITECTURE §6) lists module DbContexts; it is empty until the first migration.
- **Integration tests.** `TestDatabase` collection fixture: with `BASERA_TEST_PG` set, it creates `basera_test_<8 hex>` on that server, migrates, and drops it at the end; otherwise Testcontainers `postgres:16`. Documented in ARCHITECTURE §11 and `docs/learning/04-tenancy-and-audit.md`. ARCHITECTURE §11 said "one container per test class"; it is now one database per run, with a tenant seeded per class.
- **Version gap.** The local server is PostgreSQL 18; ARCHITECTURE §6, CI (Testcontainers) and production use 16. Nothing we use differs between them. Don't use 17+/18-only SQL features.

### Remaining

4. Tenancy and audit (ITenantContext, TenantDbContext, interceptor, isolation integration test)
5. Domain events, outbox, Hangfire
6. Web plumbing (envelope, errors, rate limits, HTMX, tag helpers, i18n, PWA, mock SMS, file storage, health checks)

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
- **Docker not found** on this machine's PATH (`docker` command missing). Resolved 2026-10-18 by using the local PostgreSQL (see resume section); Testcontainers stays as the fallback and the CI path.
