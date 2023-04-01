using Bogus;
using Confluent.Kafka;
using FluentAssertions;
using Mass.Transit.Outbox.Repo.Replicate.core;
using Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;
using MassTransit.Context;
using MassTransit.KafkaIntegration.Serializers;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static Mass.Transit.Outbox.Repo.Replicate.test.TestFramework.Assertions.DelayedAssertions;


namespace Mass.Transit.Outbox.Repo.Replicate.test;

[Collection(nameof(TestCollection))]
public class KafkaPublishStateMachineTest : IClassFixture<ServiceFixture>
{
    private readonly ServiceFixture _serviceFixture;
    private readonly ITestHarness _harness;
    private readonly Faker _faker;

    public KafkaPublishStateMachineTest(ITestOutputHelper testOutputHelper, ServiceFixture serviceFixture)
    {
        serviceFixture.TestOutputHelper = testOutputHelper;
        _serviceFixture = serviceFixture;
        serviceFixture.GetOrCreateServiceProvider();
        _harness = serviceFixture.ServiceProvider.GetTestHarness();
        _faker = new Faker();
    }

    [Fact]
    public async Task Test1()
    {
        const string oldInvoiceNumber = "2303CNOEI00000002097";
        var expectedInvoiceNumber = _faker.Random.AlphaNumeric(oldInvoiceNumber.Length);
        var json = await File.ReadAllTextAsync(Path.Combine(".", "test", "Test_Data", "InboundCustomerOrder", "Invoice.json"));

        await using var scoped = _serviceFixture.ServiceProvider!.CreateAsyncScope();
        var serviceProvider = scoped.ServiceProvider;
        using var p = new ProducerBuilder<Null, string>(new ProducerConfig((ClientConfig)serviceProvider.GetService(typeof(ClientConfig))!)).Build();
        var correlationId = Guid.NewGuid();
        var sendContext = new MessageSendContext<string>(json)
        {
            CorrelationId = correlationId
        };
        var replace = json.Replace(oldInvoiceNumber, expectedInvoiceNumber, StringComparison.InvariantCulture);
        var message = new Message<Null, string>
        {
            Value = replace,
            Headers = DictionaryHeadersSerialize.Serializer.Serialize(sendContext)
        };

        var deliveryResult = await p.ProduceAsync(ServiceFixture.Topic, message, _harness.CancellationToken);
        AssertEvent(() => deliveryResult.Status == PersistenceStatus.Persisted, TimeSpan.FromSeconds(10),
            "Saga should Produce to kafka topic");

        await AssertEvent(async () => 
            await _harness.Published.Any<MyModels.Submit>(x => x.Context.Message.CorrelationId == correlationId), TimeSpan.FromSeconds(30), "Inbound success should ends with Submit");

        var sagaHarness = _harness.GetSagaStateMachineHarness<MyStateMachine, MyState>();
        sagaHarness.Exists(correlationId, TimeSpan.FromSeconds(1)).Should().NotBeNull();
        (await sagaHarness.Exists(x => x.CorrelationId == correlationId, machine => machine.ProcessBlobRequest!.Pending,
            TimeSpan.FromSeconds(10))).Should().NotBeNull();
        (await sagaHarness.Exists(x => x.CorrelationId == correlationId, machine => machine.ProcessLogMessageRequest!.Pending,
            TimeSpan.FromSeconds(30))).Should().NotBeNull();

        (await sagaHarness.Exists(x => x.CorrelationId == correlationId, machine => machine.ProcessNext,
            TimeSpan.FromSeconds(30))).Should().NotBeNull();
        var saga = sagaHarness.Sagas.Select(x => x.CorrelationId == correlationId).Last().Saga!;
        saga.BlobUrl.Should().NotBeNull();
    }
}