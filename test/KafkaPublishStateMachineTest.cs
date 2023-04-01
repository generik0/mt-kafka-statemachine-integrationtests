using Bogus;
using Confluent.Kafka;
using FluentAssertions;
using Mass.Transit.Outbox.Repo.Replicate.core;
using Mass.Transit.Outbox.Repo.Replicate.core.Common.Consumers;
using Mass.Transit.Outbox.Repo.Replicate.core.Consumers;
using Mass.Transit.Outbox.Repo.Replicate.core.Repositories;
using Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;
using MassTransit.Context;
using MassTransit.KafkaIntegration.Serializers;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using Xunit.Sdk;
using static Mass.Transit.Outbox.Repo.Replicate.test.TestFramework.Assertions.DelayedAssertions;


namespace Mass.Transit.Outbox.Repo.Replicate.test;

[Collection(nameof(MyTestCollection))]
public class KafkaPublishStateMachineTest : IClassFixture<ServiceFixture>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ServiceFixture _serviceFixture;
    private readonly ITestHarness _harness;
    private readonly Faker _faker;

    public KafkaPublishStateMachineTest(ITestOutputHelper testOutputHelper, ServiceFixture serviceFixture)
    {
        serviceFixture.TestOutputHelper = testOutputHelper;
        _testOutputHelper = testOutputHelper;
        _serviceFixture = serviceFixture;
        serviceFixture.GetOrCreateServiceProvider();
        _harness = serviceFixture.ServiceProvider.GetTestHarness();
        _faker = new Faker();
    }

    [Theory]
    [InlineData("Sample FINAL_INVOICE.json", "2303CNOEI00000002097")]
    [InlineData("Sample CREDIT_NOTES.json", "2303CNOEC00000002099")]
    public async Task Should_Receive_CustomerInvoice(string sampleFilename, string oldInvoiceNumber)
    {
        var expectedInvoiceNumber = _faker.Random.AlphaNumeric(oldInvoiceNumber.Length);
        new int[50].AsParallel().ForAll(_ =>
        {
            ProduceMessageHappy(oldInvoiceNumber, sampleFilename).Wait();
        });

        var correlationId = await ProduceMessageHappy(oldInvoiceNumber, sampleFilename, expectedInvoiceNumber);

        _testOutputHelper.WriteLine($"Invoice Number Is; {expectedInvoiceNumber}");
        _testOutputHelper.WriteLine($"CorrelationId Is; {correlationId}");

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
        await AssertLogMessage(expectedInvoiceNumber, correlationId, "InProgress");
    }


    private async Task AssertLogMessage(string expectedInvoiceNumber, Guid correlationId, string status)
    {
        await using var scoped = _serviceFixture.ServiceProvider!.CreateAsyncScope();
        var serviceProvider = scoped.ServiceProvider;
        var messageLogRepository = serviceProvider.GetRequiredService<IMessageLogRepository>();
        var logMessage = await messageLogRepository.GetAsync(expectedInvoiceNumber, correlationId, CancellationToken.None);

        logMessage.Should().NotBeNull();
        logMessage!.Status.Should().Be(status);
    }



    private async Task<Guid> ProduceMessageHappy(string oldInvoiceNumber, string sampleFilename,
        string? expectedInvoiceNumber = null)
    {
        expectedInvoiceNumber ??= _faker.Random.AlphaNumeric(oldInvoiceNumber.Length);
        var combine = Path.Combine(".", "test", "Test_Data", sampleFilename);
        var json = await File.ReadAllTextAsync(combine);

        return await ProcessMessage(oldInvoiceNumber, expectedInvoiceNumber, json);
    }


    private async Task<Guid> ProcessMessage(string oldInvoiceNumber, string expectedInvoiceNumber, string json)
    {
        await using var scoped = _serviceFixture.ServiceProvider!.CreateAsyncScope();
        var serviceProvider = scoped.ServiceProvider;
        using var p =
            new ProducerBuilder<Null, string>(
                new ProducerConfig((ClientConfig)serviceProvider.GetService(typeof(ClientConfig))!)).Build();
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
        return correlationId;
    }


    [Theory]
    [InlineData("Sample FINAL_INVOICE.json", "2303CNOEI00000002097", "\"invoiceType\": \"O\"","\"invoiceType\": \"None\"")]
    [InlineData("Sample CREDIT_NOTES.json", "2303CNOEC00000002099", "\"invoiceType\": \"N\"", "\"invoiceType\": \"None\"")]
    [InlineData("Sample FINAL_INVOICE.json", "2303CNOEI00000002097", "\"type\": \"FINANCIAL_PLATFORM_EXPORT\"", "\"type\": \"None\"")]
    [InlineData("Sample CREDIT_NOTES.json", "2303CNOEC00000002099", "\"type\": \"FINANCIAL_PLATFORM_EXPORT\"", "\"type\": \"None\"")]
    public async Task Should_Receive_CustomerInvoiceWithBadType(string sampleFilename, string oldInvoiceNumber, string oldType, string newType)
    {
        var expectedInvoiceNumber = _faker.Random.AlphaNumeric(oldInvoiceNumber.Length);

        new int[50].AsParallel().ForAll(_ =>
        {
            ProduceMessageHappy(oldInvoiceNumber, sampleFilename).Wait();
        });

        var combine = Path.Combine(".", "test", "Test_Data", sampleFilename);
        var json = (await File.ReadAllTextAsync(combine))
            .Replace(oldType, newType, StringComparison.InvariantCulture);

        var correlationId = await ProcessMessage(oldInvoiceNumber, expectedInvoiceNumber, json);
        
        var sagaHarness = _harness.GetSagaStateMachineHarness<MyStateMachine, MyState>();
        sagaHarness.Exists(correlationId, TimeSpan.FromSeconds(1)).Should().NotBeNull();
        (await sagaHarness.Exists(x => x.CorrelationId == correlationId, machine => machine.ProcessBlobRequest!.Pending,
            TimeSpan.FromSeconds(5))).Should().NotBeNull();

        (await sagaHarness.Exists(x => x.CorrelationId == correlationId, machine => machine.Ignored,
            TimeSpan.FromSeconds(30))).Should().NotBeNull();

        (await _harness.Published.Any<ProcessDuplicateInvoice.Query>(x =>
            x.Context.Message.CorrelationId == correlationId)).Should().BeFalse();
        
        await AssertEvent(async () => await _harness.Consumed.Any<LogMessageCommonUpdate.Command>(
                x => x.Context.Message.CorrelationId == correlationId), TimeSpan.FromSeconds(5),
            "Saga should have update log"); 

        await AssertLogMessage(expectedInvoiceNumber, correlationId, "Ignored");
        
    }

}