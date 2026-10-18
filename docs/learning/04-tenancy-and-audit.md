# 04 — Tenancy and audit

> Feature 4 is in progress. The tenancy and audit sections are added when the feature is done; the
> test database section below came first, because the isolation test needs a real PostgreSQL.

## A real database for integration tests

### What was built

`TestDatabase` in `tests/Basera.IntegrationTests/Infrastructure`: an xUnit collection fixture that gives the whole test run one PostgreSQL database, migrated and ready. `BaseraWebFactory` boots the real host pointed at it, and `DatabaseCollection` is the `[Collection]` name test classes join to share it.

It works in one of two ways:

1. **Your own server (`BASERA_TEST_PG` set).** The variable holds a connection string to a PostgreSQL server you already run, for example the one installed on Windows. At the start of the run the fixture connects to the database named in it (normally `postgres`), runs `CREATE DATABASE basera_test_1a2b3c4d` with a fresh short guid, and applies migrations. At the end it runs `DROP DATABASE ... WITH (FORCE)`.
2. **A throwaway container (variable not set).** Testcontainers starts `postgres:16` in Docker and removes it afterwards. This is what CI uses. Without Docker the run fails with a message naming both options.

Both print what they did, e.g. `[TestDatabase] Created test database basera_test_1a2b3c4d on localhost:5432 (BASERA_TEST_PG).` and later `[TestDatabase] Dropped test database basera_test_1a2b3c4d.`

### Why it is shaped this way

**Why a real PostgreSQL, not SQLite or the EF in-memory provider.** The things we most need to test are PostgreSQL behaviour: global query filters translated to SQL, `xmin` concurrency tokens, `timestamptz`, `bigint` money, schemas per module. The in-memory provider ignores most of that, and SQLite is a different database. A test that passes on them proves little about production.

**Why a new database each run instead of reusing `basera`.** Tests must start from a known state and must never touch your development data. A uniquely named database cannot collide with `basera`, with another run, or with a previous run that crashed. The random suffix is also why two terminals can run the tests at the same time.

**Why one database per run, not per test class.** Creating a database and migrating every module costs seconds; doing it per class would multiply that by the number of classes. Instead each test class seeds its own tenant, and because every query is tenant-filtered, classes cannot see each other's rows. That is the same isolation the product relies on, so the tests exercise it constantly. The cost is that classes in the collection run one after another, not in parallel.

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

### Read in this order

1. `tests/Basera.IntegrationTests/Infrastructure/TestDatabase.cs`: the two modes, create, migrate, drop.
2. `tests/Basera.IntegrationTests/Infrastructure/BaseraWebFactory.cs`: how the host is pointed at the test database and how DbContexts are found.
3. `tests/Basera.IntegrationTests/Infrastructure/TestDatabaseTests.cs`: proof the connection string reaches the right database.
4. `docs/ARCHITECTURE.md` §11.
