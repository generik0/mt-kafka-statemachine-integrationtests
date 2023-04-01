using System.Text.Json.Serialization;
using Confluent.Kafka;
using Dapper;
using FluentValidation;
using Mass.Transit.Outbox.Repo.Replicate.core;
using Mass.Transit.Outbox.Repo.Replicate.core.Consumers;
using Mass.Transit.Outbox.Repo.Replicate.core.Repositories;
using Mass.Transit.Outbox.Repo.Replicate.core.Repositories.SqlTypeHandlers;
using Mass.Transit.Outbox.Repo.Replicate.test.TestFramework.Logging;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;

public class ServiceFixture : IDisposable
{
    public IHost Host { get; set; } = null!;
    public IServiceProvider? ServiceProvider { get; set; }

    public ServiceFixture (NpgSqlDockerComposeFixture npgSqlDockerComposeFixture, 
        AzureRiteStorageFixture azureRiteStorageFixture, 
    KafkaDockerComposeFixture kafkaDockerComposeFixture)
    {

        azureRiteStorageFixture.StartOnce();
        kafkaDockerComposeFixture.StartOnce();
        npgSqlDockerComposeFixture.StartAndEnsureMigrated();
    }
    

    public IServiceProvider GetOrCreateServiceProvider()
    {
        return ServiceProvider ??= CreateServiceProvider();
    }

    private static readonly XUnitLoggerCategoryMinValue[] LogMinValues = {
        new("MassTransit", LogLevel.Information), 
        new("MassTransit.Testing.KafkaTestHarnessHostedService", LogLevel.Warning), 
        new("FastEndpoints.Swagger.ValidationSchemaProcessor", LogLevel.Warning), 
        new("FastEndpoints.StartupTimer", LogLevel.Warning), 
        new("Microsoft.EntityFrameworkCore.Infrastructure", LogLevel.Warning),
        new("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning),
        new("Microsoft.EntityFrameworkCore.Database.Query", LogLevel.Warning),
    };

    private IServiceProvider CreateServiceProvider()
    {
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.Services.AddSingleton<ILoggerProvider>(_ => new XUnitLoggerProvider(TestOutputHelper!, LogMinValues));
            })
            .ConfigureServices((hostContext, services) =>
            {
                SqlMapper.AddTypeHandler(InstantHandler.Default);
                services.AddValidatorsFromAssemblyContaining<InboundCustomerOrder.Consumer>();
                services.AddScoped<IMessageLogRepository, MessageLogRepository>();
                services.AddDbContext<MyDbContext>(x => x.EnableSensitiveDataLogging());
                services.ConfigureKafkaTestOptions(options =>
                {
                    options.CreateTopicsIfNotExists = true;
                    options.TopicNames = new[] { Topic };
                });
                services.AddMassTransitTestHarness(registration =>
                {
                    registration.AddDelayedMessageScheduler();
                    registration.SetKebabCaseEndpointNameFormatter();
                    registration.AddEntityFrameworkOutbox<MyDbContext>(o =>
                    {
                        o.UsePostgres();
                    });
                    AddConsumers(registration);

                    registration.UsingInMemory((context, cfg) =>
                    {
                        cfg.UseDelayedMessageScheduler();
                        cfg.ConfigureEndpoints(context, new DefaultEndpointNameFormatter("Billing_Summary_", true));
                        cfg.ConfigureJsonSerializerOptions(settings =>
                        {
                            settings.Converters.Add(NodaTime.Serialization.SystemTextJson.NodaConverters
                                .InstantConverter);
                            settings.Converters.Add(new JsonStringEnumConverter());
                            return settings;
                        });
                    });

                    registration.AddRider(rider =>
                    {
                        rider.AddConsumer<InboundCustomerOrder.Consumer>();
                        rider.UsingKafka((context, k) =>
                        {
                            k.TopicEndpoint<MyModels.Model.Raw>(Topic, "consumerGroup1", c =>
                            {
                                c.AutoOffsetReset = AutoOffsetReset.Latest;
                                c.ConfigureConsumer<InboundCustomerOrder.Consumer>(context);
                                c.SetValueDeserializer(new InboundCustomerOrder.Deserializer());
                            });
                        });
                    });

                    registration.AddSagaStateMachine<MyStateMachine, MyState, MyStateDefinition>()
                        .EntityFrameworkRepository(r =>
                        {
                            r.ExistingDbContext<MyDbContext>();
                            r.UsePostgres();
                        });
                });
            }).Build();

        Host.StartAsync().Wait();
        return Host.Services;
    }

    private static void AddConsumers(IRegistrationConfigurator registration)
    {
        foreach (var consumer in typeof(InboundCustomerOrder.Consumer).Assembly.GetTypes()
                     .Where(p =>
                     {
                         return p.GetInterfaces().Any(y => y == typeof(IConsumer)) &&
                                p is { IsClass: true, IsAbstract: false } &&
                                p.GetMethods().Any(x => x.Name.Equals("Consume")) &&
                                p != typeof(InboundCustomerOrder.Consumer);
                     }))
        {
            registration.AddConsumer(consumer);
        }
    }

    public static string Topic = "MyTopic";
    public ITestOutputHelper? TestOutputHelper { get; set; }

    public void Dispose()
    {
        Host.StopAsync().Wait();
    }
}