using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CakeOrders.Infrastructure.Data;

public sealed class CakeOrdersDbContextFactory : IDesignTimeDbContextFactory<CakeOrdersDbContext>
{
    public CakeOrdersDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CakeOrders")
            ?? "Server=localhost;Database=CakeOrdersDb;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        var options = new DbContextOptionsBuilder<CakeOrdersDbContext>().UseSqlServer(connectionString).Options;
        return new CakeOrdersDbContext(options);
    }
}
