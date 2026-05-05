using Microsoft.EntityFrameworkCore;
using JsbaiBackend.Models;

namespace JsbaiBackend.Data;

/// <summary>
/// This is the DATABASE CONTEXT — the bridge between our C# code and the SQLite database.
/// 
/// Think of it like this:
/// - The database is a filing cabinet
/// - AppDbContext is the person who manages the filing cabinet
/// - Submissions is one drawer in the cabinet (one table)
/// 
/// Entity Framework Core (EF Core) automatically:
/// - Creates the database file if it doesn't exist
/// - Creates the Submissions table based on our Submission model
/// - Converts C# objects to database rows and back
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // This represents the "Submissions" table in the database
    // DbSet<Submission> means "a set of Submission rows"
    public DbSet<Submission> Submissions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tell EF Core that RefId must be unique — no two submissions can have the same ref ID
        modelBuilder.Entity<Submission>()
            .HasIndex(s => s.RefId)
            .IsUnique();

        // Set default value for Status column
        modelBuilder.Entity<Submission>()
            .Property(s => s.Status)
            .HasDefaultValue("Under Editorial Review");
    }
}
