using CakeOrders.Domain.Entities;
using CakeOrders.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CakeOrders.Infrastructure.Data;

public sealed class CakeOrdersDbContext(DbContextOptions<CakeOrdersDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<Referrer> Referrers => Set<Referrer>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductWorkshopRouting> ProductWorkshopRoutings => Set<ProductWorkshopRouting>();
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<PosTerminal> PosTerminals => Set<PosTerminal>();
    public DbSet<OrderChangeLog> OrderChangeLogs => Set<OrderChangeLog>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<OrderCashPayment> OrderCashPayments => Set<OrderCashPayment>();
    public DbSet<RahkaranIntegrationSettings> RahkaranIntegrationSettings => Set<RahkaranIntegrationSettings>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ProductionTask> ProductionTasks => Set<ProductionTask>();
    public DbSet<ProductionTaskItem> ProductionTaskItems => Set<ProductionTaskItem>();
    public DbSet<ProductionTaskHistory> ProductionTaskHistory => Set<ProductionTaskHistory>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<RahkaranIntegrationSettings>(entity =>
        {
            entity.ToTable("RahkaranIntegrationSettings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OrderNumber).IsUnique();
            entity.Property(x => x.OrderNumber).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);
            entity.Property(x => x.UpdatedByUserId).HasMaxLength(450);
            entity.HasOne(x => x.ProductionBranch)
                .WithMany(x => x.ProductionOrders)
                .HasForeignKey(x => x.ProductionBranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.CustomerName).HasMaxLength(200);
            entity.Property(x => x.CustomerPhone).HasMaxLength(30);
            entity.Property(x => x.OrderType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.RequestedWeightKg).HasPrecision(8, 2);
            entity.Property(x => x.PricePerKg).HasPrecision(18, 2);
            entity.Property(x => x.CakeText).HasMaxLength(500);
            entity.Property(x => x.SampleImagePath).HasMaxLength(500);
            entity.Property(x => x.PrintImagePath).HasMaxLength(500);
            entity.Property(x => x.FinalAmount).HasPrecision(18, 2);
            entity.Property(x => x.RequestedDepositAmount).HasPrecision(18, 2);
            entity.Property(x => x.RequestedDepositAmount).HasPrecision(18, 2);
            entity.Property(x => x.DepositAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaidAmount).HasPrecision(18, 2);
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
            entity.Property(x => x.ChangedByUserId).HasMaxLength(450);
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
        modelBuilder.Entity<Workshop>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.BranchId, x.Name }).IsUnique();
            entity.HasOne(x => x.Branch)
                .WithMany(x => x.Workshops)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.CategoryId, x.Name }).IsUnique();
            entity.HasOne(x => x.Category)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ProductWorkshopRouting>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProductId, x.BranchId }).IsUnique();
            entity.HasOne(x => x.Product)
                .WithMany(x => x.WorkshopRoutings)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Branch)
                .WithMany(x => x.ProductWorkshopRoutings)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Workshop)
                .WithMany(x => x.ProductWorkshopRoutings)
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("AspNetUsers", table =>
                table.HasCheckConstraint(
                    "CK_AspNetUsers_WorkshopAssignment",
                    "([WorkshopId] IS NULL AND [WorkshopRole] IS NULL) OR ([WorkshopId] IS NOT NULL AND [WorkshopRole] IS NOT NULL)"));

            entity.HasIndex(x => x.WorkshopId);

            // Each workshop can have at most one manager; employees can be many.
            entity.HasIndex(x => x.WorkshopId)
                .IsUnique()
                .HasDatabaseName("IX_AspNetUsers_WorkshopManager")
                .HasFilter("[WorkshopId] IS NOT NULL AND [WorkshopRole] = 1");

            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
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
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorUserId).HasMaxLength(450);
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(150).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(100);
            entity.Property(x => x.DetailsJson).HasMaxLength(4000);
            entity.Property(x => x.IpAddress).HasMaxLength(45);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.ActorUserId, x.CreatedAtUtc });
        });
        modelBuilder.Entity<OrderChangeLog>(entity =>
        {
            entity.ToTable("OrderChangeLogs");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.FieldName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.OldValue)
                .HasMaxLength(2000);

            entity.Property(x => x.NewValue)
                .HasMaxLength(2000);

            entity.Property(x => x.ChangedByUserId)
                .HasMaxLength(450);

            entity.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.OrderId, x.ChangedAtUtc });
        });

        modelBuilder.Entity<OrderCashPayment>(entity =>
        {
            entity.ToTable("OrderCashPayments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.ReceivedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.ReceiptReference).HasMaxLength(100);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => new { x.OrderId, x.ReceivedAtUtc });
            entity.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("PaymentTransactions");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Amount)
                .HasPrecision(18, 2);

            entity.Property(x => x.IdempotencyKey)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.MerchantReference)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Rrn)
                .HasMaxLength(100);

            entity.Property(x => x.TraceNumber)
                .HasMaxLength(100);

            entity.Property(x => x.ResponseCode)
                .HasMaxLength(50);

            entity.Property(x => x.ResponseMessage)
                .HasMaxLength(1000);

            entity.Property(x => x.BridgeInstallationId)
                .HasMaxLength(100);

            entity.Property(x => x.CreatedByUserId)
                .HasMaxLength(450);

            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique();

            entity.HasIndex(x => x.MerchantReference)
                .IsUnique();

            entity.HasIndex(x => new
            {
                x.OrderId,
                x.CreatedAtUtc
            });

            entity.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CashRegister)
                .WithMany()
                .HasForeignKey(x => x.CashRegisterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PosTerminal)
                .WithMany()
                .HasForeignKey(x => x.PosTerminalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductionTask>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);
            entity.Property(x => x.UpdatedByUserId).HasMaxLength(450);
            entity.HasIndex(x => new { x.OrderId, x.WorkshopId }).IsUnique();
            entity.HasIndex(x => new { x.BranchId, x.WorkshopId, x.Status });

            entity.HasOne(x => x.Order)
                .WithMany(x => x.ProductionTasks)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Branch)
                .WithMany(x => x.ProductionTasks)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Workshop)
                .WithMany(x => x.ProductionTasks)
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductionTaskItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.Notes).HasMaxLength(1000);

            entity.HasOne(x => x.ProductionTask)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.ProductionTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.OrderItem)
                .WithMany()
                .HasForeignKey(x => x.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductionTaskHistory>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ChangedByUserId).HasMaxLength(450);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasIndex(x => new { x.ProductionTaskId, x.ChangedAtUtc });

            entity.HasOne(x => x.ProductionTask)
                .WithMany(x => x.History)
                .HasForeignKey(x => x.ProductionTaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProductName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.Quantity)
                .HasPrecision(18, 3);

            entity.Property(x => x.UnitPrice)
                .HasPrecision(18, 2);

            entity.Property(x => x.LineTotal)
                .HasPrecision(18, 2);

            entity.HasOne(x => x.Order)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Workshop)
                .WithMany()
                .HasForeignKey(x => x.WorkshopId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RecipientUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Link).HasMaxLength(500);

            entity.HasIndex(x => new { x.RecipientUserId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.RecipientUserId, x.ReadAtUtc });

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

