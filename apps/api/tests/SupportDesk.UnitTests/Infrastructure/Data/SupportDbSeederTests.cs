using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Data;

public sealed class SupportDbSeederTests
{
    private const string DevelopmentPassword = "DevOnly-Password1!";

    [Fact]
    public async Task SeedAsync_OnPopulatedDatabase_HashesOnlyAgentsMissingAPasswordHash()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var customer = new Customer(
            "Test Customer",
            "customer@example.com",
            null,
            CustomerTier.Standard,
            FixedClock.DefaultNow);
        var category = new Category("General", false, false);
        var missingHashAgent = new Agent(
            "Alex Turner",
            "alex.turner@northwind-support.example",
            10,
            FixedClock.DefaultNow);
        var existingHashAgent = new Agent(
            "Ben Osei",
            "ben.osei@northwind-support.example",
            8,
            FixedClock.DefaultNow);
        existingHashAgent.SetPasswordHash("existing-encoded-password-hash");

        db.AddRange(customer, category, missingHashAgent, existingHashAgent);
        await db.SaveChangesAsync();

        db.Add(Ticket.Raise(
            "TCK-TEST1",
            "Test title",
            "Test description for seeded database coverage.",
            customer.Id,
            category.Id,
            TicketPriority.Medium,
            FixedClock.DefaultNow));
        await db.SaveChangesAsync();

        var hasher = new PasswordHasher<Agent>();
        var seeder = new SupportDbSeeder(
            db,
            new FixedClock(),
            hasher,
            NullLogger<SupportDbSeeder>.Instance);

        await seeder.SeedAsync();

        db.ChangeTracker.Clear();
        var seededAgent = await db.Set<Agent>().SingleAsync(agent => agent.Id == missingHashAgent.Id);
        var existingAgent = await db.Set<Agent>().SingleAsync(agent => agent.Id == existingHashAgent.Id);

        Assert.NotNull(seededAgent.PasswordHash);
        Assert.NotEqual(DevelopmentPassword, seededAgent.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(seededAgent, seededAgent.PasswordHash!, DevelopmentPassword));
        Assert.Equal("existing-encoded-password-hash", existingAgent.PasswordHash);
    }
}
