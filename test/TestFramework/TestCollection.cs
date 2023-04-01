using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;

[CollectionDefinition(nameof(TestCollection))]
public class TestCollection : ICollectionFixture<NpgSqlDockerComposeFixture>, 
    ICollectionFixture<AzureRiteStorageFixture>, 
    ICollectionFixture<KafkaDockerComposeFixture>
{
}