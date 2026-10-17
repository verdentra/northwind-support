using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Features.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class SlaSummarySqlTests
{
    private static readonly DateTime NowUtc =
        new(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("priority")]
    [InlineData("agent")]
    public void BuildSlaSummaryQuery_GeneratesServerSideGroupedSql(string groupBy)
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer("Server=unused;Database=unused")
            .Options;

        using var db = new SupportDbContext(options);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(NowUtc);
        var queries = new TicketQueries(db, clock.Object, new SlaOptions());

        var sql = queries.BuildSlaSummaryQuery(new SlaSummaryQuery
        {
            FromUtc = NowUtc.AddDays(-7),
            ToUtc = NowUtc,
            GroupBy = groupBy
        }).ToQueryString();

        var normalizedSql = Regex.Replace(sql, @"\s+", " ").ToUpperInvariant();

        Assert.Matches(@"CREATEDATUTC\]\s*>=", normalizedSql);
        Assert.Matches(@"CREATEDATUTC\]\s*<", normalizedSql);
        Assert.Contains("GROUP BY", normalizedSql);
        if (string.Equals(groupBy, "priority", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Contains("PRIORITY", normalizedSql);
        }
        else
        {
            Assert.Contains("AGENTID", normalizedSql);
            Assert.Contains("FULLNAME", normalizedSql);
            Assert.Contains("UNASSIGNED", normalizedSql);
        }

        Assert.Contains("SUM(CASE WHEN", normalizedSql);
        Assert.Contains("CASE", normalizedSql);
        Assert.Contains("AVG(", normalizedSql);
        Assert.Contains("DATEDIFF(", normalizedSql);
        Assert.Matches(
            @"CASE WHEN .+?= 0 THEN .+? ELSE .+?/ .+? END|CASE WHEN .+?= 0 THEN .+? ELSE .+?/.+? END",
            normalizedSql);
    }
}
