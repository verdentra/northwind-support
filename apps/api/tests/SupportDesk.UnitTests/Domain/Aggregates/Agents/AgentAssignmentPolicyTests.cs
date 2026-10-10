using SupportDesk.Domain.Aggregates.Agents;
using static SupportDesk.UnitTests.TestDoubles.Workloads;

namespace SupportDesk.UnitTests.Domain.Aggregates.Agents;

public class AgentAssignmentPolicyTests
{
    private const int General = 1;
    private const int Billing = 2;

    private static AssignmentDecision ForGeneral(params AgentWorkload[] agents) =>
        AgentAssignmentPolicy.Choose(agents, General, "General", requiresSpecialist: false);

    private static AssignmentDecision ForBilling(params AgentWorkload[] agents) =>
        AgentAssignmentPolicy.Choose(agents, Billing, "Billing", requiresSpecialist: true);

    [Fact]
    public void Choose_PicksTheAgentWithTheFewestOpenTickets()
    {
        var decision = ForGeneral(Workload(1, open: 5), Workload(2, open: 2), Workload(3, open: 4));

        Assert.Equal(2, decision.AgentId);
        Assert.Equal("Agent 2", decision.AgentName);
        Assert.Contains("fewest open tickets", decision.Reason);
        Assert.False(decision.KeptCurrentAgent);
    }

    [Fact]
    public void Choose_OnATie_PicksTheLowestAgentId()
    {
        var decision = ForGeneral(Workload(7, open: 3), Workload(4, open: 3), Workload(9, open: 3));

        Assert.Equal(4, decision.AgentId);
        Assert.Contains("tie broken by lowest agent id", decision.Reason);
    }

    [Fact]
    public void Choose_IsDeterministicWhateverTheInputOrder()
    {
        var agents = new[] { Workload(3, open: 1), Workload(1, open: 1), Workload(2, open: 1) };

        Assert.Equal(1, ForGeneral(agents).AgentId);
        Assert.Equal(1, ForGeneral(agents.OrderByDescending(a => a.AgentId).ToArray()).AgentId);
    }

    [Fact]
    public void Choose_SkipsInactiveAgents()
    {
        var decision = ForGeneral(Workload(1, open: 0, active: false), Workload(2, open: 6));

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void Choose_SkipsAgentsAtTheirLimit()
    {
        // Strictly below the limit: 6 of 6 is full, 5 of 6 still has room.
        var decision = ForGeneral(Workload(1, open: 6, max: 6), Workload(2, open: 7, max: 8));

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void Choose_OneBelowTheLimit_IsEligible() =>
        Assert.Equal(1, ForGeneral(Workload(1, open: 5, max: 6)).AgentId);

    [Fact]
    public void Choose_WhenTheCategoryRequiresASpecialist_OnlyConsidersSpecialists()
    {
        var decision = ForBilling(
            Workload(1, open: 0, specializations: [General]),
            Workload(2, open: 4, specializations: [General, Billing]));

        Assert.Equal(2, decision.AgentId);
    }

    [Fact]
    public void Choose_WhenNoSpecialistIsRequired_AnyoneMayTakeIt() =>
        Assert.Equal(1, ForGeneral(Workload(1, open: 0, specializations: [Billing])).AgentId);

    [Fact]
    public void Choose_WhenNobodyIsEligible_LeavesItUnassignedWithAReason()
    {
        var decision = ForBilling(
            Workload(1, open: 0, specializations: [General]),
            Workload(2, open: 6, max: 6, specializations: [Billing]),
            Workload(3, open: 0, active: false, specializations: [Billing]));

        Assert.Null(decision.AgentId);
        Assert.Null(decision.AgentName);
        Assert.Contains("unassigned", decision.Reason);
        Assert.Contains("Billing", decision.Reason);
        Assert.Contains("open-ticket limit", decision.Reason);
    }

    [Fact]
    public void Choose_WhenThereIsNoSpecialistAtAll_SaysSo()
    {
        var decision = ForBilling(Workload(1, open: 0, specializations: [General]));

        Assert.Null(decision.AgentId);
        Assert.Contains("no active agent specialises in it", decision.Reason);
    }

    [Fact]
    public void Choose_WhenThereAreNoActiveAgents_SaysSo()
    {
        var decision = ForGeneral(Workload(1, active: false));

        Assert.Null(decision.AgentId);
        Assert.Contains("no active agents", decision.Reason);
    }

    [Fact]
    public void Choose_WithNoAgents_LeavesItUnassigned() =>
        Assert.Null(ForGeneral().AgentId);

    [Fact]
    public void Choose_KeepsAnEligibleCurrentOwnerEvenIfSomeoneHasFewerTickets()
    {
        var decision = AgentAssignmentPolicy.Choose(
            [Workload(1, open: 0), Workload(2, open: 5)], General, "General", requiresSpecialist: false, currentAgentId: 2);

        Assert.Equal(2, decision.AgentId);
        Assert.True(decision.KeptCurrentAgent);
        Assert.Contains("Kept Agent 2", decision.Reason);
    }

    [Fact]
    public void Choose_KeepsACurrentOwnerAtTheirLimit_BecauseThisTicketIsAlreadyCounted()
    {
        // 6 of 6 includes this very ticket: keeping it adds nothing to their load.
        var decision = AgentAssignmentPolicy.Choose(
            [Workload(1, open: 0), Workload(2, open: 6, max: 6)], General, "General", requiresSpecialist: false, currentAgentId: 2);

        Assert.Equal(2, decision.AgentId);
        Assert.True(decision.KeptCurrentAgent);
    }

    [Fact]
    public void Choose_ReassignsAnOwnerOverTheirLimit()
    {
        // 7 of 6 (manually over-assigned): without this ticket still 6 of 6, so no room.
        var decision = AgentAssignmentPolicy.Choose(
            [Workload(1, open: 3), Workload(2, open: 7, max: 6)], General, "General", requiresSpecialist: false, currentAgentId: 2);

        Assert.Equal(1, decision.AgentId);
        Assert.False(decision.KeptCurrentAgent);
        Assert.Contains("Agent 2 is no longer eligible", decision.Reason);
    }

    [Fact]
    public void Choose_ReassignsAnInactiveOwner()
    {
        var decision = AgentAssignmentPolicy.Choose(
            [Workload(1, open: 3), Workload(2, open: 0, active: false)], General, "General", requiresSpecialist: false, currentAgentId: 2);

        Assert.Equal(1, decision.AgentId);
        Assert.Contains("inactive", decision.Reason);
    }

    [Fact]
    public void Choose_ReassignsAnOwnerWhoIsNotASpecialist()
    {
        // The manual assignment endpoint does not check specialisms, so this can happen.
        var decision = AgentAssignmentPolicy.Choose(
            [Workload(1, open: 0, specializations: [General]), Workload(2, open: 4, specializations: [Billing])],
            Billing, "Billing", requiresSpecialist: true, currentAgentId: 1);

        Assert.Equal(2, decision.AgentId);
        Assert.Contains("not a Billing specialist", decision.Reason);
    }

    [Fact]
    public void Choose_WhenTheOwnerIsIneligibleAndNobodyElseIs_LeavesItUnassigned()
    {
        var decision = AgentAssignmentPolicy.Choose(
            [Workload(1, open: 0, active: false)], General, "General", requiresSpecialist: false, currentAgentId: 1);

        Assert.Null(decision.AgentId);
        Assert.Contains("no longer eligible", decision.Reason);
        Assert.Contains("unassigned", decision.Reason);
    }
}
