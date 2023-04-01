using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using FluentValidation.Results;
using Mass.Transit.Outbox.Repo.Replicate.core.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Consumers;

public static class LogMessage
{
    public class Consumer : IConsumer<Query>
    {
        private readonly IValidator<Query> _validator;
        private readonly IMessageLogRepository _messageLogRepository;
        private readonly ILogger<Consumer> _logger;

        public Consumer(IValidator<Query> validator, IMessageLogRepository messageLogRepository, ILogger<Consumer> logger)
        {
            _validator = validator;
            _messageLogRepository = messageLogRepository;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<Query> context)
        {
            try
            {
                var validation = await _validator.ValidateAsync(context.Message, context.CancellationToken);
                if (!validation.IsValid)
                {
                    throw new Validator.ValidationException(context.Message.InvoiceNumber, validation.Errors);
                }

                var message = context.Message;

                var entity = new MessageLog
                {
                    BlobUrl = message.BlobUrl,
                    CorrelationId = message.CorrelationId,
                    InvoiceDate = context.Message.CreateDate,
                    MessageSentAt = context.Message.RegistrationDate,
                    InvoiceNumber = message.InvoiceNumber,
                    Status = "InProgress",
                    Stage = "My Stage",
                    IsError = false,
                    Retries = 0
                };
                await _messageLogRepository.InsertGetIdAsync(entity, context.CancellationToken);

                await context.RespondAsync(new Response(context.CorrelationId!.Value));
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "LogMessage failed");
                throw;
            }
            
        }
    }

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.BlobUrl).NotNull();
            RuleFor(x => x.InvoiceNumber).NotNull();
            RuleFor(x => x.RegistrationDate).NotNull();
        }

        [Serializable, ExcludeFromCodeCoverage]
        public class ValidationException : Exception

        {
            public ValidationException(string invoiceNumber,
                IEnumerable<ValidationFailure> validationFailures)
                : base($"The invoice cannot be validated for logging. Invoice number: {invoiceNumber}. " +
                       $"Errors: {string.Join(", ", validationFailures.Select(x => x.ErrorMessage))}")
            {
            }
        }
    }

    [ExcludeFromCodeCoverage]
    public record Query(Guid CorrelationId, string InvoiceNumber, string BlobUrl, Instant RegistrationDate,
        Instant CreateDate, string CurrentState) : IConsumer;

    [ExcludeFromCodeCoverage]
    public record Response(Guid CorrelationId);
}