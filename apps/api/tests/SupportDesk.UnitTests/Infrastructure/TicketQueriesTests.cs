
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;
using Xunit;

namespace SupportDesk.UnitTests.Infrastructure;

public sealed class TicketQueriesTests
{
    [Fact]
    public async Task GetPagedAsync_FiltersByExactPriority()
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Db;

        var customer = new Customer(
            "Contoso Ltd", "contoso@example.com", null,
            CustomerTier.Premium, FixedClock.DefaultNow);

        var category = new Category("General", false, false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        db.AddRange(
            Ticket.Raise("TCK-0041", "High ticket", "Description",
                customer.Id, category.Id, TicketPriority.High, FixedClock.DefaultNow),
            Ticket.Raise("TCK-0042", "Low ticket", "Description",
                customer.Id, category.Id, TicketPriority.Low, FixedClock.DefaultNow));

        await db.SaveChangesAsync();

        var queries = CreateTicketQueries(db);

        var result = await queries.GetPagedAsync(
            new TicketQuery { Priority = TicketPriority.High },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.All(result.Items, item =>
            Assert.Equal(TicketPriority.High, item.Priority));
    }

    [Fact]
    public async Task GetPagedAsync_CombinesPriorityAndCategoryFilters()
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Db;

        var customer = new Customer(
            "Contoso Ltd", "contoso@example.com", null,
            CustomerTier.Premium, FixedClock.DefaultNow);

        var general = new Category("General", false, false);
        var billing = new Category("Billing", false, false);

        db.AddRange(customer, general, billing);
        await db.SaveChangesAsync();

        db.AddRange(
            Ticket.Raise("TCK-0041", "Matching ticket", "Description",
                customer.Id, general.Id, TicketPriority.High, FixedClock.DefaultNow),
            Ticket.Raise("TCK-0042", "Wrong category", "Description",
                customer.Id, billing.Id, TicketPriority.High, FixedClock.DefaultNow),
            Ticket.Raise("TCK-0043", "Wrong priority", "Description",
                customer.Id, general.Id, TicketPriority.Low, FixedClock.DefaultNow));

        await db.SaveChangesAsync();

        var queries = CreateTicketQueries(db);

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.High,
                CategoryId = general.Id
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("TCK-0041", result.Items[0].Reference);
    }

    [Fact]
    public async Task GetPagedAsync_SearchesByCustomerName()
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Db;

        var contoso = new Customer(
            "Contoso Ltd", "contoso@example.com", null,
            CustomerTier.Premium, FixedClock.DefaultNow);

        var fabrikam = new Customer(
            "Fabrikam Ltd", "fabrikam@example.com", null,
            CustomerTier.Premium, FixedClock.DefaultNow);

        var category = new Category("General", false, false);

        db.AddRange(contoso, fabrikam, category);
        await db.SaveChangesAsync();

        db.AddRange(
            Ticket.Raise("TCK-0041", "First ticket", "Description",
                contoso.Id, category.Id, TicketPriority.High, FixedClock.DefaultNow),
            Ticket.Raise("TCK-0042", "Second ticket", "Description",
                fabrikam.Id, category.Id, TicketPriority.High, FixedClock.DefaultNow));

        await db.SaveChangesAsync();

        var queries = CreateTicketQueries(db);

