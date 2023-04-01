using Mass.Transit.Outbox.Repo.Replicate.core.Configurations.Converters;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Configurations;

public class InboxStateConfiguration : IEntityTypeConfiguration<InboxState>
{
    public void Configure(EntityTypeBuilder<InboxState> builder)
    {
        builder.Property(e => e.Delivered)
            .HasConversion(UtcConverters.UtcNullableConverter);
        builder.Property(e => e.Consumed)
            .HasConversion(UtcConverters.UtcNullableConverter);
        builder.Property(e => e.ExpirationTime)
            .HasConversion(UtcConverters.UtcNullableConverter);
        builder.Property(e => e.Received)
            .HasConversion(UtcConverters.UtcConverter);
    }

    
}