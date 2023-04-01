using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Mass.Transit.Outbox.Repo.Replicate.test.TestFramework.Logging;

[ExcludeFromCodeCoverage]
public record XUnitLoggerCategoryMinValue(string CategoryName, LogLevel? MinLogLevel);
