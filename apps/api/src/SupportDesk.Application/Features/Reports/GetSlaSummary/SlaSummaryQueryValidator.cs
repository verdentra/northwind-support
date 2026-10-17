using FluentValidation;
using SupportDesk.Application.Contracts.Reports;

namespace SupportDesk.Application.Features.Reports.GetSlaSummary;

public sealed class SlaSummaryQueryValidator : AbstractValidator<SlaSummaryQuery>
{
    public SlaSummaryQueryValidator()
    {
        RuleFor(query => query.GroupBy)
            .NotEmpty()
            .Must(groupBy =>
                string.Equals(groupBy, "agent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(groupBy, "priority", StringComparison.OrdinalIgnoreCase))
            .WithMessage("GroupBy must be either 'agent' or 'priority'.");

        RuleFor(query => query.ToUtc)
            .Must((query, toUtc) =>
                !query.FromUtc.HasValue ||
                !toUtc.HasValue ||
                query.FromUtc.Value < toUtc.Value)
            .WithMessage("ToUtc must be later than FromUtc.");
    }
}
