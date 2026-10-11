
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SupportDesk.Infrastructure.Data;

public sealed class SupportDbContextFactory
    : IDesignTimeDbContextFactory<SupportDbContext>
{
    public SupportDbContext CreateDbContext(string[] args)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        string? presentationPath = null;

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "apps",
                "api",
                "src",
                "SupportDesk.Presentation");

            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
            {
                presentationPath = candidate;
                break;
            }

            directory = directory.Parent;
        }

        if (presentationPath is null)
        {
            throw new DirectoryNotFoundException(
                "Could not find the SupportDesk.Presentation folder.");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(presentationPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile(
                "appsettings.Development.json",
                optional: true)
            .Build();

        var connectionString =
            configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' was not found.");

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new SupportDbContext(options);
    }
}
