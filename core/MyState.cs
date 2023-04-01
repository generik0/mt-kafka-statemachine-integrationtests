using System.Diagnostics.CodeAnalysis;
using MassTransit;
using NodaTime;

#nullable disable

namespace Mass.Transit.Outbox.Repo.Replicate.core;

[ExcludeFromCodeCoverage]
public class MyState : SagaStateMachineInstance
{
    public long Id { get; set; }
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    public Instant InvoiceDate { get; set; }
    public Instant MessageSentAt { get; set; }

    public string InvoiceNumber { get; set; }
    public string BlobUrl { get; set; }

    public string Message { get; set; }
    public bool IsManualRetry { get; set; }
    public bool IsStartupRetry { get; set; }
    public string FinancialPlatformType { get; set; }
    public string InvoiceType { get; set; }
}