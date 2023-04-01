using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Configurations;

[ExcludeFromCodeCoverage]
public class MessageLogConfiguration : IEntityTypeConfiguration<MessageLog>
{
    public void Configure(EntityTypeBuilder<MessageLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => new {x.CorrelationId, x.InvoiceNumber}).IsUnique();
        builder.HasIndex(x => new {x.InvoiceNumber, x.MessageSentAt, x.InvoiceDate});
        builder.HasIndex(x => new {x.InvoiceNumber, x.MessageSentAt, x.InvoiceDate, x.CorrelationId});
        builder.HasIndex(x => new {x.InvoiceNumber, x.InvoiceDate});
    }
}
