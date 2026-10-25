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
- **Module projects are plain class libraries** with a framework reference to ASP.NET Core. When the first module adds Razor views, that module switches to the Razor SDK and we decide the view location convention for `Presentation/Views`. *(Decided 2026-10-25, Session 2 Feature 5: `Presentation/Views/<Area>/<Controller>/<Action>.cshtml`, see ARCHITECTURE §2.)*
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

## Session 2 — Identity

Goal: Data Protection, the Identity module, owner registration and password login, OTP login, staff invitations, and a role-aware shell (features 1–6). Learning docs continue the numbering (07 onward). Properties is not started this session. IEmailSender/MailKit is deferred to the Notifications session.

### Done

- **Feature 1 — Data Protection keys in PostgreSQL.** In `Basera.Shared`: `DataProtection/{DataProtectionKeysDbContext, BaseraDataProtection}` with migration `InitialDataProtection` (schema `dataprotection`, table `data_protection_key`). Package `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 8.0.32. Host calls `AddBaseraDataProtection()`; `scripts/migrate.ps1` lists the context. Integration tests (`Security/DataProtectionTests.cs`): protect/unprotect across two hosts, key ids present in the table, anti-forgery cookie + token from host A accepted by host B, application name `Basera`. Learning doc `07-data-protection-keys.md`.

- **Feature 2 — Identity module skeleton.** `Basera.Modules.Identity`: `Domain/{Tenant (+TenantPlan, TenantStatus), TenantMembership, Role}`, `Infrastructure/{ApplicationUser, IdentityDbContext, SignInLookup, BaseraIdentity}` with migration `InitialIdentity` (schema `identity`: `tenant`, `application_user`, `tenant_membership`, `user_claim`, `user_login`, `user_token`). `Basera.Shared.Authorization/{BaseraRoles, BaseraPolicies.Require*}`. Package `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 8.0.32. Host: `UseAuthentication()` before tenancy and authorization; `scripts/migrate.ps1` lists `IdentityDbContext`. Tests: 11 unit (`Identity/TenantTests.cs`) and 10 integration (`Identity/IdentityModelTests.cs`). Learning doc `08-identity-module.md`.

