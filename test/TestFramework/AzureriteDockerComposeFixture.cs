using System.Diagnostics;
using System.Text.RegularExpressions;
using DockerComposeFixture;
using Xunit.Abstractions;
using Xunit.Sdk;
using DockerFixture = DockerComposeFixture.DockerFixture;

namespace Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;
public class AzureRiteStorageFixture
{
    private readonly IMessageSink _diagnosticMessageSink;

    private static readonly object Lock = new();

    public static string StorageConnectionString =>
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;" +
        "QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;" +
        "TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;";

    public AzureRiteStorageFixture(IMessageSink diagnosticMessageSink)
    {
        _diagnosticMessageSink = diagnosticMessageSink;
        StartOnce();
    }

    private static bool IsRunning()
    {
#if !DEBUG
    return true;
#endif
        var filter = new Regex(Regex.Escape("azurite"));

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
            if (IsRunning())
            {
                _diagnosticMessageSink.OnMessage(new DiagnosticMessage("Azurerite container already started"));
                return;
            }

            dockerFixture.InitOnce(() => new DockerFixtureOptions
            {
                DockerComposeFiles = new[] { "azurelite-docker-compose.yml" },
                CustomUpTest = output => output.Any(l => l.Contains(
                    "Azurite Table service is successfully listening at"))
            });
            _diagnosticMessageSink.OnMessage(new DiagnosticMessage("Azurerite container started"));
        }
    }
}
