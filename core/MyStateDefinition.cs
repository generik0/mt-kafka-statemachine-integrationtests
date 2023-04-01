using MassTransit;

namespace Mass.Transit.Outbox.Repo.Replicate.core;

public class MyStateDefinition : SagaDefinition<MyState>
{
    private readonly IServiceProvider _provider;
    private const int ConcurrencyLimit = 8;


    public MyStateDefinition(IServiceProvider provider)
    {
        _provider = provider;
        Endpoint(e =>
        {
            e.Name = $"{nameof(MyState)}-queue";
            e.PrefetchCount = ConcurrencyLimit;
        });
    }

    protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator,
        ISagaConfigurator<MyState> consumerConfigurator)
    {
        endpointConfigurator.UseMessageRetry(r => r.Intervals(TimeSpan.FromMinutes(1)));
        var partition = endpointConfigurator.CreatePartitioner(ConcurrencyLimit);

        consumerConfigurator.Message<MyModels.Submit>(x => x.UsePartitioner(partition, u => u.Message.InvoiceNumber));
        consumerConfigurator.Message<MyModels.StartupSubmit>(x =>
            x.UsePartitioner(partition, u => u.Message.InvoiceNumber));
        consumerConfigurator.Message<MyModels.ManualRetrySubmit>(x =>
            x.UsePartitioner(partition, u => u.Message.InvoiceNumber));

        endpointConfigurator.UseEntityFrameworkOutbox<MyDbContext>(_provider);
    }
}