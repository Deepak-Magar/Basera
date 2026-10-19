# 06 — Web plumbing

## What was built

The cross-cutting web pieces every module will lean on, mostly in `Basera.Shared/Web`, wired up by `AddBaseraWeb()` and the middleware order in `Program.cs`:

- **Envelope.** Every `/api` response is `{ success, data, error: { code, message, target }, correlationId }`. `result.ToApiResult()` turns a service `Result` into it, and `ApiResults` holds the single `ErrorCode` → HTTP status map.
- **Errors.**
  - A correlation id per request appears in the `X-Correlation-Id` header, in every log line, and on every error page.
  - One `IExceptionHandler` returns a 500 envelope for `/api` and a toast event for HTMX requests (`HX-Reswap: none`). Full page requests are re-executed to `/error`.
  - 404s and other bare statuses go to `/error/{status}`, as a page or an envelope.
  - Details are shown only in Development.
- **Rate limits** on `/account/*` (10 per minute) and `/api/*` (120 per minute), per user or else per IP. A rejection is a 429 with `Retry-After`.
- **HTMX.** `Request.IsHtmx()`, `HxTrigger`, `HxRedirect`, and `this.PageOrPartial()`. Each layout's `body` carries the anti-forgery token in `hx-headers`, and a global filter validates every POST.
- **Tag helpers**, plus `NepaliDateModelBinder`, which accepts `2083-06-27` or `२०८३-०६-२७`:
  - `<bs-date>`: BS first, AD second.
  - `<money>`: lakh grouping, Nepali digits in Nepali.
  - `<phone>`: tap to call.
  - `<status-badge>`: colour plus text.
- **i18n.** `SharedResource` in English and Nepali. The language comes from a cookie, else the area default (Owner English, the phone areas Nepali). A switch in every header changes it, and Devanagari is emitted as text rather than `&#x...;` entities.
- **PWA.** `manifest.webmanifest`, icons, and `sw.js`, which caches static assets only.
- **Mock SMS.** `ISmsSender` → `MockSmsSender`, listed at `/dev/sms` (Development only). `SmsSegments` counts billable parts.
- **Files.** `IFileStorage` with `LocalDiskFileStorage` at `tenant/{tenantId}/{module}/{id}` under `App_Data`, never under `wwwroot`.
- **Health.** `/health` checks the database, storage and Hangfire, and returns JSON with no exception text.

## Why it is shaped this way

**One envelope, one status map.** Clients must tell "your input was wrong" from "we broke" without parsing prose. A fixed shape with a machine-readable `code` and a `target` field lets a form put the message next to the right input.

**Correlation ids turn a screenshot into a log search.** A resident reports "something went wrong"; the reference number on that page is the id on every log line of that request.

**Why HTMX errors don't swap.** By default a failing fragment would replace part of the page with an error page inside it. `HX-Reswap: none` keeps the page intact, and the toast tells the user what happened.

**Why limits are applied by path.** Identity's login and OTP endpoints don't exist yet. A path-based global limiter protects them the day they're added, with no attribute to forget. OTP SMS cost real money (ADR-004).

**Why area default languages.** Owners work at a desk in English; wardens and residents use phones in Nepali (§1). The cookie overrides the default, and Identity will move the choice onto the user.

**Why the service worker caches only static files.** A cached page could show yesterday's balance, or another person's page on a shared phone.

## Read in this order

1. `src/Basera.Web/Program.cs`: the middleware order, top to bottom.
2. `src/BuildingBlocks/Basera.Shared/Web/ApiEnvelope.cs`
3. `src/BuildingBlocks/Basera.Shared/Web/BaseraExceptionHandler.cs` and `src/Basera.Web/Controllers/ErrorController.cs`
4. `src/BuildingBlocks/Basera.Shared/Web/HtmxExtensions.cs`
5. `tests/Basera.IntegrationTests/Web/WebPlumbingTests.cs`

## Questions you should be able to answer

1. A warden's HTMX "mark attendance" button hits an action that throws. What does the server send back, what does the warden see, and how would you find the exception in the logs?
2. Why does the exception handler look at `IExceptionHandlerPathFeature.Path` instead of `Request.Path` to decide whether to answer with an envelope?
