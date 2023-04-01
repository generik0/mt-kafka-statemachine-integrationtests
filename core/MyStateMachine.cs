using Mass.Transit.Outbox.Repo.Replicate.core.Common.Consumers;
using Mass.Transit.Outbox.Repo.Replicate.core.Consumers;
using MassTransit;
using Microsoft.Extensions.Logging;

// ReSharper disable SuggestBaseTypeForParameterInConstructor

namespace Mass.Transit.Outbox.Repo.Replicate.core;

public class MyStateMachine : MassTransitStateMachine<MyState>
{
    private readonly ILogger<MyStateMachine> _logger;

    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    public Event<MyModels.Submit>? Submitted { get; private set; }
    public Event<MyModels.StartupSubmit>? StartupSubmitted { get; private set; }
    public Event<MyModels.ManualRetrySubmit>? ManualRetrySubmit { get; private set; }
    public Request<MyState, BlobStore.Query, BlobStore.Response>? ProcessBlobRequest { get; set; }
    public Request<MyState, LogMessage.Query, LogMessage.Response>? ProcessLogMessageRequest { get; set; }
    public Request<MyState, CustomerInvoiceValidation.Query, CustomerInvoiceValidation.Response>? ProcessCustomerInvoiceValidationRequest { get; set; }


    public MyStateMachine(ILogger<MyStateMachine> logger)
    {
        _logger = logger;
        InstanceState(x => x.CurrentState);

        Event(() => Submitted, x => x.CorrelateById(context => context.Message.CorrelationId));
        Request(() => ProcessBlobRequest);
        Request(() => ProcessLogMessageRequest);
        Request(() => ProcessCustomerInvoiceValidationRequest);


        InitialState();
        ProcessBlobState();
        ProcessLogMessageState();
        ProcessCustomerInvoiceValidationState();

        Next();
    }

    private void InitialState()
    {
        Initially(
            When(Submitted)
                .TransitionTo(Registered)
                .Then(context =>
                {
                    context.Saga.CorrelationId = context.Message.CorrelationId;
                    context.Saga.MessageSentAt = context.Message.MessageSentAt;
                    context.Saga.InvoiceDate = context.Message.InvoiceDate;
                    context.Saga.InvoiceNumber = context.Message.InvoiceNumber;
                    context.Saga.Message = context.Message.CustomerInvoice;
                    context.Saga.InvoiceType = context.Message.InvoiceType;
                    context.Saga.FinancialPlatformType = context.Message.FinancialPlatformType;
                })
                .TransitionTo(ProcessBlob));
    }

    private void ProcessBlobState()
    {
        WhenEnter(ProcessBlob, f =>
        {
            f.Then(x => _logger.LogInformation(
                "Continuing to Financial Operations Save Blob. InvoiceNumber: {0}.", x.Saga.InvoiceNumber));
            return f.Request(ProcessBlobRequest,
                    x => new BlobStore.Query(x.Saga.Message, x.Saga.InvoiceNumber, x.Saga.CorrelationId))
                .TransitionTo(ProcessBlobRequest!.Pending);
        });

        During(ProcessBlobRequest!.Pending,
            When(ProcessBlobRequest.Completed)
                .Then(context => context.Saga.BlobUrl = context.Message.BlobUrl)
                .TransitionTo(ProcessLogMessage),
            When(ProcessBlobRequest.TimeoutExpired)
                .Then(x => _logger.LogError(
                    "Unable to persist invoice to blob.Invoice number due to timeout: {0}. Blob url: {1}", x.Saga.InvoiceNumber, x.Saga.BlobUrl))
                .TransitionTo(TimeoutExpiredBlobFailed),
            When(ProcessBlobRequest.Faulted)
                .Then(x => _logger.LogError(
                    "Unable to persist invoice to blob.Invoice number: {0}. Blob url: {1}", x.Saga.InvoiceNumber, x.Saga.BlobUrl))
                .TransitionTo(FaultedBlobFailed)
        );
    }

    public State FaultedBlobFailed { get; set; }

