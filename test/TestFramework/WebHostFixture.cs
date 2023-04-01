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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;

namespace Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;

public class WebHostFixture : WebApplicationFactory<Program>
{
    public IHost Host { get; set; } = null!;
    public IServiceProvider? ServiceProvider { get; set; }

    public WebHostFixture (NpgSqlDockerComposeFixture npgSqlDockerComposeFixture, 
        AzureRiteStorageFixture azureRiteStorageFixture, 
    KafkaDockerComposeFixture kafkaDockerComposeFixture)
    {

        azureRiteStorageFixture.StartOnce();
        kafkaDockerComposeFixture.StartOnce();
        npgSqlDockerComposeFixture.StartAndEnsureMigrated();
    }


    public IServiceProvider Start()
    {
        if (_started)
        {
            return Services;
        }
        _started = true;
        try
        {
            var client = CreateClient();
            return Services;
        }
        catch (Exception exception)
        {
            TestOutputHelper!.WriteLine(exception.Message);
            throw;
        }

    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Directory.GetCurrentDirectory());

        if (TestOutputHelper is null)
        {
            throw new ArgumentNullException(nameof(TestOutputHelper));
        }
        builder.ConfigureLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.Services.AddSingleton<ILoggerProvider>(_ => new XUnitLoggerProvider(TestOutputHelper, LogMinValues));
        });
        ConfigureServices(builder);
    }


    public IServiceProvider GetOrCreateServiceProvider()
    {
        return ServiceProvider ??= Start();
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


    protected virtual void ConfigureServices(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
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
        });
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
    private bool _started;
    public ITestOutputHelper? TestOutputHelper { get; set; }
}