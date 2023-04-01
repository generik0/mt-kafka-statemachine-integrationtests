using System.Diagnostics.CodeAnalysis;
using Mass.Transit.Outbox.Repo.Replicate.core.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Consumers;

public static class ProcessDuplicateInvoice
{
    public class Consumer : IConsumer<Query>
    {
        private readonly IMessageLogRepository _messageLogRepository;
        private readonly ILogger<Consumer> _logger;

        public Consumer(IMessageLogRepository messageLogRepository, ILogger<Consumer> logger)
        {
            _messageLogRepository = messageLogRepository;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<Query> context)
        {
            var (invoiceNumber, messageSentAt, invoiceDate, correlationId) = context.Message;
            var messageLogs = await _messageLogRepository.GetAllAsync(invoiceNumber, context.CancellationToken);
            if (messageLogs.All(x => x.CorrelationId == correlationId))
            {
                _logger.LogDebug($"Message is valid and has no other logs. InvoiceNumber: {invoiceNumber}");
                await context.RespondAsync(new Response(context.CorrelationId!.Value, true));
                return;
            }

            var otherMessageLogs = messageLogs
                .Where(x => x.CorrelationId != context.Message.CorrelationId).ToArray();

            if (otherMessageLogs.All(x => x.InvoiceDate < invoiceDate))
            {
                _logger.LogDebug($"Message is valid as it has no duplicates with higher invoiceDate. " +
                                 $"InvoiceNumber: {invoiceNumber}. InvoiceDate: {invoiceDate}");
                await context.RespondAsync(new Response(context.CorrelationId!.Value, true));
                return;
            } 
                
            if (otherMessageLogs.All(x => x.MessageSentAt < messageSentAt && x.InvoiceDate <= invoiceDate))
            {
                _logger.LogDebug($"Message is valid as the message is sent more recent. " +
                                 $"InvoiceNumber: {invoiceNumber}. InvoiceDate: {invoiceDate}. MessageSentAt: {messageSentAt}");
                await context.RespondAsync(new Response(context.CorrelationId!.Value, true));
                return;
            }
                
            _logger.LogInformation($"Message is not valid as the there is a more recent message consumed. " +
                                   $"InvoiceNumber: {invoiceNumber}. InvoiceDate: {invoiceDate}. MessageSentAt: {messageSentAt}");
            await context.RespondAsync(new Response(context.CorrelationId!.Value, false));
        }
    }

    [ExcludeFromCodeCoverage]
    public record Query(string InvoiceNumber, Instant MessageSentAt, Instant InvoiceDate, Guid CorrelationId) : IConsumer;

    [ExcludeFromCodeCoverage]
    public record Response(Guid CorrelationId, bool IsValid);
}