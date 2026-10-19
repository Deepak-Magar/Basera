# 04 — Multitenancy and interceptors

## What was built

The plumbing that keeps each hostel's data to itself and records who changed what, in `Basera.Shared`:

- `ITenantOwned`, `IAuditable`, `ISoftDeletable`: interfaces an entity implements (with private setters) to opt in.
- `TenantContext`, the scoped "which tenant are we acting for". `TenantResolutionMiddleware` fills it from the signed-in user's `tenant_id` claim.
- `TenantDbContext`, the base class of every module DbContext. It puts tables and the migrations history in the module's schema, uses snake_case names, stores `Money` as `bigint` paisa, and adds one global query filter per entity: right tenant and not soft-deleted.
- `TenantInterceptor`, which stamps the tenant on insert and refuses cross-tenant writes, and `AuditInterceptor`, which fills `created_*`/`updated_*` and turns deletes into `deleted_at`.
- `AddModuleDbContext<T>()`, the single registration call a module makes.
- `GuidV7`, time-ordered ids for .NET 8.
- Integration tests on a real PostgreSQL, through `TestDatabase` (your local server via `BASERA_TEST_PG`, or Testcontainers).

## Why it is shaped this way

**Isolation must be the default, not a habit.** ADR-003 puts all hostels in shared tables. If every query had to remember `Where(x => x.TenantId == ...)`, one forgotten clause would leak another hostel's residents. With the filter in the base class, you have to *type* `IgnoreQueryFilters()` to see across tenants, and that call is easy to search for in review.

**The filter reads the tenant per query.** It compares against a property of the context. EF Core treats a context member in a filter as a SQL parameter, evaluated every time a query runs, so one compiled model serves every tenant. When no tenant is set, that property throws `TenantNotResolvedException`. A missing claim is a loud bug, not a silently empty page.

**Interceptors guard the write side.** Filters only apply to queries. `TenantInterceptor` runs on every `SaveChanges` of every module, including modules written later by someone who never read this page. One gap remains: an object attached with a guessed id from another tenant would produce `UPDATE ... WHERE id = @id`, which no filter sees. Making `tenant_id` a concurrency token adds `AND tenant_id = @tenant` to every UPDATE and DELETE, so such a statement matches nothing and EF throws.

**Audit columns are not the audit log.** The columns answer "who touched this row last". The Audit module (PRODUCT_SPEC §4.11) will answer "what changed", fed by domain events. Created values are marked unmodified on every update, so nothing can overwrite them.

**Real PostgreSQL in tests.** Filters, concurrency tokens, `timestamptz` and schemas are database behaviour. The EF in-memory provider ignores most of it. Each run gets a fresh `basera_test_<guid>` database, so tests never touch your `basera` data.

## Read in this order

1. `src/BuildingBlocks/Basera.Shared/Persistence/TenantDbContext.cs`
2. `src/BuildingBlocks/Basera.Shared/Persistence/TenantInterceptor.cs`
3. `src/BuildingBlocks/Basera.Shared/Persistence/AuditInterceptor.cs`
4. `src/BuildingBlocks/Basera.Shared/Tenancy/TenantResolutionMiddleware.cs`
5. `tests/Basera.IntegrationTests/Tenancy/TenantIsolationTests.cs`

## Questions you should be able to answer

1. A warden's request reaches a Billing query, but their cookie has no `tenant_id` claim. What exactly happens, and why is that better than returning an empty list?
2. The query filter already hides other tenants' rows. Why is `tenant_id` *also* a concurrency token? Describe the request that would succeed without it.
