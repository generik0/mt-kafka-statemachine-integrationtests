using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Middelware;

[ExcludeFromCodeCoverage]
public class GlobalLoggingPostProcessor : IGlobalPostProcessor
{
    public Task PostProcessAsync(object req, object? res, HttpContext ctx, IReadOnlyCollection<ValidationFailure> failures, CancellationToken ct)
    {
        var logger = ctx.Resolve<ILogger<GlobalLoggingPostProcessor>>();
        var correlationIdentifier = ctx.TraceIdentifier.Split(":").FirstOrDefault() ?? "Unknown";

        if (failures.Any())
        {
            logger.LogWarning($"Validation Failure - CorrelationIdentifier: {correlationIdentifier}, Request: {req}, Request: {req}, Validation failures: {failures}");
        }
        else if (!ctx.Request.Path.Value?.StartsWith("/user-permissions", StringComparison.InvariantCultureIgnoreCase) ?? false)
        {
            logger.LogInformation($"CorrelationIdentifier: {correlationIdentifier}, Request: {req}, Request: {req}");
        }

        return Task.CompletedTask;
    }
}
