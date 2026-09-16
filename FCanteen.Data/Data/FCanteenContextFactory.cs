using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FCanteen.Data.Data
{
    public class FCanteenContextFactory
        : IDesignTimeDbContextFactory<FCanteenContext>
    {
        public FCanteenContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' was not found.");

            var optionsBuilder =
                new DbContextOptionsBuilder<FCanteenContext>();

            optionsBuilder.UseSqlServer(
                connectionString,
                sqlOptions =>
                {
                    sqlOptions.CommandTimeout(300);
                });

            return new FCanteenContext(
                optionsBuilder.Options);
        }
    }
}