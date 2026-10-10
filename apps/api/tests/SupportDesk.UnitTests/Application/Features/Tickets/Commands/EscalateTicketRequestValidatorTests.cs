using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

/// <remarks>Who escalated is not part of the request: it comes from the access token.</remarks>
public class EscalateTicketRequestValidatorTests
{
    private static readonly EscalateTicketRequestValidator Validator = new();

    [Theory]
    [InlineData(5)]
    [InlineData(500)]
    public void AReasonAtEitherLengthLimit_IsValid(int length) =>
        Assert.True(Validator.Validate(new EscalateTicketRequest(new string('x', length))).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("four")]
    [InlineData("  abc  ")]
    public void ATooShortReason_IsInvalid(string reason)
    {
        var result = Validator.Validate(new EscalateTicketRequest(reason));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EscalateTicketRequest.Reason));
    }

    [Fact]
    public void AReasonOver500Characters_IsInvalid() =>
        Assert.False(Validator.Validate(new EscalateTicketRequest(new string('x', 501))).IsValid);

    [Fact]
    public void AMissingReason_IsInvalid() =>
        Assert.False(Validator.Validate(new EscalateTicketRequest(null!)).IsValid);
}
