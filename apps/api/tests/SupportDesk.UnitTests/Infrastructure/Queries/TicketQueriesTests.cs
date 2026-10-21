using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;
using SupportDesk.Domain.Aggregates.Agents;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Configuration;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests
{
    /// <summary>
    /// Creates ticket queries with the SLA configuration used by tests.
    /// </summary>
    private static TicketQueries CreateQueries(SupportDbContext db)
    {
        var options = Options.Create(new SlaOptions
        {
            AtRiskThresholdPercent = 25
        });

        return new TicketQueries(db, new FixedClock(), options);
    }

    
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

    private static async Task SeedAsync(SupportDbContext db)
    {
        var customer1 = new Customer(
            "Acme Corporation",
            "acme@example.com",
            null,
            CustomerTier.Standard,
            FixedClock.DefaultNow);

        var customer2 = new Customer(
            "Northwind Traders",
            "northwind@example.com",
            null,
            CustomerTier.Standard,
            FixedClock.DefaultNow);

        var category = new Category("Technical", false, false);

        db.Set<Customer>().AddRange(customer1, customer2);
        db.Set<Category>().Add(category);

        await db.SaveChangesAsync();

        db.Set<Ticket>().AddRange(
            Ticket.Raise(
                "TCK-001", "Login problem", "Cannot log in",
                customer1.Id, category.Id,
                TicketPriority.High, FixedClock.DefaultNow),

            Ticket.Raise(
                "TCK-002", "Payment issue", "Payment failed",
                customer2.Id, category.Id,
                TicketPriority.Low, FixedClock.DefaultNow),

            Ticket.Raise(
                "TCK-003", "Account issue", "Account locked",
                customer1.Id, category.Id,
                TicketPriority.High, FixedClock.DefaultNow)
        );

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByPriority()
    {
        var (connection, db) = await CreateDatabaseAsync();

        await using var cleanup = connection;
        await using var context = db;

        await SeedAsync(db);

        var queries = CreateQueries(db);
        
        var result = await queries.GetPagedAsync(
            new TicketQuery { Priority = TicketPriority.High },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items,
            ticket => Assert.Equal(TicketPriority.High, ticket.Priority));
    }

    [Fact]
    public async Task GetPagedAsync_CombinesFiltersWithAnd()
    {
        var (connection, db) = await CreateDatabaseAsync();

        await using var cleanup = connection;
        await using var context = db;

        await SeedAsync(db);

        var customer = await db.Set<Customer>()
            .SingleAsync(x => x.Name == "Acme Corporation");

        var queries = CreateQueries(db);

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.High,
                CustomerId = customer.Id,
                Search = "Login"
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("TCK-001", result.Items[0].Reference);
    }

    [Fact]
    public async Task GetPagedAsync_SearchesCustomerName()
    {
        var (connection, db) = await CreateDatabaseAsync();

        await using var cleanup = connection;
        await using var context = db;

        await SeedAsync(db);

        var queries = CreateQueries(db);

        var result = await queries.GetPagedAsync(
            new TicketQuery { Search = " Northwind " },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("TCK-002", result.Items[0].Reference);
    }

    [Fact]
    public async Task GetPagedAsync_CountsFilteredTicketsBeforePaging()
    {
        var (connection, db) = await CreateDatabaseAsync();

        await using var cleanup = connection;
        await using var context = db;

        await SeedAsync(db);

        var queries = CreateQueries(db);
        
        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.High,
                Page = 1,
                PageSize = 1
            },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }
    [Fact]
    public async Task GetPagedAsync_FiltersByStatus()
    {
        var (connection, db) = await CreateDatabaseAsync();

        await using var cleanup = connection;
        await using var context = db;

        await SeedAsync(db);

        var ticket = await db.Set<Ticket>()
            .SingleAsync(x => x.Reference == "TCK-001");

        ticket.ChangeStatus(TicketStatus.Open, FixedClock.DefaultNow);
        await db.SaveChangesAsync();

        var queries = CreateQueries(db);

        var result = await queries.GetPagedAsync(
            new TicketQuery { Status = TicketStatus.Open },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("TCK-001", result.Items[0].Reference);
    }
    [Fact]
    public async Task GetPagedAsync_FiltersByCategory()
    {
        var (connection, db) = await CreateDatabaseAsync();

        await using var cleanup = connection;
        await using var context = db;

        await SeedAsync(db);

        var originalCategory = await db.Set<Category>()
            .SingleAsync(x => x.Name == "Technical");

        var otherCategory = new Category("Billing", false, false);
        db.Set<Category>().Add(otherCategory);
        await db.SaveChangesAsync();

        db.Set<Ticket>().Add(
            Ticket.Raise(
                "TCK-004",
                "Invoice problem",
                "Incorrect invoice amount",
                customerId: 1,
                categoryId: otherCategory.Id,
                TicketPriority.Medium,
                FixedClock.DefaultNow));

        await db.SaveChangesAsync();

        var queries = CreateQueries(db);

        var result = await queries.GetPagedAsync(
            new TicketQuery { CategoryId = otherCategory.Id },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("TCK-004", result.Items[0].Reference);
    }
    [Fact]
    public async Task GetPagedAsync_FiltersUnassignedTickets()
    {
        var (connection, db) = await CreateDatabaseAsync();

        await using var cleanup = connection;
        await using var context = db;

        await SeedAsync(db);

        var agent = new Agent(
            "Test Agent",
            "agent@example.com",
            10,
            FixedClock.DefaultNow);

        db.Set<Agent>().Add(agent);
        await db.SaveChangesAsync();

        var assignedTicket = await db.Set<Ticket>()
            .SingleAsync(x => x.Reference == "TCK-001");

        assignedTicket.AssignTo(agent.Id, FixedClock.DefaultNow);
        await db.SaveChangesAsync();

        var queries = CreateQueries(db);

        var result = await queries.GetPagedAsync(
            new TicketQuery { UnassignedOnly = true },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        Assert.DoesNotContain(
            result.Items,
            ticket => ticket.Reference == "TCK-001");
    }
    
    [Fact]
    public async Task GetPagedAsync_FiltersByAssignedAgent()
    {
        var (connection, db) = await CreateDatabaseAsync();

        await using var cleanup = connection;
        await using var context = db;

        await SeedAsync(db);

        var agent1 = new Agent(
            "Agent One",
            "agent1@example.com",
            10,
            FixedClock.DefaultNow);

        var agent2 = new Agent(
            "Agent Two",
            "agent2@example.com",
            10,
            FixedClock.DefaultNow);

        db.Set<Agent>().AddRange(agent1, agent2);
        await db.SaveChangesAsync();

        var ticket1 = await db.Set<Ticket>()
            .SingleAsync(x => x.Reference == "TCK-001");

        var ticket2 = await db.Set<Ticket>()
            .SingleAsync(x => x.Reference == "TCK-002");

        ticket1.AssignTo(agent1.Id, FixedClock.DefaultNow);
        ticket2.AssignTo(agent2.Id, FixedClock.DefaultNow);

        await db.SaveChangesAsync();

        var queries = CreateQueries(db);

        var result = await queries.GetPagedAsync(
            new TicketQuery { AssignedAgentId = agent1.Id },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("TCK-001", result.Items[0].Reference);
    }

}
