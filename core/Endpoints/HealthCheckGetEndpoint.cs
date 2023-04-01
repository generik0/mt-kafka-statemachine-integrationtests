using System.Diagnostics.CodeAnalysis;
using FastEndpoints;

namespace FinOps.BillingSummary.WebApi.HealthCheckGetEndpoint;

[ExcludeFromCodeCoverage]
public class HealthCheckGetEndpoint : EndpointWithoutRequest<HealthCheckGetEndpoint.HealthCheckResponse>
{
    public override void Configure()
    {
        Get("api/health-check");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) => 
        await SendOkAsync(new HealthCheckResponse("Success"), ct);

    public class HealthCheckResponse
    {
        public string Status { get; set; }

        public HealthCheckResponse(string status)
        {
            Status = status;
        }
    }
}
