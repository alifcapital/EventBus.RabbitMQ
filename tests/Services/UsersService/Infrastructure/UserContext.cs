using Microsoft.EntityFrameworkCore;
using UsersService.Models;

namespace UsersService.Infrastructure;

public sealed class UserContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public UserContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(p => p.Id);
        });
        
        base.OnModelCreating(modelBuilder);
    }
}