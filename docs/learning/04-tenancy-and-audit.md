# 04 — Tenancy and audit

## What was built

The plumbing that keeps each hostel's data to itself and records who changed what. All of it is in `Basera.Shared`, ready for the first module that owns tables:

- **Marker interfaces** in `Domain/`: `ITenantOwned` (`TenantId`), `IAuditable` (`CreatedAt/By`, `UpdatedAt/By`) and `ISoftDeletable` (`DeletedAt/By`). An entity opts in by implementing them with private setters. There is also `GuidV7`, which makes time-ordered ids.
- **`Tenancy/`**: `ITenantContext`, the scoped `TenantContext`, `TenantNotResolvedException`, and `TenantResolutionMiddleware`, which copies the `tenant_id` claim into the context (`app.UseBaseraTenancy()`).
- **`Users/`**: `ICurrentUser` (from the `sub` claim) for the audit columns, and `BaseraClaimTypes`.
- **`Persistence/`**:
  - `TenantDbContext`, the base class for every module DbContext.
  - `TenantInterceptor` and `AuditInterceptor`, which run on `SaveChanges`.
  - `MoneyConverter`.
  - `AddModuleDbContext<T>()`, which wires it all together for a module.
- **Tests:**
  - `TestDatabase` gives the integration tests a real PostgreSQL (see below).
  - `TenantIsolationTests` proves tenant A cannot see or change tenant B's rows.

A module will write:

```csharp
public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options, ITenantContext tenant)
    : TenantDbContext(options, tenant)
{
    protected override string Schema => "billing";

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
}

// in BillingModule.AddModule:
services.AddModuleDbContext<BillingDbContext>(configuration);
```

## How tenancy works

**Reading.** `TenantDbContext` adds a global query filter to every `ITenantOwned` entity: `e.TenantId == CurrentTenantId`. `CurrentTenantId` is a property of the context. EF Core reads a context member in a filter afresh for every query and sends it as a SQL parameter, so one compiled model serves all tenants. If no tenant is set, reading that property throws `TenantNotResolvedException`. A forgotten tenant is a loud bug, not an empty page or, worse, every tenant's rows.

**Writing.** `TenantInterceptor` runs before every save:

- On insert it stamps the current tenant, so services never set `TenantId`.
- It refuses to insert a row for another tenant, and it refuses to change a loaded row's tenant.
- With no tenant in scope, it throws.

**The guessed-id hole.** Filters only apply to queries. Someone could build an object with another tenant's id, `Attach` it and save. EF would then send `UPDATE ... WHERE id = @id`, which no filter sees. So `tenant_id` is also marked as a *concurrency token*, and EF sends `UPDATE ... WHERE id = @id AND tenant_id = @tenant`. Against another tenant's row that matches nothing, and EF throws `DbUpdateConcurrencyException`. The isolation test tries exactly this attack, for both update and delete.

**Where the tenant comes from.** Only from the signed-in user's `tenant_id` claim (ADR-003), via the middleware. A URL or form field is never trusted. A background job opens one DI scope per tenant and calls `TenantContext.Set`. Switching tenants inside a scope is refused, because entities already loaded belong to the first tenant.

**Escaping the filter.** `IgnoreQueryFilters()` is the only way out, and it also drops the soft-delete filter. Platform admin will use it, with an audit record (ARCHITECTURE §3.5).

## How auditing works

`AuditInterceptor` sets `CreatedAt/By` on insert and `UpdatedAt/By` on update, using `IClock` and `ICurrentUser`. A null user means a background job. Created values are marked unmodified on every update, so they can never be overwritten, even by mistake.

Deleting an `ISoftDeletable` row is turned into an update of `DeletedAt/By`, and the query filter hides it from then on. Only Resident, Property, Room, Bed and Notice may be soft-deletable. Financial rows are append-only (ADR-010), so they never implement it.

