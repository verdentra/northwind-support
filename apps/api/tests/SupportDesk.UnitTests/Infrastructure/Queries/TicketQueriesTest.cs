
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests
{
    private static async Task<(
        SqliteConnection Connection,
        SupportDbContext Db,
        TicketQueries Queries)> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow)
            .Returns(new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc));

        return (connection, db, new TicketQueries(db, clock.Object, new SlaOptions()));
    }

    private static async Task<(Customer Customer, Category Category)> AddCustomerAndCategoryAsync(
        SupportDbContext db,
        string customerName = "Test Customer",
        string categoryName = "General")
    {
        var customer = new Customer(
            customerName,
            $"{Guid.NewGuid():N}@example.com",
            null,
            CustomerTier.Standard,
            DateTime.UtcNow);

        var category = new Category(categoryName, false, false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        return (customer, category);
    }

    private static Ticket CreateTicket(
        string reference,
        string title,
        Customer customer,
        Category category,
        TicketPriority priority)
    {
        return Ticket.Raise(
            reference,
            title,
            "A description for testing ticket filtering.",
            customer.Id,
            category.Id,
            priority,
            new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetPagedAsync_HighPriority_ReturnsOnlyHighTickets()
    {
        var database = await CreateDatabaseAsync();
        await using var connection = database.Connection;
        await using var db = database.Db;

        var (customer, category) = await AddCustomerAndCategoryAsync(db);

        db.AddRange(
            CreateTicket("TCK-1001", "High ticket", customer, category, TicketPriority.High),
            CreateTicket("TCK-1002", "Critical ticket", customer, category, TicketPriority.Critical),
            CreateTicket("TCK-1003", "Another high ticket", customer, category, TicketPriority.High));

        await db.SaveChangesAsync();

        var result = await database.Queries.GetPagedAsync(
            new TicketQuery { Priority = TicketPriority.High },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item =>
            Assert.Equal(TicketPriority.High, item.Priority));
    }

    [Fact]
    public async Task GetPagedAsync_CombinedFilters_AppliesBothFilters()
    {
        var database = await CreateDatabaseAsync();
        await using var connection = database.Connection;
        await using var db = database.Db;

        var (customer, category) = await AddCustomerAndCategoryAsync(db);
        var otherCategory = new Category("Hardware", false, false);
        db.Add(otherCategory);
        await db.SaveChangesAsync();

        db.AddRange(
            CreateTicket("TCK-2001", "Matching ticket", customer, category, TicketPriority.High),
            CreateTicket("TCK-2002", "Wrong priority", customer, category, TicketPriority.Low),
            CreateTicket("TCK-2003", "Wrong category", customer, otherCategory, TicketPriority.High));

        await db.SaveChangesAsync();

        var result = await database.Queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.High,
                CategoryId = category.Id
            },
            CancellationToken.None);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-2001", ticket.Reference);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_SearchByCustomerName_FindsCustomerTickets()
    {
        var database = await CreateDatabaseAsync();
        await using var connection = database.Connection;
        await using var db = database.Db;

        var (customer, category) = await AddCustomerAndCategoryAsync(
            db,
            customerName: "Northwind Trading");

        var (otherCustomer, _) = await AddCustomerAndCategoryAsync(
            db,
            customerName: "Contoso Services",
            categoryName: "Accounts");

        db.AddRange(
            CreateTicket("TCK-3001", "Login problem", customer, category, TicketPriority.Medium),
            CreateTicket("TCK-3002", "Payment problem", otherCustomer, category, TicketPriority.Medium));

        await db.SaveChangesAsync();

        var result = await database.Queries.GetPagedAsync(
            new TicketQuery { Search = "Northwind Trading" },
            CancellationToken.None);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-3001", ticket.Reference);
        Assert.Equal(1, result.TotalCount);
    }
}
