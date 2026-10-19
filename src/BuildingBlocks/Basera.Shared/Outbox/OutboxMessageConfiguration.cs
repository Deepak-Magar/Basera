using Microsoft.EntityFrameworkCore;

namespace Basera.Shared.Outbox;

/// <summary>
/// The one mapping of <c>outbox.outbox_message</c>. <see cref="OutboxDbContext"/> owns the table and its
/// migrations; every module DbContext maps it too (excluded from its migrations) so it can insert rows in its
/// own transaction.
/// </summary>
internal static class OutboxMessageConfiguration
{
    public const string Schema = "outbox";
    public const string Table = "outbox_message";

    public static void Map(ModelBuilder modelBuilder, bool excludeFromMigrations)
    {
        modelBuilder.Entity<OutboxMessage>(message =>
        {
            message.ToTable(Table, Schema, table => table.ExcludeFromMigrations(excludeFromMigrations));
            message.HasKey(m => m.Id);
            message.Property(m => m.Id).ValueGeneratedNever();
            message.Property(m => m.Type).HasMaxLength(500);
            message.Property(m => m.Payload).HasColumnType("jsonb");
            message.Property(m => m.LastError).HasMaxLength(2000);

            // What the processor scans for: undelivered, live messages that are due.
            message.HasIndex(m => m.NextAttemptAt)
                .HasDatabaseName("ix_outbox_message_pending")
                .HasFilter("processed_at IS NULL AND NOT is_dead_lettered");
        });
    }
}