The *audit log* of PRODUCT_SPEC §4.11 (who changed a resident's phone, who reversed a receipt) is separate. It belongs to the Audit module and will be fed by domain events (Feature 5). The columns here answer "who touched this row last"; the log answers "what changed".

## Conventions applied to every module DbContext

- Tables and the `__EFMigrationsHistory` table live in the module's schema, so each module migrates independently (ARCHITECTURE §3.1).
- snake_case names via EFCore.NamingConventions: `CreatedAt` becomes `created_at`. The history table's columns become `migration_id` and `product_version`. That's harmless, as long as every context uses the same convention, which `AddModuleDbContext` guarantees.
- `Money` is stored as `bigint` paisa by convention, so no entity configures it.
- The connection string is `ConnectionStrings:Default`, read when the first context is created, not at startup. So the host and the non-database tests run without a database. A missing or placeholder value gives an error that names the `dotnet user-secrets` command.

## Why it is shaped this way

**Why filters and interceptors, not "remember to add `Where(TenantId == ...)`".** A shared database is only safe if isolation is the default, not a habit. With the filter in the base class, a developer has to type `IgnoreQueryFilters()` to see another tenant's data, and that call is easy to search for in review.

**Why throw instead of returning nothing.** An unresolved tenant that filtered to zero rows would look like an empty hostel, a bug that would hide for a long time. An exception shows up in the first test.

**Why the tenant check is in an interceptor, not in each service.** It runs for every save of every module, including ones written later by someone who never read this page.

**Why `GuidV7`.** Random v4 GUIDs scatter inserts across the whole primary key index. v7 ids begin with the time, so new rows go to the end of the index, like an auto-increment, while still being safe to generate in C# before saving. .NET 8 has no built-in; `GuidV7.New()` produces the RFC 9562 layout that .NET 9's `Guid.CreateVersion7()` does.

**Why a test-only `ProbeDbContext`.** No module owns tables yet. A small context in the test project proves the building block against PostgreSQL, through the same `AddBaseraShared` + `AddModuleDbContext` path a module will use, without inventing a fake domain inside a real module. Its table is created directly (`CreateTablesAsync`) because test-only code should not ship migrations.

## A real database for integration tests

### What was built

`TestDatabase` in `tests/Basera.IntegrationTests/Infrastructure`: an xUnit collection fixture that gives the whole test run one PostgreSQL database, migrated and ready. `BaseraWebFactory` boots the real host pointed at it, and `DatabaseCollection` is the `[Collection]` name test classes join to share it.

It works in one of two ways:

1. **Your own server (`BASERA_TEST_PG` set).** The variable holds a connection string to a PostgreSQL server you already run, for example the one installed on Windows. At the start of the run the fixture connects to the database named in it (normally `postgres`), runs `CREATE DATABASE basera_test_1a2b3c4d` with a fresh short guid, and applies migrations. At the end it runs `DROP DATABASE ... WITH (FORCE)`.
2. **A throwaway container (variable not set).** Testcontainers starts `postgres:16` in Docker and removes it afterwards. This is what CI uses. Without Docker the run fails with a message naming both options.

Both print what they did, e.g. `[TestDatabase] Created test database basera_test_1a2b3c4d on localhost:5432 (BASERA_TEST_PG).` and later `[TestDatabase] Dropped test database basera_test_1a2b3c4d.`

### Why it is shaped this way

**Why a real PostgreSQL, not SQLite or the EF in-memory provider.** The things we most need to test are PostgreSQL behaviour: global query filters translated to SQL, concurrency tokens in `WHERE` clauses, `timestamptz`, `bigint` money, schemas per module. The in-memory provider ignores most of that, and SQLite is a different database. A test that passes on them proves little about production.

**Why a new database each run instead of reusing `basera`.** Tests must start from a known state and must never touch your development data. A uniquely named database cannot collide with `basera`, with another run, or with a previous run that crashed. The random suffix is also why two terminals can run the tests at the same time.

**Why one database per run, not per test class.** Creating a database and migrating every module costs seconds; doing it per class would multiply that by the number of classes. Instead each test uses fresh tenant ids, and because every query is tenant-filtered, tests cannot see each other's rows. That is the same isolation the product relies on, so the tests exercise it constantly. The cost is that classes in the collection run one after another, not in parallel.

**Why migrations, not `EnsureCreated`.** `EnsureCreated` builds the schema straight from the model and skips the migration files, so a broken or missing migration would go unnoticed until deployment. Applying the real migrations tests them too. The fixture asks the host's own service collection for every registered `DbContext`, so a module's migrations are picked up as soon as it registers its context.

**Why `WITH (FORCE)` and `ClearAllPools`.** Npgsql keeps connections open in a pool after a test finishes, and PostgreSQL refuses to drop a database with open connections. Clearing the pool closes ours, and `FORCE` (PostgreSQL 13+) ends any that remain.

**Why an environment variable, not a config file.** The password stays out of the repository, and the same tests run unchanged in CI, where the variable is absent and Testcontainers takes over.

### Try it

```powershell
setx BASERA_TEST_PG "Host=localhost;Port=5432;Username=postgres;Password=<your postgres password>"
# open a new terminal so it sees the variable, then:
dotnet test tests/Basera.IntegrationTests
```

If a run is killed half-way, its database stays behind. List leftovers with `psql -U postgres -c "\l basera_test_*"` and drop them with `DROP DATABASE basera_test_xxxxxxxx;`.

## Read in this order

1. `src/BuildingBlocks/Basera.Shared/Persistence/TenantDbContext.cs`: the filter, the concurrency token, the conventions.
2. `src/BuildingBlocks/Basera.Shared/Persistence/TenantInterceptor.cs` and `AuditInterceptor.cs`: the write side.
3. `src/BuildingBlocks/Basera.Shared/Tenancy/`: where the tenant comes from.
4. `tests/Basera.IntegrationTests/Tenancy/TenantIsolationTests.cs`: the isolation, attack and audit tests.
5. `tests/Basera.IntegrationTests/Infrastructure/TestDatabase.cs`: the two modes, create, migrate, drop.
6. `tests/Basera.IntegrationTests/Infrastructure/BaseraWebFactory.cs`: how the host is pointed at the test database and how DbContexts are found.
7. `docs/ARCHITECTURE.md` §3.5, §4.5, §5, §6, §11.
