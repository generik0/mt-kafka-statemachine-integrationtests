using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Mass.Transit.Outbox.Repo.Replicate.test.TestFramework.Logging
{
    [ExcludeFromCodeCoverage]
    internal class XUnitLogger : ILogger
    {
        private readonly LogLevel? _minLogLevel;
        private readonly string _categoryName;
        private readonly LoggerExternalScopeProvider _scopeProvider;
        private readonly ITestOutputHelper _testOutputHelper;

        public XUnitLogger(ITestOutputHelper testOutputHelper, LoggerExternalScopeProvider scopeProvider,
            string categoryName)
        {
            _testOutputHelper = testOutputHelper;
            _scopeProvider = scopeProvider;
            _categoryName = categoryName;
        }
        
        public XUnitLogger(ITestOutputHelper testOutputHelper, LoggerExternalScopeProvider scopeProvider,
            string categoryName, LogLevel? logLevel) : this(testOutputHelper, scopeProvider, categoryName)
        {
            _minLogLevel = logLevel;
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= (_minLogLevel ?? LogLevel.Debug);

        public IDisposable BeginScope<TState>(TState state)
        {
            return _scopeProvider.Push(state);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var sb = new StringBuilder();
            sb.Append(GetLogLevelString(logLevel))
                .Append(" [").Append(_categoryName).Append("] ")
                .Append(formatter(state, exception));

            if (exception != null)
            {
                sb.Append('\n').Append(exception);
            }

            // Append scopes
            _scopeProvider.ForEachScope((scope, state) =>
            {
                state.Append("\n => ");
                state.Append(scope);
            }, sb);

            try
            {
                _testOutputHelper.WriteLine(sb.ToString());
            }
            catch (InvalidOperationException)
            {
                // ignore "There is no currently active test." exception - that is thrown when WebApp fixture is disposed and logs are still written
            }
        }

        public static ILogger CreateLogger(ITestOutputHelper testOutputHelper)
        {
            return new XUnitLogger(testOutputHelper, new LoggerExternalScopeProvider(), "");
        }

        public static ILogger<T> CreateLogger<T>(ITestOutputHelper testOutputHelper)
        {
            return new XUnitLogger<T>(testOutputHelper, new LoggerExternalScopeProvider());
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "\nTrace:\t",
                LogLevel.Debug => "\nDebug:\t",
                LogLevel.Information => "\nInfo:\t",
                LogLevel.Warning => "\nWarn:\t",
                LogLevel.Error => "\nFail:\t",
                LogLevel.Critical => "\nCritical:\t",
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
            };
        }
    }
}
