// context file for creating the db
// Data/BirdReportContext.cs
using Microsoft.EntityFrameworkCore;

public class BirdReportContext : DbContext
{
    public DbSet<User> Users => Set<User>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        string dbPath = Environment.GetEnvironmentVariable("SQLITE_DB_PATH")
            ?? "birdreport.db"; // local dev fallback 

        options.UseSqlite($"Data Source={dbPath}");
    }
}