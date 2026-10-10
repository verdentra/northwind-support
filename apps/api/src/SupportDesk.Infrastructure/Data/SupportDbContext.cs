using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Infrastructure.Data;

/// <summary>
/// EF Core context for the support desk, and the unit of work that commits each request's
/// changes. Mapping lives in the per-aggregate configuration classes in
/// <c>Data/Configurations</c>.
/// </summary>
public class SupportDbContext(DbContextOptions<SupportDbContext> options) : DbContext(options), IUnitOfWork
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectChangesToHistory();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RejectChangesToHistory();

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every timestamp in this database is UTC; say so, so that it survives the round trip.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    /// <summary>Escalation history is append-only: it can be added, never changed or removed.</summary>
    private void RejectChangesToHistory()
    {
        var changed = ChangeTracker.Entries<TicketEscalation>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (changed)
        {
            throw new InvalidOperationException("Ticket escalation history is immutable and cannot be updated or deleted.");
        }
    }
}
