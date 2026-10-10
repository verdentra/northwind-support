using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.HealthChecks;

/// <summary>
/// Healthy when the database answers. Only opens a connection - it does not query any table, so
/// it is cheap enough for a container orchestrator to call every few seconds.
/// </summary>
public sealed class DatabaseHealthCheck(SupportDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("The database is reachable.")
                : HealthCheckResult.Unhealthy("The database is not reachable.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Never echo connection details back to an anonymous caller.
            return HealthCheckResult.Unhealthy("The database is not reachable.", exception);
        }
    }
}
