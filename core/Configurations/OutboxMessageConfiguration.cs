using Mass.Transit.Outbox.Repo.Replicate.core.Configurations.Converters;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(e => e.EnqueueTime)
            .HasConversion(UtcConverters.UtcNullableConverter);
        builder.Property(e => e.ExpirationTime)
            .HasConversion(UtcConverters.UtcNullableConverter);
        builder.Property(e => e.SentTime)
            .HasConversion(UtcConverters.UtcConverter);
    }
}