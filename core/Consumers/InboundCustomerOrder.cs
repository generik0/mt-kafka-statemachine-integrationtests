using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using FluentValidation;
using FluentValidation.Results;
using MassTransit;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Consumers;

public static class InboundCustomerOrder
{
    public class Consumer : IConsumer<MyModels.Model.Raw>
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

        private readonly IValidator<MyModels.Model.Raw> _validatorRaw;
        private readonly IValidator<MyModels.Model.Slim> _validatorSlim;
        private readonly ILogger<Consumer> _logger;


        public Consumer(IValidator<MyModels.Model.Raw> validatorRaw,
            IValidator<MyModels.Model.Slim> validatorSlim, ILogger<Consumer> logger)
        {
            _validatorSlim = validatorSlim;
            _validatorRaw = validatorRaw;
            _logger = logger;
            if (!JsonSerializerOptions.Converters.Any())
            {
                JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            }
        }

        public virtual async Task Consume(ConsumeContext<MyModels.Model.Raw> context)
        {
            await _validatorRaw.ValidateAndThrowAsync(context.Message);
            var model = JsonSerializer.Deserialize<MyModels.Model.Slim>(context.Message.CustomerInvoice, JsonSerializerOptions);
            ArgumentNullException.ThrowIfNull(model?.Invoice);

            var validation = await _validatorSlim.ValidateAsync(model);
            if (!validation.IsValid)
            {
                throw new ValidatorSlim.ValidationException(model.Invoice?.InvoiceHeader?.InvoiceNumber ?? "Invoice number is unknown", validation.Errors);
            }

            if (model.MessageHeader.MessageReceivingPartner != MyModels.Model.MessageHeader.MessageReceivingPartnerEnum.FINOPS)
            {
                _logger.LogInformation($"Ignoring message as it is not delivered to Financial Operations. InvoiceNumber: {0}.", model.Invoice.InvoiceHeader.InvoiceNumber);
                return;
            }

            var invoiceNumber = model.Invoice.InvoiceHeader.InvoiceNumber;

            var messageSentAt = TryInstantUtc(model.MessageHeader.MessageSentAt, invoiceNumber,
                nameof(MyModels.Model.MessageHeader.MessageSentAt));
            var invoiceDate = TryInstantUtc(model.Invoice!.InvoiceHeader!.InvoiceDate, invoiceNumber,
                nameof(MyModels.Model.Slim.Invoice.InvoiceHeader.InvoiceDate));

            var submit = new MyModels.Submit
            {
                CorrelationId = context.CorrelationId ?? NewId.NextSequentialGuid(),
                InvoiceNumber = invoiceNumber,
                MessageSentAt = messageSentAt,
                MessageReceivingPartner = MyModels.Model.MessageHeader.MessageReceivingPartnerEnum.FINOPS,
                InvoiceDate = invoiceDate,
                CustomerInvoice = context.Message.CustomerInvoice,
                FinancialPlatformType = model.MessageHeader.Type,
                InvoiceType = model.Invoice.InvoiceHeader.InvoiceType
            };
            await context.Publish(submit);
        }

        private static Instant TryInstantUtc(string originalValue, string invoiceNumber, string name)
        {
            if (!DateTime.TryParse(originalValue, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
            {
                throw new TryInstantException(invoiceNumber, name, originalValue);
            }
            return Instant.FromDateTimeUtc(value);
        }

        [Serializable, ExcludeFromCodeCoverage]
        public class TryInstantException : Exception
        {
            public TryInstantException(string invoiceNumber,
                string name, string value)
                : base($"The invoice date time cannot be cannot be converted to system date time. " +
                       $"Invoice number: {invoiceNumber}. " +
                       $"Date time property: {name}, Value: {value}")
            {
            }
        }
    }

    public class ValidatorRaw : AbstractValidator<MyModels.Model.Raw>
    {
        public ValidatorRaw()
        {
            RuleFor(x => x.CustomerInvoice).NotNull();
        }

        [Serializable, ExcludeFromCodeCoverage]
        public class ValidationException : Exception
        {
            public ValidationException(string invoiceNumber,
                IEnumerable<ValidationFailure> validationFailures)
                : base($"The invoice cannot be validated. Invoice number: {invoiceNumber}. " +
                       $"Errors: {string.Join(", ", validationFailures.Select(x => x.ErrorMessage))}")
            {
            }
        }
    }

    public class ValidatorSlim : AbstractValidator<MyModels.Model.Slim>
    {
        public ValidatorSlim()
        {
            RuleFor(x => x.MessageHeader).NotNull();
            When(x => x.MessageHeader is not null, () =>
            {
                RuleFor(x => x.MessageHeader.MessageReceivingPartner).NotNull();
                RuleFor(x => x.MessageHeader.MessageSentAt).NotNull();
                RuleFor(x => x.MessageHeader.Type).NotNull();
            });

            RuleFor(x => x.Invoice).NotNull();
            When(x => x.Invoice is not null, () =>
            {
                RuleFor(x => x.Invoice.InvoiceHeader).NotNull();
                RuleFor(x => x.Invoice.InvoiceItem).NotEmpty();
            });
            When(x => x.Invoice?.InvoiceHeader != null, () =>
            {
                RuleFor(x => x.Invoice.InvoiceHeader.InvoiceDate).NotNull();
                RuleFor(x => x.Invoice.InvoiceHeader.InvoiceNumber).NotNull();
                RuleFor(x => x.Invoice.InvoiceHeader.InvoiceType).NotNull();
            });
        }

        [Serializable, ExcludeFromCodeCoverage]
        public class ValidationException : Exception
        {
            public ValidationException(string invoiceNumber,
                IEnumerable<ValidationFailure> validationFailures)
                : base($"The invoice cannot be validated. Invoice number: {invoiceNumber}. " +
                       $"Errors: {string.Join(", ", validationFailures.Select(x => x.ErrorMessage))}")
            {
            }
        }
    }

    public class Deserializer : IDeserializer<MyModels.Model.Raw>
    {
        public MyModels.Model.Raw Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            if (isNull)
            {
                return new MyModels.Model.Raw();
            }

            var jNode = JsonNode.Parse(data);
            return new MyModels.Model.Raw
            {
                CustomerInvoice = jNode!["customerInvoice"]!.ToJsonString()
            };
        }
    }
}