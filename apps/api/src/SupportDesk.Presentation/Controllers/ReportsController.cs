using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Features.Reports.GetSlaSummary;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController : ControllerBase
{
    [HttpGet("sla-summary")]
    public Task<IReadOnlyList<SlaSummaryDto>> GetSlaSummary(
        [FromQuery] SlaSummaryQuery query,
        [FromServices] GetSlaSummaryQueryHandler handler,
        CancellationToken cancellationToken)
    {
        return handler.HandleAsync(
            query,
            cancellationToken);
    }
}
