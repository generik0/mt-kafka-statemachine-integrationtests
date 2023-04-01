using System.Diagnostics.CodeAnalysis;
using NodaTime;

namespace Mass.Transit.Outbox.Repo.Replicate.core;

[ExcludeFromCodeCoverage]
public class MessageLog
{
    public long Id { get; set; }
    public Guid CorrelationId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string Stage { get; set; } = null!;
    public bool IsError { get; set; }
    public int Retries { get; set; }
    public string? BlobUrl { get; set; }
    public Instant InvoiceDate { get; set; }
    public Instant MessageSentAt { get; set; }
}

[ExcludeFromCodeCoverage]
public class LogMessageUpdate
{
    public string? Status { get; set; }
    public string? Stage { get; set; }
    public bool? IsError { get; set; }
    public bool IncrementRetries { get; set; }

}
