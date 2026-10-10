using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data.Configurations;

/// <summary>
/// The escalation history. Part of the ticket aggregate (the relationship to the ticket is
/// configured in <see cref="TicketConfiguration"/>); the agents are referenced by id.
/// </summary>
public sealed class TicketEscalationConfiguration : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> builder)
    {
        builder.ToTable("TicketEscalations", t =>
            t.HasCheckConstraint(
                "CK_TicketEscalations_Priority",
                "[FromPriority] BETWEEN 1 AND 4 AND [ToPriority] BETWEEN 1 AND 4"));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FromPriority).HasConversion<int>();

        builder.Property(e => e.ToPriority).HasConversion<int>();

        builder.Property(e => e.FromDueAtUtc).HasColumnType("datetime2(3)");

        builder.Property(e => e.ToDueAtUtc).HasColumnType("datetime2(3)");

        builder.Property(e => e.Reason).HasMaxLength(TicketEscalation.ReasonMaxLength).IsRequired();

        builder.Property(e => e.EscalatedBy).HasMaxLength(TicketEscalation.EscalatedByMaxLength).IsRequired();

        builder.Property(e => e.EscalatedAtUtc).HasColumnType("datetime2(3)");

        // An agent with history is kept (agents are deactivated, not deleted), so the history
        // always points at a real row.
        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.FromAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.ToAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // The only read is "this ticket's history, newest first": the index seeks on TicketId and
        // returns rows already in that order, so there is no sort. It also serves the foreign key,
        // so EF does not add a separate TicketId index.
        builder.HasIndex(e => new { e.TicketId, e.EscalatedAtUtc })
            .HasDatabaseName("IX_TicketEscalations_TicketId_EscalatedAtUtc")
            .IsDescending(false, true);

        // The agent foreign keys get EF's conventional single-column indexes, which keep a
        // (rare) agent delete check from scanning the whole history table.
    }
}
