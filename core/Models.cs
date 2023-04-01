using System.Diagnostics.CodeAnalysis;
using MassTransit;
using NodaTime;

namespace Mass.Transit.Outbox.Repo.Replicate.core;
#nullable disable

[ExcludeFromCodeCoverage]
public static class MyModels
{
    [ExcludeFromCodeCoverage]
    public static class Model
    {
        public class Raw
        {
            public string CustomerInvoice { get; init; }
        }

        public class Slim
        {
            public MessageHeader MessageHeader { get; init; }
            public InvoiceSlim Invoice { get; init; }

            public class InvoiceSlim
            {
                public InvoiceHeaderSlim InvoiceHeader { get; init; }
                public IReadOnlyCollection<InvoiceItemSlim> InvoiceItem { get; init; }
            }

            public class InvoiceHeaderSlim
            {
                public string InvoiceNumber { get; init; }
                public string InvoiceDate { get; init; }
                public string InvoiceType { get; init; }
            }

            public class InvoiceItemSlim
            {
                public string ItemNumber { get; init; }
            }
        }

        public class Dto
        {
            public MessageHeader MessageHeader { get; init; }
            public InvoiceType Invoice { get; init; }

            public class InvoiceType
            {
                public InvoiceHeader InvoiceHeader { get; init; }
                public IReadOnlyCollection<InvoiceItem> InvoiceItem { get; init; }
            }

            public class InvoiceHeader
            {
                public string InvoiceNumber { get; set; }
                public string InvoiceType { get; init; }
                public string InvoiceDate { get; init; }
                public CompanyCode CompanyCode { get; init; }
                public string TotalNetAmount { get; init; }
                public string TotalTaxAmount { get; init; }
                public string InvoiceCurrency { get; init; }
                public BillTo BillTo { get; init; }
            }

            public class InvoiceItem
            {
                public string ItemNumber { get; init; }
                public string ExchangeRate { get; init; }
                public string ChargeCode { get; init; }
                public string BookingNumber { get; init; }

                /// <summary>
                /// case id -- Finops Id
                /// </summary>
                public string CaseId { get; init; }
            }

            public class CompanyCode
            {
                public string Name1 { get; set; }
            }

            public class BillTo
            {
                public string Partner { get; set; }
            }
        }


        public class MessageHeader
        {
            public string MessageSentAt { get; init; }
            public string Type { get; set; }
            public string MessageId { get; init; }
            public string MessageIdIdentification { get; set; }
            public MessageReceivingPartnerEnum MessageReceivingPartner { get; set; }

            public enum MessageReceivingPartnerEnum : byte
            {
                Unknown = 0,
                FINOPS = 1
            }

        }
    }

    [ExcludeFromCodeCoverage]
    public record Submit
    {
        public Guid CorrelationId { get; set; }
        public Instant InvoiceDate { get; set; }
        public Instant MessageSentAt { get; set; }
        public string InvoiceNumber { get; set; }
        public string BlobUrl { get; set; } = string.Empty;
        public string CustomerInvoice { get; init; }
        public Model.MessageHeader.MessageReceivingPartnerEnum MessageReceivingPartner { get; set; }
        public string InvoiceType { get; set; }
        public string FinancialPlatformType { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public record StartupSubmit : Submit
    {
    }

    [ExcludeFromCodeCoverage]
    public record ManualRetrySubmit : Submit
    {

    }

    [ExcludeFromCodeCoverage]
    public record SendToLogMessage(string InvoiceNumber, string BlobUrl, Instant RegistrationDate) : IConsumer;
}
