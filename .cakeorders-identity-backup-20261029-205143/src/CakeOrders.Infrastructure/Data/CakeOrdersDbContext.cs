using CakeOrders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Infrastructure.Data;

public sealed class CakeOrdersDbContext(DbContextOptions<CakeOrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<Referrer> Referrers => Set<Referrer>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
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
        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.HasIndex(x => x.Name).IsUnique();
        });
        modelBuilder.Entity<CashRegister>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.BridgeInstallationId).HasMaxLength(100);
            entity.Property(x => x.BridgeSecretHash).HasMaxLength(128);
            entity.HasIndex(x => new { x.BranchId, x.Name }).IsUnique();
            entity.HasIndex(x => x.BridgeInstallationId).IsUnique().HasFilter("[BridgeInstallationId] IS NOT NULL");
            entity.HasIndex(x => x.PosTerminalId).IsUnique().HasFilter("[PosTerminalId] IS NOT NULL");
            entity.HasOne(x => x.Branch).WithMany(x => x.CashRegisters).HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PosTerminal).WithMany().HasForeignKey(x => x.PosTerminalId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
