using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Consumers;

public static class CustomerInvoiceValidation
{
    public class Consumer : IConsumer<Query>
    {
        private readonly ILogger<Consumer> _logger;
        public Consumer(ILogger<Consumer> logger)
        {
            _logger = logger;
        }
        public async Task Consume(ConsumeContext<Query> context)
        {
            var message = context.Message;
            if (!IsValidInvoiceType(message.InvoiceType))
            {
                _logger.LogDebug($"Message has invalid InvoiceType {message.InvoiceType} InvoiceNumber: {message.InvoiceNumber}.");
                await context.RespondAsync(new Response(context.CorrelationId!.Value, false));
                return;
            }
            if (!IsValidFinancialPlatformType(message.FinancialPlatformType))
            {
                _logger.LogDebug($"Message has invalid FinancialPlatformType {message.FinancialPlatformType} InvoiceNumber: {message.InvoiceNumber}.");
                await context.RespondAsync(new Response(context.CorrelationId!.Value, false));
                return;
            }
            await context.RespondAsync(new Response(context.CorrelationId!.Value, true));
        }

        private static bool IsValidInvoiceType(string invoiceType) => 
            invoiceType.Equals("O", StringComparison.InvariantCultureIgnoreCase) || invoiceType.Equals("N", StringComparison.InvariantCultureIgnoreCase);

        private static bool IsValidFinancialPlatformType(string financialPlatformType) =>
            financialPlatformType.Equals("FINANCIAL_PLATFORM_EXPORT", StringComparison.InvariantCultureIgnoreCase) || 
            financialPlatformType.Equals("FINANCIAL_PLATFORM_IMPORT", StringComparison.InvariantCultureIgnoreCase);
    }

    [ExcludeFromCodeCoverage]
    public record Query(string InvoiceType, string FinancialPlatformType, string InvoiceNumber, Guid CorrelationId) : IConsumer;

    [ExcludeFromCodeCoverage]
    public record Response(Guid CorrelationId, bool IsValid);
}