        var result = await queries.GetPagedAsync(
            new TicketQuery { Search = "  Contoso  " },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("TCK-0041", result.Items[0].Reference);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersBySlaStatusBreached()
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Db;
        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Contoso Ltd", "contoso@example.com", null,
            CustomerTier.Premium, now);
        var category = new Category("General", false, false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        var breached = Ticket.Raise(
            "TCK-0041", "Past due", "Description",
            customer.Id, category.Id, TicketPriority.High, now);
        breached.SetSlaWindow(now.AddHours(-10), now.AddHours(-1));

        var within = Ticket.Raise(
            "TCK-0042", "Still fine", "Description",
            customer.Id, category.Id, TicketPriority.High, now);
        within.SetSlaWindow(now.AddHours(-1), now.AddHours(10));

        var noSla = Ticket.Raise(
            "TCK-0043", "No due date", "Description",
            customer.Id, category.Id, TicketPriority.Low, now);

        db.AddRange(breached, within, noSla);
        await db.SaveChangesAsync();

        var queries = CreateTicketQueries(db);

        var result = await queries.GetPagedAsync(
            new TicketQuery { SlaStatus = SlaStatus.Breached },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("TCK-0041", result.Items[0].Reference);
        Assert.Equal(SlaStatus.Breached, result.Items[0].SlaStatus);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersBySlaStatusAtRisk()
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Db;
        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Contoso Ltd", "contoso@example.com", null,
            CustomerTier.Premium, now);
        var category = new Category("General", false, false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        // 8h window, 1h remaining → 12.5% left → AtRisk at 25% threshold
        var atRisk = Ticket.Raise(
            "TCK-0041", "Almost due", "Description",
            customer.Id, category.Id, TicketPriority.High, now);
        atRisk.SetSlaWindow(now.AddHours(-7), now.AddHours(1));

        // 8h window, 4h remaining → 50% left → WithinSla
        var within = Ticket.Raise(
            "TCK-0042", "Comfortable", "Description",
            customer.Id, category.Id, TicketPriority.High, now);
        within.SetSlaWindow(now.AddHours(-4), now.AddHours(4));

        db.AddRange(atRisk, within);
        await db.SaveChangesAsync();

        var queries = CreateTicketQueries(db);

        var result = await queries.GetPagedAsync(
            new TicketQuery { SlaStatus = SlaStatus.AtRisk },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("TCK-0041", result.Items[0].Reference);
        Assert.Equal(SlaStatus.AtRisk, result.Items[0].SlaStatus);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersBySlaStatusWithinSla()
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Db;
        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Contoso Ltd", "contoso@example.com", null,
            CustomerTier.Premium, now);
        var category = new Category("General", false, false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        var within = Ticket.Raise(
            "TCK-0041", "Comfortable", "Description",
            customer.Id, category.Id, TicketPriority.High, now);
        within.SetSlaWindow(now.AddHours(-1), now.AddHours(3));

        var atRisk = Ticket.Raise(
            "TCK-0042", "Almost due", "Description",
            customer.Id, category.Id, TicketPriority.High, now);
        atRisk.SetSlaWindow(now.AddHours(-7), now.AddHours(1));

        db.AddRange(within, atRisk);
        await db.SaveChangesAsync();

        var queries = CreateTicketQueries(db);

        var result = await queries.GetPagedAsync(
            new TicketQuery { SlaStatus = SlaStatus.WithinSla },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("TCK-0041", result.Items[0].Reference);
        Assert.Equal(SlaStatus.WithinSla, result.Items[0].SlaStatus);
    }

    [Fact]
    public async Task GetPagedAsync_CombinesSlaStatusWithPriorityFilter()
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Db;
        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Contoso Ltd", "contoso@example.com", null,
            CustomerTier.Premium, now);
        var category = new Category("General", false, false);

        db.AddRange(customer, category);
        await db.SaveChangesAsync();

        var matching = Ticket.Raise(
            "TCK-0041", "High breached", "Description",
            customer.Id, category.Id, TicketPriority.High, now);
        matching.SetSlaWindow(now.AddHours(-10), now.AddHours(-1));

        var wrongPriority = Ticket.Raise(
            "TCK-0042", "Low breached", "Description",
            customer.Id, category.Id, TicketPriority.Low, now);
        wrongPriority.SetSlaWindow(now.AddHours(-10), now.AddHours(-1));

        db.AddRange(matching, wrongPriority);
        await db.SaveChangesAsync();

        var queries = CreateTicketQueries(db);

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.High,
                SlaStatus = SlaStatus.Breached
            },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("TCK-0041", result.Items[0].Reference);
    }

    // Uses the same at-risk threshold configured by the application.
    private static TicketQueries CreateTicketQueries(SupportDbContext db)
    {
        var slaOptions = Options.Create(new SlaOptions
        {
            AtRiskThresholdPercent = 25
        });

        return new TicketQueries(db, new FixedClock(), slaOptions);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDatabase(
            SqliteConnection connection,
            SupportDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public SupportDbContext Db { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<SupportDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new SupportDbContext(options);
            await db.Database.EnsureCreatedAsync();

            return new TestDatabase(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
