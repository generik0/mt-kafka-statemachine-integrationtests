using Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;
using Microsoft.Extensions.DependencyInjection;

namespace Mass.Transit.Outbox.Repo.Replicate.test;

public class UnitTest1 : IClassFixture<ServiceFixture>
{
    private readonly IServiceProvider _provider;

    public UnitTest1(ServiceFixture serviceFixture)
    {
        _provider = serviceFixture.GetOrCreateServiceProvider();
    }

    [Fact]
    public void Test1()
    {

    }
}