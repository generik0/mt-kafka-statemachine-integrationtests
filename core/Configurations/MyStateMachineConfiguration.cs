using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Configurations;

public class MyStateMachineConfiguration : IEntityTypeConfiguration<MyState>
{
    public void Configure(EntityTypeBuilder<MyState> builder)
    {
        builder.HasBaseType((Type)null!);
        builder.HasIndex(x => x.CorrelationId);
        builder.Property(x => x.BlobUrl).HasMaxLength(2048 );
        builder.Property(x => x.CurrentState).HasMaxLength(128);
        builder.Property(x => x.InvoiceNumber).HasMaxLength(128);
    }
}