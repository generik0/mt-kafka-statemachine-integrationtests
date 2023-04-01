using System.Diagnostics;
using System.Text.RegularExpressions;
using DockerComposeFixture;
using Mass.Transit.Outbox.Repo.Replicate.core;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;
public class NpgSqlDockerComposeFixture
{
    private readonly IMessageSink _diagnosticMessageSink;
    public static readonly object Lock = new();
    private bool _migrated;

    public NpgSqlDockerComposeFixture(IMessageSink diagnosticMessageSink)
    {
        _diagnosticMessageSink = diagnosticMessageSink;
    }

    public void StartOnce()
    {
        lock (Lock)
        {
            var dockerFixture = new DockerFixture(_diagnosticMessageSink);
            if (IsRunning()) return;

            dockerFixture.InitOnce(() => new DockerFixtureOptions
            {
                DockerComposeFiles = new[] { "postgres-docker-compose.yml" },
                CustomUpTest = output => output.Any(l => l.Contains(
                    "database system is ready to accept connections"))
            });
        }
    }

    private static bool IsRunning()
    {
        var filter = new Regex(Regex.Escape("postgres"));

        var ps = Process.Start(new ProcessStartInfo("docker", "ps")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true
        });
        ps!.WaitForExit(3000);

        return ps.StandardOutput.ReadToEnd()
            .Split('\n')
            .Skip(1)
            .Any(s => filter.IsMatch(s));

    }

    public bool StartAndEnsureMigrated()
    {
        StartOnce();
        if(_migrated) return true;
        _migrated = true;
        var dbContext = new MyDbContext();

        dbContext!.Database.EnsureDeleted();
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseNpgsql(string.Format(ConnectionStringTemplate, "postgres"));
        var migrationDbContext = new DbContext(optionsBuilder.Options);
        migrationDbContext.Database.ExecuteSqlRaw(
            $"CREATE DATABASE \"{DatabaseName}\" WITH OWNER = postgres ENCODING = 'UTF8' TABLESPACE = pg_default CONNECTION LIMIT = -1;");

        dbContext.Database.ExecuteSqlRaw("create extension if not exists pg_trgm;");
        dbContext.Database.EnsureCreated();
        return true;
    }

    public static string ConnectionStringTemplate => "Host=localhost;Database={0};Integrated Security=True;Username=postgres;Include Error Detail=true;Maximum Pool Size=10";
    // "Host=localhost;Port=5432;Database={0};Username=postgres;Password=postgres;";
    public static string DatabaseName = typeof(MyDbContext).Assembly.GetName().Name!;
}
