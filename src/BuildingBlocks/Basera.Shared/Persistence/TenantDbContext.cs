using System.Linq.Expressions;
using System.Reflection;
using Basera.Shared.Domain;
using Basera.Shared.Outbox;
using Basera.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Basera.Shared.Persistence;

/// <summary>
/// Base class for every module DbContext (ARCHITECTURE §3, §6). It puts the module's tables and its
/// migrations history in the module's own schema, filters <see cref="ITenantOwned"/> rows by the current
/// tenant and hides deleted <see cref="ISoftDeletable"/> rows. It also makes <c>tenant_id</c> part of every
/// UPDATE and DELETE, stores <see cref="Money"/> as <c>bigint</c> paisa, and maps the shared outbox table so
/// reliable domain events commit with the change. Register it with
/// <see cref="PersistenceServiceCollectionExtensions.AddModuleDbContext{TContext}"/>.
/// </summary>
public abstract class TenantDbContext(DbContextOptions options, ITenantContext tenantContext) : DbContext(options)
{
    private static readonly PropertyInfo CurrentTenantIdProperty =
        typeof(TenantDbContext).GetProperty(nameof(CurrentTenantId), BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>The PostgreSQL schema this module owns, e.g. "billing".</summary>
    protected abstract string Schema { get; }

    /// <summary>
    /// Read by the query filter each time a query runs (EF treats a DbContext member in a filter as a
    /// query parameter), so one compiled model serves every tenant. Throws when no tenant is set.
    /// </summary>
    private Guid CurrentTenantId => tenantContext.RequiredTenantId;

    /// <summary>The module's entity configuration; filters are added after it runs.</summary>
    protected abstract void ConfigureModel(ModelBuilder modelBuilder);

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Keeps the connection string from registration and adds the per-module history table (§3.1).
        optionsBuilder.UseNpgsql(npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema));
    }

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        // Raised events are collected by DomainEventInterceptor, never stored as columns.
        modelBuilder.Ignore<IDomainEvent>();
        ConfigureModel(modelBuilder);
        OutboxMessageConfiguration.Map(modelBuilder, excludeFromMigrations: true);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(type => type.BaseType is null && !type.IsOwned()))
        {
            var clrType = entityType.ClrType;
            if (typeof(IHasDomainEvents).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).Ignore(nameof(IHasDomainEvents.DomainEvents));
            }

            var isTenantOwned = typeof(ITenantOwned).IsAssignableFrom(clrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);

            if (isTenantOwned)
            {
                // Adds "AND tenant_id = @original" to UPDATE and DELETE, so a stub attached with a guessed id
                // cannot change another tenant's row: the statement matches nothing and EF throws.
                modelBuilder.Entity(clrType).Property(nameof(ITenantOwned.TenantId)).IsConcurrencyToken();
            }

            if (isTenantOwned || isSoftDeletable)
            {
                modelBuilder.Entity(clrType).HasQueryFilter(BuildFilter(clrType, isTenantOwned, isSoftDeletable));
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Money>().HaveConversion<MoneyConverter>();
    }

    // EF allows one filter per entity type, so tenant and soft delete are combined:
    // e => e.TenantId == this.CurrentTenantId && e.DeletedAt == null
    private LambdaExpression BuildFilter(Type clrType, bool tenant, bool softDelete)
    {
        var entity = Expression.Parameter(clrType, "e");
        var conditions = new List<Expression>();
        if (tenant)
        {
            conditions.Add(Expression.Equal(
                Expression.Property(entity, nameof(ITenantOwned.TenantId)),
                Expression.Property(Expression.Constant(this), CurrentTenantIdProperty)));
        }

        if (softDelete)
        {
            conditions.Add(Expression.Equal(
                Expression.Property(entity, nameof(ISoftDeletable.DeletedAt)),
                Expression.Constant(null, typeof(DateTimeOffset?))));
        }

        return Expression.Lambda(conditions.Aggregate(Expression.AndAlso), entity);
    }
}
