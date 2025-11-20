using Microsoft.EntityFrameworkCore;

namespace DeliveryService.Infrastructure;

public sealed class DeliveryContext : DbContext
{
    public DeliveryContext(DbContextOptions options) : base(options)
    {
    }
}