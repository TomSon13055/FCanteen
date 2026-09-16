using FCanteen.Data.Data;
using FCanteen.Lab02Runner.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using FCanteen.Lab02Runner.Benchmarks;
using FCanteen.Lab02Runner.Reports;
using FCanteen.Lab02Runner.AsyncDemos;
using FCanteen.Lab02Runner.RaceConditions;

namespace FCanteen.Lab02Runner
{
    internal class Program
    {
        private static string _connectionString = string.Empty;

        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection was not found.");

            await using (var db = CreateContext())
            {
                var canConnect =
                    await db.Database.CanConnectAsync();

                if (!canConnect)
                {
                    Console.WriteLine(
                        "Cannot connect to database.");

                    return;
                }
            }

            while (true)
            {
                Console.WriteLine("==============================");
                Console.WriteLine(" FCanteen - Lab 02 Runner");
                Console.WriteLine("==============================");
                Console.WriteLine("1. Seed large data");
                Console.WriteLine("2. YC2 - Sequential foreach");
                Console.WriteLine("3. YC2 - Parallel.ForEach");
                Console.WriteLine("4. YC2 - Parallel.For variants");
                Console.WriteLine("5. YC2 - Full benchmark + save settlement");
                Console.WriteLine("6. YC3 - Top 10 revenue LINQ vs PLINQ");
                Console.WriteLine("7. YC3 - Revenue by hour LINQ vs PLINQ");
                Console.WriteLine("8. YC3 - Top branch by month LINQ vs PLINQ");
                Console.WriteLine("9. YC3 - Items below 1% revenue LINQ vs PLINQ");
                Console.WriteLine("10. YC4 - Async EF Core");
                Console.WriteLine("11. YC5 - Race condition");
                Console.WriteLine("0. Exit");
                Console.WriteLine("==============================");

                Console.Write("Choose: ");

                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await LargeDataSeeder.SeedAsync(_connectionString);
                        break;

                    case "2":
                        await MenuEfficiencyBenchmark.RunSequentialAsync(_connectionString);
                        break;

                    case "3":
                        await MenuEfficiencyBenchmark.RunParallelForEachAsync(_connectionString);
                        break;

                    case "4":
                        await MenuEfficiencyBenchmark
                            .RunParallelForVariantsAsync(_connectionString);
                        break;

                    case "5":
                        await MenuEfficiencyBenchmark.RunAllAndSaveAsync(_connectionString);
                        break;

                    case "6":
                        await PlinqReports.RunTop10RevenueAsync(_connectionString);
                        break;

                    case "7":
                        await PlinqReports.RunRevenueByHourAsync(_connectionString);
                        break;

                    case "8":
                        await PlinqReports.RunTopBranchByMonthAsync(_connectionString);
                        break;

                    case "9":
                        await PlinqReports.RunLowRevenueItemsAsync(_connectionString);
                        break;

                    case "10":
                        await AsyncDailyReport.RunComparisonAsync( _connectionString);
                        break;

                    case "11":
                        IngredientRaceConditionDemo.Run();
                        break;

                    case "0":
                        return;

                    default:
                        Console.WriteLine(
                            "Invalid choice.");
                        break;
                }
            }
        }

        private static FCanteenContext CreateContext()
        {
            var options =
                new DbContextOptionsBuilder<FCanteenContext>()
                    .UseSqlServer(_connectionString)
                    .Options;

            return new FCanteenContext(options);
        }
    }
}