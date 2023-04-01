using System.Diagnostics;
using System.Text.RegularExpressions;
using DockerComposeFixture;
using Xunit.Abstractions;

namespace Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;
public class KafkaDockerComposeFixture
{
    private readonly IMessageSink _diagnosticMessageSink;
    private static readonly object Lock = new();

    public KafkaDockerComposeFixture(IMessageSink diagnosticMessageSink)
    {
        _diagnosticMessageSink = diagnosticMessageSink;
        StartOnce();
    }


    private static bool IsRunning()
    {
#if !DEBUG
    return true;
#endif

        var filter = new Regex(Regex.Escape("schema-registry"));

        var ps = Process.Start(new ProcessStartInfo("docker", "ps")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true
        });
        ps.WaitForExit(3000);

        return ps.StandardOutput.ReadToEnd()
        .Split('\n')
            .Skip(1)
            .Any(s => filter.IsMatch(s));

    }


    public void StartOnce()
    {
        lock (Lock)
        {
            var dockerFixture = new DockerFixture(_diagnosticMessageSink);
            if (IsRunning()) return;

            dockerFixture.InitOnce(() => new DockerFixtureOptions
            {
                DockerComposeFiles = new[] { "kafka-docker-compose.yml" },
                CustomUpTest = output => output.Any(l => l.EndsWith(
                    "Server started, listening for requests... (io.confluent.kafka.schemaregistry.rest.SchemaRegistryMain)"))
            });
        }
    }
}
