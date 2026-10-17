using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Features.Reports.GetSlaSummary;

namespace SupportDesk.UnitTests.Application.Features.Reports;

public sealed class SlaSummaryQueryValidatorTests
{
    private readonly SlaSummaryQueryValidator validator = new();

    [Theory]
    [InlineData("agent")]
    [InlineData("priority")]
    [InlineData("AGENT")]
    [InlineData("Priority")]
    public void Accepts_valid_group_by_values(string groupBy)
    {
        var query = new SlaSummaryQuery
        {
            FromUtc = null,
            ToUtc = null,
            GroupBy = groupBy
        };

        var result = validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Rejects_an_invalid_group_by_value()
    {
        var query = new SlaSummaryQuery
        {
            FromUtc = null,
            ToUtc = null,
            GroupBy = "customer"
        };

        var result = validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(SlaSummaryQuery.GroupBy));
    }

    [Fact]
    public void Rejects_when_from_is_not_before_to()
    {
        var from = new DateTime(
            2026,
            10,
            15,
            8,
            0,
            0,
            DateTimeKind.Utc);

        var to = from;

        var query = new SlaSummaryQuery
        {
            FromUtc = from,
            ToUtc = to,
            GroupBy = "priority"
        };

        var result = validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(SlaSummaryQuery.ToUtc));
    }

    [Fact]
    public void Accepts_a_valid_half_open_date_range()
    {
        var from = new DateTime(
            2026,
            10,
            15,
            8,
            0,
            0,
            DateTimeKind.Utc);

        var to = from.AddDays(1);

        var query = new SlaSummaryQuery
        {
            FromUtc = from,
            ToUtc = to,
            GroupBy = "priority"
        };

        var result = validator.Validate(query);

        Assert.True(result.IsValid);
    }
}
