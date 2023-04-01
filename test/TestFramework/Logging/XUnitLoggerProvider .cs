#nullable enable

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Mass.Transit.Outbox.Repo.Replicate.test.TestFramework.Logging
{
    public sealed class XUnitLoggerProvider : ILoggerProvider
    {
        private readonly LoggerExternalScopeProvider _scopeProvider = new();
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly XUnitLoggerCategoryMinValue[]? _minLogValues;

        public XUnitLoggerProvider(ITestOutputHelper testOutputHelper, params XUnitLoggerCategoryMinValue[]? minValues)
        {
            _testOutputHelper = testOutputHelper;
            _minLogValues = minValues;
        }

        public ILogger CreateLogger(string categoryName) => 
            new XUnitLogger(_testOutputHelper, _scopeProvider, categoryName, 
                    _minLogValues?.FirstOrDefault(x => x.CategoryName.StartsWith(categoryName))?.MinLogLevel);

        public void Dispose()
        {
            // Method intentionally left empty.
        }
    }
}
