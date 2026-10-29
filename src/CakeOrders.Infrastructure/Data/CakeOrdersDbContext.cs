using CakeOrders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Infrastructure.Data;

public sealed class CakeOrdersDbContext(DbContextOptions<CakeOrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<Referrer> Referrers => Set<Referrer>();
    public DbSet<PosTerminal> PosTerminals => Set<PosTerminal>();

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
            entity.Property(x => x.CakeText).HasMaxLength(500);
            entity.Property(x => x.SampleImagePath).HasMaxLength(500);
            entity.Property(x => x.PrintImagePath).HasMaxLength(500);
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
        modelBuilder.Entity<Referrer>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });
        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasIndex(x => new { x.OrderId, x.ChangedAtUtc });
        });
        modelBuilder.Entity<PosTerminal>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(45).IsRequired();
            entity.Property(x => x.ConnectionType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.TerminalCode).HasMaxLength(100);
            entity.HasIndex(x => x.Name);
        });
    }
}