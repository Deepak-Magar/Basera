using Basera.IntegrationTests.Infrastructure;
using Basera.Shared;
using Basera.Shared.Domain;
using Basera.Shared.Domain.Calendar;
using Basera.Shared.Persistence;
using Basera.Shared.Tenancy;
using Basera.Shared.Time;
using Basera.Shared.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Basera.IntegrationTests.Tenancy;

/// <summary>
/// ADR-003: tenant A cannot see or change tenant B's rows. Uses the real registration path
/// (AddBaseraShared + AddModuleDbContext) against PostgreSQL.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class TenantIsolationTests(TestDatabase database) : IAsyncLifetime
{
    private const string Table = ProbeDbContext.Table;
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 6, 15, 0, TimeSpan.Zero);

    // Fresh tenants per test, so tests cannot see each other's rows either.
    private readonly Guid tenantA = GuidV7.New();
    private readonly Guid tenantB = GuidV7.New();
    private readonly Guid user = GuidV7.New();
    private readonly MutableClock clock = new(Now);
    private ServiceProvider services = null!;

    public async Task InitializeAsync()
    {
        services = new ServiceCollection()
            .AddBaseraShared()
            .AddSingleton<IClock>(clock)
            .AddSingleton<ICurrentUser>(new FixedUser(user))
            .AddModuleDbContext<ProbeDbContext>(database.Configuration)
            .BuildServiceProvider(validateScopes: true);

        await ProbeDbContext.EnsureCreatedAsync(services, database.ConnectionString);
    }

    public Task DisposeAsync() => services.DisposeAsync().AsTask();

    [Fact]
    public async Task Each_tenant_sees_only_its_own_rows()
    {
        await AsTenant(tenantA, db => db.Notes.AddRange(new ProbeNote("A1", Money.Zero), new ProbeNote("A2", Money.Zero)));
        var noteB = new ProbeNote("B1", Money.Zero);
        await AsTenant(tenantB, db => db.Notes.Add(noteB));

        (await QueryAs(tenantA, db => db.Notes.Select(n => n.Text).OrderBy(t => t).ToListAsync())).Should().Equal("A1", "A2");
        (await QueryAs(tenantB, db => db.Notes.Select(n => n.Text).ToListAsync())).Should().Equal("B1");
        (await QueryAs(tenantA, db => db.Notes.FindAsync(noteB.Id).AsTask())).Should().BeNull();
        (await QueryAs(tenantA, db => db.Notes.CountAsync(n => n.Id == noteB.Id))).Should().Be(0);
    }

    [Fact]
    public async Task Insert_stamps_the_current_tenant()
    {
        var note = new ProbeNote("A1", Money.Zero);

        await AsTenant(tenantA, db => db.Notes.Add(note));

        note.TenantId.Should().Be(tenantA);
        (await Scalar($"SELECT tenant_id FROM {Table} WHERE id = '{note.Id}'")).Should().Be(tenantA);
    }

    [Fact]
    public async Task Querying_without_a_tenant_throws()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();

        await db.Invoking(d => d.Notes.ToListAsync()).Should().ThrowAsync<TenantNotResolvedException>();
    }

    [Fact]
    public async Task Saving_without_a_tenant_throws()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();
        db.Notes.Add(new ProbeNote("orphan", Money.Zero));

        await db.Invoking(d => d.SaveChangesAsync()).Should().ThrowAsync<TenantNotResolvedException>();
    }

    [Fact]
    public async Task Inserting_a_row_for_another_tenant_is_refused()
    {
        var act = () => AsTenant(tenantA, db =>
        {
            var note = new ProbeNote("smuggled", Money.Zero);
            db.Notes.Add(note);
            db.Entry(note).Property(n => n.TenantId).CurrentValue = tenantB;
        });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Refusing to save*");
        (await QueryAs(tenantB, db => db.Notes.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Moving_a_row_to_another_tenant_is_refused()
    {
        var note = new ProbeNote("A1", Money.Zero);
        await AsTenant(tenantA, db => db.Notes.Add(note));

        var act = () => AsTenant(tenantA, async db =>
        {
            var loaded = await db.Notes.SingleAsync(n => n.Id == note.Id);
            db.Entry(loaded).Property(n => n.TenantId).CurrentValue = tenantB;
        });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Refusing to save*");
        (await Scalar($"SELECT tenant_id FROM {Table} WHERE id = '{note.Id}'")).Should().Be(tenantA);
    }

    [Fact]
    public async Task A_stub_claiming_another_tenants_row_cannot_update_or_delete_it()
    {
        var note = new ProbeNote("A1", Money.Zero);
        await AsTenant(tenantA, db => db.Notes.Add(note));

        // Tenant B guesses A's id and says the row is its own. tenant_id is in the WHERE clause, so nothing matches.
        var update = () => AsTenant(tenantB, db =>
        {
            var stub = StubFor(db, note.Id, tenantB);
            stub.Text = "hijacked";
            db.Entry(stub).Property(n => n.Text).IsModified = true;
        });
        var delete = () => AsTenant(tenantB, db => db.Notes.Remove(StubFor(db, note.Id, tenantB)));

        await update.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await delete.Should().ThrowAsync<DbUpdateConcurrencyException>();
        (await Scalar($"SELECT text FROM {Table} WHERE id = '{note.Id}' AND deleted_at IS NULL")).Should().Be("A1");
    }

    [Fact]
    public async Task Audit_columns_are_set_on_insert_and_update_and_created_values_never_change()
    {
        var note = new ProbeNote("A1", Money.Zero);
        await AsTenant(tenantA, db => db.Notes.Add(note));

        clock.UtcNow = Now.AddHours(2);
        await AsTenant(tenantA, async db =>
        {
            var loaded = await db.Notes.SingleAsync(n => n.Id == note.Id);
            loaded.Text = "A1 edited";
            db.Entry(loaded).Property(n => n.CreatedAt).CurrentValue = Now.AddYears(-1);
        });

        var saved = await QueryAs(tenantA, db => db.Notes.AsNoTracking().SingleAsync(n => n.Id == note.Id));
        saved.Text.Should().Be("A1 edited");
        saved.CreatedAt.Should().Be(Now);
        saved.CreatedBy.Should().Be(user);
        saved.UpdatedAt.Should().Be(Now.AddHours(2));
        saved.UpdatedBy.Should().Be(user);
    }

    [Fact]
    public async Task Removing_a_soft_deletable_row_hides_it_but_keeps_it()
    {
        var note = new ProbeNote("A1", Money.Zero);
        await AsTenant(tenantA, db => db.Notes.Add(note));

        await AsTenant(tenantA, async db => db.Notes.Remove(await db.Notes.SingleAsync(n => n.Id == note.Id)));

        (await QueryAs(tenantA, db => db.Notes.CountAsync())).Should().Be(0);
        var kept = await QueryAs(tenantA, db => db.Notes.IgnoreQueryFilters().SingleAsync(n => n.Id == note.Id));
        kept.DeletedAt.Should().Be(Now);
        kept.DeletedBy.Should().Be(user);
    }

    [Fact]
    public async Task Money_is_stored_as_bigint_paisa()
    {
        var note = new ProbeNote("rent", Money.FromRupees(12_500.50m));
        await AsTenant(tenantA, db => db.Notes.Add(note));

        (await QueryAs(tenantA, db => db.Notes.AsNoTracking().SingleAsync(n => n.Id == note.Id))).Amount
            .Should().Be(Money.FromRupees(12_500.50m));
        (await Scalar($"SELECT amount FROM {Table} WHERE id = '{note.Id}'")).Should().Be(1_250_050L);
    }

    [Fact]
    public async Task Tables_and_migration_history_live_in_the_module_schema_with_snake_case_names()
    {
        var columns = (string)(await Scalar(
            "SELECT string_agg(column_name || ':' || data_type, ',' ORDER BY column_name) FROM information_schema.columns " +
            $"WHERE table_schema = '{ProbeDbContext.SchemaName}' AND table_name = 'probe_note'"))!;

        columns.Split(',').Should().BeEquivalentTo(
            "amount:bigint", "created_at:timestamp with time zone", "created_by:uuid", "deleted_at:timestamp with time zone",
            "deleted_by:uuid", "id:uuid", "tenant_id:uuid", "text:character varying", "updated_at:timestamp with time zone",
            "updated_by:uuid");

        await using var scope = services.CreateAsyncScope();
        var history = scope.ServiceProvider.GetRequiredService<ProbeDbContext>().GetService<IHistoryRepository>();
        history.GetCreateIfNotExistsScript().Should().Contain($"{ProbeDbContext.SchemaName}.\"__EFMigrationsHistory\"");
    }

    private static ProbeNote StubFor(ProbeDbContext db, Guid id, Guid claimedTenant)
    {
        // Set while still detached: key values cannot change once EF tracks the entity.
        var stub = new ProbeNote("stub", Money.Zero);
        var detached = db.Entry(stub);
        detached.Property(n => n.Id).CurrentValue = id;
        detached.Property(n => n.TenantId).CurrentValue = claimedTenant;
        db.Notes.Attach(stub);
        return stub;
    }

    private Task AsTenant(Guid tenant, Action<ProbeDbContext> work) => AsTenant(tenant, db =>
    {
        work(db);
        return Task.CompletedTask;
    });

    // One scope per unit of work, like one HTTP request.
    private async Task AsTenant(Guid tenant, Func<ProbeDbContext, Task> work)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        var db = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();
        await work(db);
        await db.SaveChangesAsync();
    }

    private async Task<T> QueryAs<T>(Guid tenant, Func<ProbeDbContext, Task<T>> query)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        return await query(scope.ServiceProvider.GetRequiredService<ProbeDbContext>());
    }

    private async Task<object?> Scalar(string sql)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public DateTimeOffset NepalNow => UtcNow.ToOffset(SystemClock.NepalOffset);

        public DateOnly Today => DateOnly.FromDateTime(NepalNow.DateTime);

        public NepaliDate TodayBs => NepaliDate.FromDateOnly(Today);
    }

    private sealed class FixedUser(Guid id) : ICurrentUser
    {
        public Guid? UserId => id;
    }
}
