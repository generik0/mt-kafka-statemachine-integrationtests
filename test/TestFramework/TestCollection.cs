using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;

[CollectionDefinition(nameof(TestCollection))]
public class TestCollection : ICollectionFixture<NpgSqlDockerComposeFixture>, 
    ICollectionFixture<AzureRiteStorageFixture>, 
    ICollectionFixture<KafkaDockerComposeFixture>
{
    public TestCollection(NpgSqlDockerComposeFixture npgSqlDockerComposeFixture, AzureRiteStorageFixture azureRiteStorageFixture, KafkaDockerComposeFixture kafkaDockerComposeFixture )
    {
        azureRiteStorageFixture.StartOnce();
        kafkaDockerComposeFixture.StartOnce();
        npgSqlDockerComposeFixture.StartAndEnsureMigrated();
    }
}