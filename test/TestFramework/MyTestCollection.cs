using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;

[CollectionDefinition(nameof(MyTestCollection))]
public class MyTestCollection : ICollectionFixture<NpgSqlDockerComposeFixture>, 
    ICollectionFixture<AzureRiteStorageFixture>, 
    ICollectionFixture<KafkaDockerComposeFixture>
{
}