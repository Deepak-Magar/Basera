# Basera — Architecture Decision Records

Each record: context, options considered, decision, consequences. Add a new record rather than editing an old one when a decision changes; mark the old one superseded.

## ADR-001 Web application, not a mobile app

Context: residents use the system a few times a month; wardens need dense operational screens; one developer.
Options: Flutter app + API + web admin; website only; website now, store wrapper later.
Decision: ASP.NET Core 8 MVC website for all roles, mobile-first for Staff/Resident/Parent, PWA installable; a Trusted Web Activity wrapper for the Play Store once there are paying customers.
Consequences: no push notifications (SMS covers reminders); one codebase and deployment; no store review cycle; dense screens and printing are easy; a native app can be added later behind the same modules.

## ADR-002 Modular monolith

Context: clear business areas (Residents, Billing, Mess...) with little overlap; one deployable wanted.
Options: single project with feature folders; classic N-layer (Web/Application/Domain/Infrastructure); modular monolith; microservices.
Decision: modular monolith: one host, one project per module, each module layered internally with its own DbContext and schema, communicating through Contracts interfaces and domain events.
Consequences: more projects than a single-project app; enforced boundaries make the code easier to reason about and to extract later; interviewers can discuss it; requires an architecture test to keep boundaries honest.

## ADR-003 Shared database with tenant column

Context: hostels pay NPR 1,500–5,000 per month; hundreds of tenants expected.
Options: database per tenant; schema per tenant; shared tables with TenantId.
Decision: one database, shared tables, `TenantId` on every tenant-owned row, global query filter and insert interceptor, tenant from the signed-in user's claims only.
Consequences: cheapest to run and migrate; isolation depends on the filter, so an integration test proves it and cross-tenant access is explicit and audited; a very large tenant can be moved to its own database later if ever needed.

## ADR-004 ASP.NET Core Identity with cookies; OTP for residents and parents

Context: website, not an API; residents and parents will not remember passwords.
Options: JWT bearer; cookie auth with Identity; external identity provider.
Decision: Identity with cookie authentication; password login for staff; phone + OTP for residents and parents, user created on first OTP; roles per tenant in claims; policies plus resource-based checks.
Consequences: standard, well-documented, works with anti-forgery and Razor; OTP needs SMS cost control (rate limits) and a mock in development; JWT can be added later for an API host without changing modules.

## ADR-005 Application services, not MediatR/CQRS

Context: team of one learning the stack; MediatR became a commercial licence in 2025.
Options: MediatR with request/handler per action; hand-rolled command/query handlers; plain application services with use-case-named methods.
Decision: plain application services (`IInvoiceService.GenerateForMonthAsync`), one clear method per use case, domain events for cross-module effects.
Consequences: fewer files, F12-navigable; pipeline behaviours (validation, logging) done with action filters and interceptors instead; if CQRS is wanted later, services are already named by use case and can be split.

## ADR-006 Hangfire for background jobs

Context: monthly invoicing, reminders, outbox and reports need scheduling, retries and visibility.
Options: BackgroundService loops; Quartz.NET; Hangfire.
Decision: Hangfire with PostgreSQL storage, dashboard restricted to platform admin, recurring jobs registered by modules with stable ids, all jobs idempotent.
Consequences: one dependency; persistent, retryable, inspectable jobs; recurring schedules in NPT must be converted to the server's time zone.

## ADR-007 Bootstrap 5 + HTMX + minimal vanilla JS

Context: MVC with Razor; dynamic parts (floor map, counts, search, inline forms) without an SPA.
Options: jQuery; vanilla JS only; HTMX; React islands.
Decision: Bootstrap 5.3 for layout, HTMX 2 for fragments driven by controller partials, a small `site.js` for camera preview, QR display and service worker.
Consequences: server stays the single source of truth; little JavaScript to maintain; anti-forgery tokens must be sent on HTMX requests; developers need to learn HTMX's handful of attributes.

## ADR-008 FluentValidation and manual mapping

Options: data annotations only; FluentValidation; AutoMapper/Mapster for mapping.
Decision: FluentValidation for input models via an action filter that fills `ModelState`; data annotations only for trivial display metadata; manual `ToViewModel()`/`ToEntity()` mapping methods.
Consequences: explicit, testable validation rules; mapping is visible code, slightly more typing, no runtime mapping surprises.

## ADR-009 Money as long paisa; dates as DateOnly/DateTimeOffset; own Nepali calendar

Options: `decimal` money; third-party BS date NuGet packages.
Decision: `Money` value object over `long` paisa; `DateOnly` for calendar dates stored in AD; `DateTimeOffset` UTC for timestamps; own `NepaliCalendar` with a verified conversion table (2000–2099 BS) and tests against known dates.
Consequences: no rounding ambiguity; BS logic is under our control and tested; the table must be maintained when the official calendar publishes new years.

## ADR-010 Append-only ledgers with reversing entries

Context: the owner's main fear is leakage and disputes.
Decision: invoices, receipts, deposit movements and adjustments are never edited or deleted; corrections are reversing entries referencing the original with a reason; audit log on top.
Consequences: trustworthy statements; slightly more complex UI for "undo"; reports must account for reversals.

## ADR-011 Domain events with a transactional outbox

Context: cross-module effects (invoice on check-in, SMS on payment) must not be lost.
Decision: in-process domain events collected at `SaveChanges`; handlers run after commit; events marked `[Reliable]` are written to an outbox table in the same transaction and delivered by a Hangfire job with retries.
Consequences: modules stay decoupled; reliable side effects without a message broker; the outbox needs monitoring and a dead-letter view.

## ADR-012 QuestPDF for documents

Options: HTML-to-PDF via headless Chromium; iText (AGPL/commercial); QuestPDF.
Decision: QuestPDF (community licence, valid while revenue is below USD 1M; revisit if exceeded), with Noto Sans Devanagari embedded.
Consequences: fast, code-defined layouts, no browser dependency in production; licence condition recorded here.

## ADR-013 Phase-one payments without gateway integration

Context: merchant onboarding with eSewa/Khalti takes time; the first customers need the product now.
Decision: cash receipts, bank-slip upload with staff confirmation, and the hostel's own payment QR with an "I have paid" flow in phase one; gateway adapters behind `IPaymentGateway` in phase two.
Consequences: sellable sooner; some manual confirmation work for wardens until phase two.

## ADR-014 Markdown documentation in the repository

Decision: `docs/ARCHITECTURE.md`, `docs/PRODUCT_SPEC.md`, `docs/DECISIONS.md`, `docs/PROGRESS.md` versioned with the code; Word exports produced on request.
Consequences: documentation changes are reviewed with code; visible on GitHub.

## ADR-015 Deployment on a single VPS with Docker Compose

Context: phase-one scale is hundreds of tenants, low traffic.
Decision: one VPS running Docker Compose (web, PostgreSQL, Caddy), migrations as a deployment step, nightly backups to object storage, GitHub Actions for build/test/image.
Consequences: cheap and simple; vertical scaling first; move PostgreSQL to a managed service or add a second web node when needed.
