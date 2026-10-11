using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.UnitTests.Application.Contracts.Tickets;

public class TicketQueryTests
{
    [Fact]
    public void Normalized_ClampsPageSoOffsetCannotOverflow()
    {
        var query = new TicketQuery { Page = int.MaxValue, PageSize = 100 }.Normalized();

        Assert.Equal(int.MaxValue / 100 + 1, query.Page);
        Assert.Equal(2_147_483_600, (query.Page - 1) * query.PageSize);
    }
}
