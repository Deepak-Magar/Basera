# Basera — Progress

## Session 1

Goal: from an empty folder to a running host with building blocks and tenancy (features 0–6). Identity is not started this session.

### Done

- **Feature 0 — folder and docs.** `D:/basera` created; `docs/ARCHITECTURE.md`, `docs/PRODUCT_SPEC.md`, `docs/DECISIONS.md` copied in (source of truth); `docs/learning/` created; `.gitignore` from `dotnet new gitignore`.
- **Feature 1 — solution skeleton.** `Basera.sln`; host `Basera.Web` (Serilog console, `/health`, areas Owner/Staff/Resident/Parent/Api with placeholder pages and one `_Layout` each, Bootstrap 5.3.8 and HTMX 2.0.11 in `wwwroot/lib`); `Basera.Shared` with `IModule`, `ModuleLoader`, `ModuleCatalog`; eleven empty modules with the §2 folder layout; unit and integration test projects (8 tests passing); `docker-compose.yml`; `scripts/dev.ps1`; `Directory.Build.props`, `Directory.Packages.props`, `global.json`. Landing page lists the 11 loaded modules.
- **Feature 2 — architecture tests.** NetArchTest.Rules 1.3.2 in `Basera.UnitTests`: per-module test that no type uses another module outside its `Contracts` namespace; per-module test (and one for `Basera.Shared`) that `...Domain` namespaces depend on neither EF Core nor ASP.NET Core; a guard that all 11 modules are inspected; self-tests against fixtures proving each rule can fail. 29 unit + 7 integration tests passing.

### Remaining

3. Value objects and results (Money, NepaliDate/NepaliCalendar, BillingPeriod, PhoneNumber, Result, IClock)
4. Tenancy and audit (ITenantContext, TenantDbContext, interceptor, isolation integration test)
5. Domain events, outbox, Hangfire
6. Web plumbing (envelope, errors, rate limits, HTMX, tag helpers, i18n, PWA, mock SMS, file storage, health checks)

### Decisions and notes

- Where the session prompt and the docs disagree, the docs win. Differences found while reading:
  - **Guid v7:** ARCHITECTURE §6 names `Guid.CreateVersion7()`, which only exists from .NET 9. On .NET 8 we will add a small `GuidV7.New()` helper in Basera.Shared that produces the same RFC 9562 layout, so we can switch to the built-in when we move to .NET 9.
  - **Api area:** the prompt asks for an `Api` MVC area with a placeholder controller and view. ARCHITECTURE §2 places API endpoints as minimal endpoints under `/api/<module>/...` inside each module. We will create the `Api` area as asked, with a JSON placeholder action and a view only as a landing/help page, and module endpoints will follow §2.
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
- **Docker not found** on this machine's PATH (`docker` command missing). Testcontainers (feature 4 onward) and `scripts/dev.ps1` need it.
