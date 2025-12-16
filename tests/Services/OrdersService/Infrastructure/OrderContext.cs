using Microsoft.EntityFrameworkCore;
using OrdersService.Models;

namespace OrdersService.Infrastructure;

public sealed class OrderContext : DbContext
{
    public DbSet<Order> Orders { get; set; }

    public OrderContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("Orders");
            e.HasKey(p => p.Id);
        });
        
        base.OnModelCreating(modelBuilder);
    }
}