    private void ProcessLogMessageState()
    {
        WhenEnter(ProcessLogMessage, f =>
        {
            f.Then(x => _logger.LogDebug(
                "Continuing to Financial Operations LogMessage. InvoiceNumber: {0}.", x.Saga.InvoiceNumber));
            return f.Request(ProcessLogMessageRequest,
                    x => new LogMessage.Query(x.Saga.CorrelationId, x.Saga.InvoiceNumber, x.Saga.BlobUrl,
                        x.Saga.MessageSentAt, x.Saga.InvoiceDate, x.Saga.CurrentState))
                .TransitionTo(ProcessLogMessageRequest!.Pending);
        });

        During(ProcessLogMessageRequest!.Pending,
            When(ProcessLogMessageRequest.Completed)
                .Then(x => _logger.LogDebug(
                    "Persist invoice to log message.Invoice number: {0}. Blob url: {1}", x.Saga.InvoiceNumber, x.Saga.BlobUrl))
                .TransitionTo(ProcessCustomerInvoiceValidation),
            When(ProcessLogMessageRequest.TimeoutExpired)
                .Then(x => _logger.LogError(
                    "Unable to persist invoice to log message.Invoice number due to timeout: {0}. Blob url: {1},", x.Saga.InvoiceNumber, x.Saga.BlobUrl))
                .TransitionTo(TimeoutExpiredLogMessageFailed),
            When(ProcessLogMessageRequest.Faulted)
                .Then(x => _logger.LogError(
                    "Unable to persist invoice to log message.Invoice number: {0}. Blob url: {1},", x.Saga.InvoiceNumber, x.Saga.BlobUrl))
                .TransitionTo(FaultedLogMessageFailed)
        );
    }

    private void ProcessCustomerInvoiceValidationState()
    {
        WhenEnter(ProcessCustomerInvoiceValidation, f =>
        {
            f.Then(x => _logger.LogDebug(
                "Continuing to Financial Operations CustomerInvoiceValidation. InvoiceNumber: {0}.. Blob url: {1}", x.Saga.InvoiceNumber, x.Saga.BlobUrl));
            return f.Request(ProcessCustomerInvoiceValidationRequest,
                x => new CustomerInvoiceValidation.Query(x.Saga.InvoiceType, x.Saga.FinancialPlatformType, x.Saga.InvoiceNumber, x.Saga.CorrelationId))
                .TransitionTo(ProcessCustomerInvoiceValidationRequest!.Pending);
        });

        During(ProcessCustomerInvoiceValidationRequest!.Pending,
            When(ProcessCustomerInvoiceValidationRequest.Completed)
                .IfElse(context => !context.Message.IsValid,
                    ignored =>
                        ignored.Then(f =>
                        {
                            _logger.LogInformation("Ignoring message it is not valid InvoiceType/FinancialPlatformType. InvoiceNumber: {0}.",
                                f.Saga.InvoiceNumber);
                            f.Publish(new LogMessageCommonUpdate.Command(f.Saga.InvoiceNumber, f.Saga.CorrelationId)
                            {
                                Status = "Ignored",
                                Stage = "ConsumeTypeIgnoreStage"
                            });
                        }).Finalize(),
                    valid => valid.TransitionTo(ProcessNext)),
            When(ProcessCustomerInvoiceValidationRequest.TimeoutExpired)
                .Then(x =>
                {
                    _logger.LogError(
                        "Unable to process CustomerInvoiceValidation .Invoice number due to timeout: {0}. Blob url: {1},",
                        x.Saga.InvoiceNumber, x.Saga.BlobUrl);
                    x.Publish(new LogMessageCommonUpdate.Command(x.Saga.InvoiceNumber, x.Saga.CorrelationId)
                    {
                        Status = "Failed",
                        Stage = "ConsumeTypeFailedStage",
                        IsError = true
                    });
                })
                .Finalize(),
            When(ProcessCustomerInvoiceValidationRequest.Faulted)
                .Then(x =>
                {
                    _logger.LogError(
                        "Unable to process CustomerInvoiceValidation to log message.Invoice number: {0}. Blob url: {1},",
                        x.Saga.InvoiceNumber, x.Saga.BlobUrl);
                    x.Publish(new LogMessageCommonUpdate.Command(x.Saga.InvoiceNumber, x.Saga.CorrelationId)
                    {
                        Status = "Failed",
                        Stage = "ConsumeTypeFailedStage",
                        IsError = true
                    });
                })
                .Finalize()
        );
    }

    public State FaultedLogMessageFailed { get; set; }

    public State TimeoutExpiredLogMessageFailed { get; set; }

    private void Next()
    {
        WhenEnter(ProcessNext, f => 
            f.Then(x => _logger.LogDebug("Persist invoice to log message.Invoice number: {0}. Blob url: {1}", x.Saga.InvoiceNumber, x.Saga.BlobUrl)));
    }

    public State? Registered { get; set; }
    public State? ProcessBlob { get; set; }
    public State? ProcessLogMessage { get; set; }

    public State? ProcessNext { get; set; }

    public State? TimeoutExpiredBlobFailed { get; set; }
    public State? Ignored { get; set; }
    public State? Success { get; set; }

    public State? ProcessCustomerInvoiceValidation { get; set; }

}