- **Feature 3 — Owner registration and password login.** Identity: `Application/{AccountService, OtpService (+OtpOptions)}`, `Domain/OtpChallenge (+OtpPurpose, OtpCheck)`, `Infrastructure/BaseraClaimsPrincipalFactory`, migration `AddOtpChallenge` (`identity.otp_challenge`). `IdentityServiceCollectionExtensions` renamed to `BaseraIdentity` (clashed with Microsoft's class of that name), and doc 08's reading list was updated to match. Lockout 5 failures / 15 minutes. Shared: `Web/FluentValidationFilter (+ModelStateExtensions.AddDomainError)`, registered globally; the `auth` rate-limit partition (`RateLimits:Auth`, formerly `RateLimits:Account`) now limits `/account/*` POSTs only. Packages FluentValidation + DependencyInjectionExtensions 12.1.1. Host: `Areas/Account/{Controllers/AccountController, Models/AccountInputs (inputs + validators), Views/...}`, `AddValidatorsFromAssemblyContaining<Program>()`, development `Identity:Otp:HashKey`, 37 new resource strings (en + ne), `.account-main` / `.otp-input` CSS. Tests: 18 integration (`Identity/AccountTests.cs`, `AccountTestClient.cs`) and 5 unit (`Identity/OtpChallengeTests.cs`). Learning doc `09-owner-registration-and-login.md`.

- **Feature 4 — OTP login for residents and parents.** Residents: `Contracts/IResidentPhoneLookup (+PhoneHolder, PhoneHolderKind)`, `Application/NoResidentPhoneLookup` (stub, registered with `TryAddScoped`). Identity: `Application/OtpSignInService` (+`OtpSendOutcome`, `OtpSignInOutcome`), `Infrastructure/IdentityStoreExtensions` (`HoldChanges`, `IsUniqueViolation`, shared with registration). `OtpService` now returns `OtpSendResult`/`OtpCheck`; `AccountService` maps them to the same messages as before. Identity references the Residents project. Host: `Otp`/`OtpVerify` actions and views, the login page links to OTP, 15 resource strings (en + ne); `BaseraCultures` makes `/account/otp` Nepali by default. Tests: 13 integration (`Identity/OtpLoginTests.cs`, with `TestClock`), 2 unit (area default language). No migration. Learning doc `10-otp-login.md`.

- **Feature 5 — Staff invitations and membership management.** Identity is now a Razor SDK project. `Domain/StaffInvitation (+InvitationState)`, `TenantMembership.ChangeRole/IsManagedStaff`, `Application/{InvitationService, StaffService}`, `Infrastructure/TenantMemberAuthorization` (`TenantMemberRequirement`, `TenantMembershipHandler`, `StaffInvitationHandler`), `Presentation/Controllers/StaffController` (+`InviteStaffInput` and validator), `Presentation/Views/{_ViewImports, _ViewStart, Owner/Staff/Index, Owner/Staff/Invite}`, migration `AddStaffInvitation`. Security stamp interval 5 minutes. Shared: `ModuleLoader` adds module assemblies as MVC parts and the view location `/Presentation/Views/{2}/{1}/{0}.cshtml`. Host: `/account/invite/{token}` (views `Invite`, `InviteInvalid`, `InviteSignedIn`), Staff link in the Owner nav, 30 resource strings (en + ne, including `Shared.Role.*`). Tests: 12 integration (`Identity/StaffInvitationTests.cs`), 10 unit (`Identity/StaffInvitationRulesTests.cs`). One fix to Feature 4's test (timestamp compared to microsecond precision). Learning doc `11-staff-invitations.md`.

### Remaining

- Feature 6 — Role-aware shell, profile, seed and smoke.

### Decisions and notes

- **Feature 1 notes:**
  - **Fixed application name `Basera`** (`SetApplicationName`). The default discriminator is the content root path, so hosts deployed to different folders could not read each other's payloads. Changing the name signs everyone out.
  - **Keys are not encrypted at rest yet.** A custom repository has no default XML encryptor, so keys are plain XML and Data Protection logs a warning at first use. Before production: `ProtectKeysWithCertificate` with a certificate from an environment secret. Open item.
  - **Class name `BaseraDataProtection`**, not `DataProtectionServiceCollectionExtensions`, which clashes with the framework class of that name in `Microsoft.Extensions.DependencyInjection`. Same pattern as `BaseraHealthChecks`.
  - **The round-trip test alone proves little on Windows**, because the default key folder under `%LOCALAPPDATA%` is shared per user. The test that compares key ids with the table rows is the real check.
- **Feature 2 notes:**
  - **`ApplicationUser` is in Infrastructure, not Domain:** it derives from `IdentityUser<Guid>` (ASP.NET Core), and the architecture test keeps Domain framework-free.
  - **`IdentityDbContext` inherits `TenantDbContext`, not Identity's `IdentityUserContext`,** so it maps the user, claim, login and token tables itself, and `AddEntityFrameworkStores` uses `UserOnlyStore`. The name matches Microsoft's `IdentityDbContext`; don't import `Microsoft.AspNetCore.Identity.EntityFrameworkCore` next to it.
  - **`AddIdentityCore` + `AddIdentityCookies` instead of `AddIdentity<TUser, TRole>`:** same cookies and sign-in manager, but no Identity roles. The role comes from `TenantMembership`.
  - **Users are tenant-owned** (filter and interceptor apply). `UserName` = phone in E.164. Phone and email are **unique across all tenants**, so one person can't hold accounts in two hostels with the same phone. That matters later for a parent with children in two hostels: one user with memberships in two tenants would need `ApplicationUser.TenantId` to go and tenant switching on the user. Revisit when Residents/Parents arrive.
  - **One `IgnoreQueryFilters`:** `SignInLookup.FindAsync`, which returns ids only (documented in `08-identity-module.md`).
  - **Tenant rows are not filtered** (a tenant is not tenant-owned). Other modules must read the current tenant through a Contracts interface (to come), never the table.
  - **Policy and role names in Shared** (`BaseraPolicies`, `BaseraRoles`), so modules use them without referencing Identity. A unit test keeps `Role` and `BaseraRoles.All` in step. Policies check the `role` claim and require an authenticated user.
  - **Cookie `.Basera.Auth`:** 14 days sliding, HttpOnly, SameSite Lax, `SameAsRequest`. The paths `/account/login`, `/account/logout` and `/account/denied` come in Feature 3. `OnValidatePrincipal` sets the tenant from the cookie's claim before the security stamp check (it runs before tenant resolution).
- **Feature 3 notes:**
  - **Registration is one `SaveChanges`:** the user store's `AutoSaveChanges` is turned off for the call, so tenant, user and membership commit together without an explicit transaction (Feature 5's rule). A race on the unique phone/email index becomes a Conflict message.
  - **OTP storage built here, not in Feature 4,** because password reset needs it. Feature 4 adds the login purpose's flow, the pending-membership lookup, and tests for every limit; the limits are already enforced (`OtpService`).
  - **`Identity:Otp:HashKey`** is a new required secret outside Development. A development key is in `appsettings.Development.json`, and it is not secret. The service throws a clear error on first use when the key is missing. It is not checked at startup, so hosts that never send a code (tests in Production mode) still start.
  - **Phone verification at owner sign-up is not done.** PRODUCT_SPEC §4.1 says "with phone verification", but the feature brief didn't include it. `PhoneVerifiedAt` is set by the first password reset (and by OTP login in Feature 4). Open item: an OTP step at registration.
  - **Service messages are English.** `DomainError` texts from `AccountService`/`OtpService` and ASP.NET Identity's password errors are not localized; page labels and validator messages are. Open item for the Nepali pass.
  - **Login redirects to `returnUrl` or `/`;** registration to `/owner`. Role-aware redirects are Feature 6.
  - **Tenant status is not checked at login** (a suspended tenant can still sign in). Comes with platform admin.
  - **Timing:** an unknown login answers faster than a wrong password (no hash computed). Minor account-enumeration signal, mitigated by the `auth` rate limit. Open item.
  - **Tests decrypt the auth cookie** with the host's own `TicketDataFormat` to check its claims, rather than adding a test-only endpoint.
