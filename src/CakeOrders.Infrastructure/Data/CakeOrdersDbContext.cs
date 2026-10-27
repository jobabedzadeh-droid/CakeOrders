using CakeOrders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Infrastructure.Data;

public sealed class CakeOrdersDbContext(DbContextOptions<CakeOrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OrderNumber).IsUnique();
            entity.Property(x => x.OrderNumber).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CustomerName).HasMaxLength(200);
            entity.Property(x => x.CustomerPhone).HasMaxLength(30);
            entity.Property(x => x.OrderType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.RequestedWeightKg).HasPrecision(8, 2);
            entity.Property(x => x.PricePerKg).HasPrecision(18, 2);
            entity.Property(x => x.SecondaryPricePerKg).HasPrecision(18, 2);
            entity.Property(x => x.FinalAmount).HasPrecision(18, 2);
            entity.Property(x => x.DepositAmount).HasPrecision(18, 2);
            entity.Property(x => x.MacaronCost).HasPrecision(18, 2);
            entity.Property(x => x.FlowerCost).HasPrecision(18, 2);
            entity.Property(x => x.DummyCost).HasPrecision(18, 2);
            entity.Property(x => x.PrintingCost).HasPrecision(18, 2);
            entity.Property(x => x.ShippingCost).HasPrecision(18, 2);
            entity.Property(x => x.OtherCost).HasPrecision(18, 2);
            entity.Property(x => x.Discount).HasPrecision(18, 2);
            entity.HasMany(x => x.StatusHistory).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasIndex(x => new { x.OrderId, x.ChangedAtUtc });
        });
    }
}