using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.HealthChecks;

namespace SupportDesk.UnitTests.Infrastructure.HealthChecks;

/// <summary>Uses SQLite, so a real connection is opened (or fails to open) without SQL Server.</summary>
public class DatabaseHealthCheckTests
{
    private static async Task<HealthStatus> CheckAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connectionString).Options;
        await using var db = new SupportDbContext(options);

        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(new HealthCheckContext());

        return result.Status;
    }

    [Fact]
    public async Task AReachableDatabase_IsHealthy() =>
        Assert.Equal(HealthStatus.Healthy, await CheckAsync("Data Source=:memory:"));

    [Fact]
    public async Task AnUnreachableDatabase_IsUnhealthy() =>
        Assert.Equal(
            HealthStatus.Unhealthy,
            await CheckAsync($"Data Source={Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.db")};Mode=ReadOnly"));
}