- **Feature 4 notes:**
  - **`IResidentPhoneLookup` is in Residents.Contracts** (the owner of residents and guardians, §3.2), so Identity → Residents is the project dependency. Residents must never reference Identity (that would be a cycle).
  - **The real lookup will search across tenants** (no one is signed in). It needs its own `IgnoreQueryFilters`, justified the same way as `SignInLookup`: a phone names at most one person, and only ids and a display name are returned.
  - **The OTP page reveals whether a number is registered** (clear messages for residents, and no SMS spent on unknown numbers). Probing is capped by the `auth` rate limit and 3 codes per phone per 10 minutes.
  - **The membership stores no link to the Resident/Guardian record** (`PhoneHolder.PersonId` is not kept). Residents can map a user to a person by phone. Add `PersonId` to the membership if resource authorization needs it.
  - **A phone that is both a resident in one hostel and a parent in another** can't be represented (one user per phone, see Feature 2 notes).
  - **OTP users get a persistent cookie** (14 days sliding); password users choose with "keep me signed in".
  - **Redirect after OTP sign-in is `/`** until Feature 6.
- **Feature 5 notes:**
  - **View location differs from ARCHITECTURE §2's first wording** ("under Areas/<Area>/Views/<Controller>/"): module views are at `Presentation/Views/<Area>/<Controller>/`. Shorter, and the same for every module. §2 updated. All modules share this virtual path, so controller names must be unique per area across modules.
  - **Application parts are added explicitly** by `ModuleLoader` (skipping parts MVC already discovered), so a module's controllers don't depend on MVC's dependency-context discovery.
  - **Invitation tokens carry the tenant id,** so accepting needs no `IgnoreQueryFilters`. The tenant id in an SMS link is not secret; the 256-bit secret is.
  - **Re-accept for a former staff member:** calling `RemovePasswordAsync` + `AddPasswordAsync` before the single held `SaveChanges` failed the user row's concurrency check (0 rows). Replaced by setting `PasswordHash` (after running the password validators) and one `UpdateSecurityStampAsync`. Root cause not pinned down; likely two store updates on one tracked user with auto-save off. Avoid several `UserManager` updates inside one held save.
  - **Invitation SMS is English** and usually two segments (hostel name + link).
  - **Staff management is Owner-only** (`RequireOwner`). PRODUCT_SPEC doesn't say whether a Manager may invite. Decide when Properties adds per-property managers.
  - **Role change signs the member out** (security stamp) rather than refreshing their claims in place. Simple and safe; refresh in place is possible with `SecurityStampValidatorOptions.OnRefreshingPrincipal` if it annoys users.
  - **Service error texts on the staff pages are English** (same open item as Feature 3).
  - **Tooling:** a PowerShell 5.1 `Get-Content`/`Set-Content` edit turned the Devanagari in `OtpLoginTests.cs` into mojibake (it reads BOM-less UTF-8 as ANSI). Repaired; file edits now go through the editor or Python only.
