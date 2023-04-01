using System.Diagnostics.CodeAnalysis;
using Mass.Transit.Outbox.Repo.Replicate.core.Configurations;
using Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;

namespace Mass.Transit.Outbox.Repo.Replicate.core;

public class MyDbContext : DbContext
{
    private readonly string _connectionString = NpgSqlDockerComposeFixture.ConnectionStringTemplate;
    private const string ConnectionStringKey = "BILLING_SUMMARY_DB_CONNECTION_STRING";
    
    public DbSet<MessageLog>? MessageLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ContextConfiguration<MessageLog, MessageLogConfiguration>();
        modelBuilder.ContextConfiguration<MyState, MyStateMachineConfiguration>();

        modelBuilder.AddInboxStateEntity();
        modelBuilder.ContextConfiguration<InboxState, InboxStateConfiguration>();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.ContextConfiguration<OutboxMessage, OutboxMessageConfiguration>();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.ContextConfiguration<OutboxState, OutboxStateConfiguration>();

        
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(_connectionString, builder =>
        {
            builder.CommandTimeout((int)TimeSpan.FromSeconds(30).TotalSeconds);
            builder.EnableRetryOnFailure();
            builder.UseNodaTime();
            builder.MinBatchSize(1);
        });
    }

    private static readonly IReadOnlyCollection<Type> MassTransitNpgsqlTypes =
        new[] { typeof(InboxState), typeof(OutboxMessage), typeof(OutboxState) };

    private static void FixMassTransitNpgsqlTimestamp(DbContext db)
    {
        foreach (var entity in db.ChangeTracker
                     .Entries().Where(entry => MassTransitNpgsqlTypes.Any(x => entry.Metadata.ClrType == x) && entry.State is EntityState.Added or EntityState.Modified))
        {
            foreach (var dt in entity.Properties.Where(x =>
                         (x.Metadata.ClrType == typeof(DateTime) || x.Metadata.ClrType == typeof(DateTime?)) &&
                         x.CurrentValue is DateTime
                         {
                             Kind: DateTimeKind.Unspecified
                         }))
            {
                var currentValue = (DateTime)dt.CurrentValue!;
                dt.CurrentValue = DateTime.MinValue;
                dt.CurrentValue = DateTime.SpecifyKind(currentValue, DateTimeKind.Utc);
            }
        }
    }
}
