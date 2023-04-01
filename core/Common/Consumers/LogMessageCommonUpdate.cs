using System.Diagnostics.CodeAnalysis;
using Mass.Transit.Outbox.Repo.Replicate.core.Repositories;
using MassTransit;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Common.Consumers;

public static class LogMessageCommonUpdate
{
    public class Consumer : IConsumer<Command>
    {
        private readonly IMessageLogRepository _messageLogRepository;

        public Consumer(IMessageLogRepository messageLogRepository)
        {
            _messageLogRepository = messageLogRepository;
        }

        public async Task Consume(ConsumeContext<Command> context)
        {
            var command = context.Message;

            await _messageLogRepository.UpdateAsync(command.InvoiceNumber, command.CorrelationId, new LogMessageUpdate
            {
                IsError = context.Message.IsError,
                Status = context.Message.Status,
                IncrementRetries = context.Message.IncrementRetries,
                Stage = context.Message.Stage,
            }, context.CancellationToken);
        }
    }

    [ExcludeFromCodeCoverage]
    public class Command : IConsumer
    {
        public string InvoiceNumber { get; }
        public Guid CorrelationId { get; }

        public Command(string invoiceNumber, Guid correlationId)
        {
            InvoiceNumber = invoiceNumber;
            CorrelationId = correlationId;
        }

        public string? Stage { get; init; }
        public string? Status { get; init; }
        public bool? IsError { get; set; }
        public bool IncrementRetries { get; set; }
    }
}
