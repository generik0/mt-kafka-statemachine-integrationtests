using Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;


namespace Mass.Transit.Outbox.Repo.Replicate.test;

[Collection(nameof(TestCollection))]
public class UnitTest1 : IClassFixture<ServiceFixture>
{
    private readonly IServiceProvider _provider;

    public UnitTest1(ServiceFixture serviceFixture)
    {

    }

    [Fact]
    public void Test1()
    {
        ;
    }
}