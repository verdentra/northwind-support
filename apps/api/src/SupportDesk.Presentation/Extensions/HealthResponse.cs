using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SupportDesk.Presentation.Extensions;

/// <summary>
/// Writes the health report as a small JSON document, e.g.
/// <c>{ "status": "Healthy", "checks": { "database": "Healthy" } }</c>. No exception text or
/// connection detail is included, because the endpoint is anonymous.
/// </summary>
public static class HealthResponse
{
    public static Task WriteAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())
        });
}
