// context file for creating the db
// Data/BirdReportContext.cs
using Microsoft.EntityFrameworkCore;

public class BirdReportContext : DbContext
{
    public DbSet<User> Users => Set<User>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite("Data Source=birdreport.db"); // db file 
    }
}