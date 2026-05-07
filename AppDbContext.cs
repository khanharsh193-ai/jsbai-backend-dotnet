using Microsoft.EntityFrameworkCore;
using JsbaiBackend.Models;
namespace JsbaiBackend.Data;
public class AppDbContext : DbContext {
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Submission> Submissions { get; set; }
    protected override void OnModelCreating(ModelBuilder m) {
        m.Entity<Submission>().HasIndex(s => s.RefId).IsUnique();
        m.Entity<Submission>().Property(s => s.Status).HasDefaultValue("Under Editorial Review");
    }
}
