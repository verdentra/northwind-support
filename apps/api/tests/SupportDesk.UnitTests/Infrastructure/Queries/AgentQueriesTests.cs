
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class AgentQueriesTests
{
    private static async Task<(SqliteConnection Connection, SupportDbContext Db)>
        CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        return (connection, db);
    }

    private static async Task<(Customer Customer, Category Category)>
        SeedBasicsAsync(SupportDbContext db, bool requiresSpecialist = false)
    {
        var customer = new Customer(
            "Test Customer",
            "customer@example.com",
            null,
            CustomerTier.Standard,
            FixedClock.DefaultNow);

        var category = new Category(
            "Technical",
            requiresSpecialist,
            false);

        db.Set<Customer>().Add(customer);
        db.Set<Category>().Add(category);

        await db.SaveChangesAsync();

        return (customer, category);
    }

    private static Agent CreateAgent(string name, int capacity = 5) =>
        new Agent(
            name,
            $"{name.Replace(" ", "").ToLowerInvariant()}@example.com",
            capacity,
            FixedClock.DefaultNow);

    private static async Task AddOpenTicketsAsync(
        SupportDbContext db,
        Customer customer,
        Category category,
        Agent agent,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            var ticket = Ticket.Raise(
                $"TCK-{Guid.NewGuid():N}",
                "Test ticket",
                "Test description",
                customer.Id,
                category.Id,
                TicketPriority.Medium,
                FixedClock.DefaultNow);

            ticket.AssignTo(agent.Id, FixedClock.DefaultNow);
            db.Set<Ticket>().Add(ticket);
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task FindBestAvailableAsync_SelectsFewestOpenTickets()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var cleanup = connection;
        await using var context = db;

        var (customer, category) = await SeedBasicsAsync(db);

        var busy = CreateAgent("Busy Agent");
        var available = CreateAgent("Available Agent");

        db.Set<Agent>().AddRange(busy, available);
        await db.SaveChangesAsync();

        await AddOpenTicketsAsync(db, customer, category, busy, 3);
        await AddOpenTicketsAsync(db, customer, category, available, 1);

        var result = await new AgentQueries(db).FindBestAvailableAsync(
            category.Id, false, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(available.Id, result.Id);
        Assert.Equal(1, result.OpenTicketCount);
    }

    [Fact]
    public async Task FindBestAvailableAsync_RequiresSpecializationWhenFlagged()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var cleanup = connection;
        await using var context = db;

        var (_, category) = await SeedBasicsAsync(db, requiresSpecialist: true);

        var specialist = CreateAgent("Specialist");
        var generalist = CreateAgent("Generalist");

        db.Set<Agent>().AddRange(specialist, generalist);
        await db.SaveChangesAsync();

        specialist.AddSpecialization(category.Id);
        await db.SaveChangesAsync();

        var result = await new AgentQueries(db).FindBestAvailableAsync(
            category.Id, true, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(specialist.Id, result.Id);
    }

    [Fact]
    public async Task FindBestAvailableAsync_ExcludesAgentsAtCapacity()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var cleanup = connection;
        await using var context = db;

        var (customer, category) = await SeedBasicsAsync(db);

        var full = CreateAgent("Full Agent", capacity: 1);
        var available = CreateAgent("Available Agent", capacity: 3);

        db.Set<Agent>().AddRange(full, available);
        await db.SaveChangesAsync();

        await AddOpenTicketsAsync(db, customer, category, full, 1);

        var result = await new AgentQueries(db).FindBestAvailableAsync(
            category.Id, false, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(available.Id, result.Id);
    }

    [Fact]
    public async Task FindBestAvailableAsync_ExcludesInactiveAgents()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var cleanup = connection;
        await using var context = db;

        var (_, category) = await SeedBasicsAsync(db);

        var inactive = CreateAgent("Inactive Agent");
        inactive.Deactivate();

        var active = CreateAgent("Active Agent");

        db.Set<Agent>().AddRange(inactive, active);
        await db.SaveChangesAsync();

        var result = await new AgentQueries(db).FindBestAvailableAsync(
            category.Id, false, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(active.Id, result.Id);
    }

    [Fact]
    public async Task FindBestAvailableAsync_BreaksTiesByLowestId()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var cleanup = connection;
        await using var context = db;

        var (_, category) = await SeedBasicsAsync(db);

        var first = CreateAgent("First Agent");
        var second = CreateAgent("Second Agent");

        db.Set<Agent>().AddRange(first, second);
        await db.SaveChangesAsync();

        var result = await new AgentQueries(db).FindBestAvailableAsync(
            category.Id, false, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(Math.Min(first.Id, second.Id), result.Id);
    }

    [Fact]
    public async Task FindBestAvailableAsync_WhenNobodyEligible_ReturnsNull()
    {
        var (connection, db) = await CreateDatabaseAsync();
        await using var cleanup = connection;
        await using var context = db;

        var (_, category) = await SeedBasicsAsync(db, requiresSpecialist: true);

        var generalist = CreateAgent("Generalist");
        db.Set<Agent>().Add(generalist);
        await db.SaveChangesAsync();

        var result = await new AgentQueries(db).FindBestAvailableAsync(
            category.Id, true, CancellationToken.None);

        Assert.Null(result);
    }
}
