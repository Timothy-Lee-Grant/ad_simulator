using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace BidEngine.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        
        // Use a dummy connection string - it just needs to see the configuration
        var dataSourceBuilder = new NpgsqlDataSourceBuilder("Host=localhost;Database=dummy");
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        optionsBuilder.UseNpgsql(dataSource);

        return new AppDbContext(optionsBuilder.Options);
    }
}