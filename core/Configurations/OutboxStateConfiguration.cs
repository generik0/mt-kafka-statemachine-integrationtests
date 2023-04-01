using Mass.Transit.Outbox.Repo.Replicate.core.Configurations.Converters;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Configurations;

public class OutboxStateConfiguration : IEntityTypeConfiguration<OutboxState>
{
    public void Configure(EntityTypeBuilder<OutboxState> builder)
    {
        builder.Property(e => e.Delivered)
            .HasConversion(UtcConverters.UtcNullableConverter);
        builder.Property(e => e.Created)
            .HasConversion(UtcConverters.UtcConverter);
    }
}