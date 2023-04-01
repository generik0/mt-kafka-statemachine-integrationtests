using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Mass.Transit.Outbox.Repo.Replicate.test.TestFramework.Logging
{
    [ExcludeFromCodeCoverage]
    internal sealed class XUnitLogger<T> : XUnitLogger, ILogger<T>
    {
        public XUnitLogger(ITestOutputHelper testOutputHelper, LoggerExternalScopeProvider scopeProvider)
            : base(testOutputHelper, scopeProvider, typeof(T).FullName)
        {
        }
    }
}
