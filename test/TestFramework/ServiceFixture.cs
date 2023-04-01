using System.Text.Json.Serialization;
using Confluent.Kafka;
using FluentValidation;
using Mass.Transit.Outbox.Repo.Replicate.core;
using Mass.Transit.Outbox.Repo.Replicate.core.Consumers;
using Mass.Transit.Outbox.Repo.Replicate.core.Repositories;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;

public class ServiceFixture
{
    private static IHost _host = null!;
    public IServiceProvider? ServiceProvider { get; set; }

    public IServiceProvider GetOrCreateServiceProvider()
    {
        return ServiceProvider ??= CreateServiceProvider();
    }

    private static IServiceProvider CreateServiceProvider()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddValidatorsFromAssemblyContaining<InboundCustomerOrder.Consumer>();
                services.AddScoped<IMessageLogRepository, MessageLogRepository>();
                services.AddDbContext<MyDbContext>(x => x.EnableSensitiveDataLogging());
                services.ConfigureKafkaTestOptions(options =>
                {
                    options.CreateTopicsIfNotExists = true;
                    options.TopicNames = new[] { Topic };
                }).AddMassTransitTestHarness(registration =>
                {
                    registration.AddDelayedMessageScheduler();
                    registration.SetKebabCaseEndpointNameFormatter();
                    registration.AddEntityFrameworkOutbox<MyDbContext>(o => { o.UsePostgres(); });
                    AddConsumers(registration);

                    registration.UsingInMemory((context, cfg) =>
                    {
                        cfg.UseDelayedMessageScheduler();
                        cfg.ConfigureEndpoints(context, new DefaultEndpointNameFormatter("default", false));
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
                });
            }).Build();

        _host.Run();
        return _host.Services;
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

    public static string Topic => Guid.NewGuid().ToString();
}