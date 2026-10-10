using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// The SLA filter must run in SQL Server, not in memory. These tests translate each predicate
/// with the real SQL Server provider - no database is opened - so an untranslatable expression
/// fails here instead of at run time. Agreement with the evaluator at the boundaries is covered
/// by <see cref="Domain.Aggregates.Tickets.SlaEvaluatorTests"/>, which counts seconds exactly
/// the way the generated DATEDIFF does.
/// </summary>
public class SlaStatusFilterTests
{
    private static string Sql(SlaStatus status)
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer("Server=unused;Database=unused")
            .Options;

        using var db = new SupportDbContext(options);

        return db.Set<Ticket>()
            .Where(SlaStatusFilter.Matching(status, FixedClock.DefaultNow, TestSla.Policy.AtRiskThresholdPercent))
            .ToQueryString();
    }

    [Theory]
    [InlineData(SlaStatus.NotApplicable)]
    [InlineData(SlaStatus.WithinSla)]
    [InlineData(SlaStatus.AtRisk)]
    [InlineData(SlaStatus.Breached)]
    [InlineData(SlaStatus.Met)]
    public void EveryStatus_TranslatesToAWhereClause(SlaStatus status) =>
        Assert.Contains("WHERE", Sql(status));

    [Theory]
    [InlineData(SlaStatus.AtRisk)]
    [InlineData(SlaStatus.WithinSla)]
    public void TheThreshold_IsComputedByTheDatabase(SlaStatus status)
    {
        var sql = Sql(status);

        Assert.Contains("DATEDIFF(second", sql);
        Assert.Contains("COALESCE([t].[SlaStartedAtUtc], [t].[CreatedAtUtc])", sql);
    }

    [Fact]
    public void NotApplicable_OnlyChecksForAMissingDueDate() =>
        Assert.Contains("[t].[DueAtUtc] IS NULL", Sql(SlaStatus.NotApplicable));
}
