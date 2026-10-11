
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data.Configurations;

public sealed class TicketEscalationConfiguration
    : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> builder)
    {
        builder.ToTable("TicketEscalations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FromPriority)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.ToPriority)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.FromAgentId)
            .IsRequired(false);

        builder.Property(e => e.ToAgentId)
            .IsRequired(false);

        builder.Property(e => e.FromDueAtUtc)
            .HasColumnType("datetime2(3)")
            .IsRequired(false);

        builder.Property(e => e.ToDueAtUtc)
            .HasColumnType("datetime2(3)")
            .IsRequired(false);

        builder.Property(e => e.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.EscalatedBy)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.EscalatedAtUtc)
            .HasColumnType("datetime2(3)")
            .IsRequired();

        // Index on TicketId: history is always loaded by ticket
        // (GET /api/tickets/{id}/escalations) and cascade-deleted with the parent.
        builder.HasIndex(e => e.TicketId);

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
