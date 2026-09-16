using System.Diagnostics;
using FCanteen.Data.Data;
using Microsoft.EntityFrameworkCore;

namespace FCanteen.Lab02Runner.Reports
{
    public static class PlinqReports
    {
        public static async Task RunTop10RevenueAsync(
            string connectionString)
        {
            var options =
                new DbContextOptionsBuilder<FCanteenContext>()
                    .UseSqlServer(
                        connectionString,
                        sqlOptions =>
                        {
                            sqlOptions.CommandTimeout(300);
                        })
                    .Options;

            await using var db =
                new FCanteenContext(options);

            Console.WriteLine();
            Console.WriteLine(
                "Loading data for PLINQ report...");

            var lines =
                await db.TicketLines
                    .AsNoTracking()
                    .Select(x => new SaleLineData
                    {
                        MenuItemId = x.MenuItemId,
                        MenuItemName = x.MenuItem.Name,
                        Quantity = x.Quantity,
                        UnitPrice = x.UnitPrice,
                        Hour = x.OrderTicket.CreatedAt.Hour
                    })
                    .ToListAsync();

            Console.WriteLine(
                $"Loaded {lines.Count:N0} TicketLines.");

            // ========================================
            // LINQ tuần tự
            // ========================================

            var stopwatch =
                Stopwatch.StartNew();

            var sequentialResult =
                lines
                    .GroupBy(x => new
                    {
                        x.MenuItemId,
                        x.MenuItemName
                    })
                    .Select(g => new RevenueResult
                    {
                        MenuItemId =
                            g.Key.MenuItemId,

                        Name =
                            g.Key.MenuItemName,

                        Revenue =
                            g.Sum(
                                x =>
                                    x.UnitPrice
                                    * x.Quantity)
                    })
                    .OrderByDescending(
                        x => x.Revenue)
                    .Take(10)
                    .ToList();

            stopwatch.Stop();

            long sequentialMs =
                stopwatch.ElapsedMilliseconds;

            // ========================================
            // PLINQ
            // ========================================

            stopwatch.Restart();

            var parallelResult =
                lines
                    .AsParallel()
                    .GroupBy(x => new
                    {
                        x.MenuItemId,
                        x.MenuItemName
                    })
                    .Select(g => new RevenueResult
                    {
                        MenuItemId =
                            g.Key.MenuItemId,

                        Name =
                            g.Key.MenuItemName,

                        Revenue =
                            g.Sum(
                                x =>
                                    x.UnitPrice
                                    * x.Quantity)
                    })
                    .OrderByDescending(
                        x => x.Revenue)
                    .Take(10)
                    .ToList();

            stopwatch.Stop();

            long plinqMs =
                stopwatch.ElapsedMilliseconds;

            // ========================================
            // In kết quả
            // ========================================

            Console.WriteLine();
            Console.WriteLine(
                "===== YC3 - TOP 10 BY REVENUE =====");

            Console.WriteLine(
                $"{"Rank",-6} {"Menu",-20} {"Revenue",15}");

            Console.WriteLine(
                "---------------------------------------------");

            int rank = 1;

            foreach (var item in parallelResult)
            {
                Console.WriteLine(
                    $"{rank,-6} " +
                    $"{item.Name,-20} " +
                    $"{item.Revenue,15:N0}");

                rank++;
            }

            Console.WriteLine();
            Console.WriteLine(
                $"Sequential LINQ : {sequentialMs:N0} ms");

            Console.WriteLine(
                $"PLINQ           : {plinqMs:N0} ms");

            double speedup =
                sequentialMs * 1.0
                / Math.Max(1, plinqMs);

            Console.WriteLine(
                $"Speedup         : {speedup:F2}x");
        }

        private sealed class SaleLineData
        {
            public int MenuItemId { get; set; }

            public string MenuItemName { get; set; }
                = string.Empty;

            public int Quantity { get; set; }

            public decimal UnitPrice { get; set; }

            public int Hour { get; set; }
        }

        private sealed class RevenueResult
        {
            public int MenuItemId { get; set; }

            public string Name { get; set; }
                = string.Empty;

            public decimal Revenue { get; set; }
        }

        public static async Task RunRevenueByHourAsync(
    string connectionString)
        {
            var options =
                new DbContextOptionsBuilder<FCanteenContext>()
                    .UseSqlServer(
                        connectionString,
                        sqlOptions =>
                        {
                            sqlOptions.CommandTimeout(300);
                        })
                    .Options;

            await using var db =
                new FCanteenContext(options);

            Console.WriteLine();
            Console.WriteLine(
                "Loading data for revenue-by-hour report...");

            var lines =
                await db.TicketLines
                    .AsNoTracking()
                    .Select(x => new SaleLineData
                    {
                        MenuItemId = x.MenuItemId,
                        MenuItemName = x.MenuItem.Name,
                        Quantity = x.Quantity,
                        UnitPrice = x.UnitPrice,
                        Hour = x.OrderTicket.CreatedAt.Hour
                    })
                    .ToListAsync();

            Console.WriteLine(
                $"Loaded {lines.Count:N0} TicketLines.");

            var hours =
                Enumerable.Range(0, 24)
                    .ToList();

            // =========================================
            // LINQ tuần tự
            // =========================================

            var stopwatch =
                Stopwatch.StartNew();

            var sequentialResult =
                hours
                    .Select(hour => new HourRevenueResult
                    {
                        Hour = hour,

                        Revenue =
                            lines
                                .Where(x => x.Hour == hour)
                                .Sum(
                                    x =>
                                        x.UnitPrice
                                        * x.Quantity)
                    })
                    .ToList();

            stopwatch.Stop();

            long sequentialMs =
                stopwatch.ElapsedMilliseconds;

            // =========================================
            // PLINQ + AsOrdered
            // =========================================

            stopwatch.Restart();

            var parallelResult =
                hours
                    .AsParallel()
                    .AsOrdered()
                    .Select(hour => new HourRevenueResult
                    {
                        Hour = hour,

                        Revenue =
                            lines
                                .Where(x => x.Hour == hour)
                                .Sum(
                                    x =>
                                        x.UnitPrice
                                        * x.Quantity)
                    })
                    .ToList();

            stopwatch.Stop();

            long plinqMs =
                stopwatch.ElapsedMilliseconds;

            // =========================================
            // In kết quả
            // =========================================

            Console.WriteLine();
            Console.WriteLine(
                "===== YC3 - REVENUE BY HOUR =====");

            Console.WriteLine(
                $"{"Hour",-10} {"Revenue",18}");

            Console.WriteLine(
                "-------------------------------");

            foreach (var item in parallelResult)
            {
                Console.WriteLine(
                    $"{item.Hour:00}:00      " +
                    $"{item.Revenue,18:N0}");
            }

            Console.WriteLine();

            Console.WriteLine(
                $"Sequential LINQ : {sequentialMs:N0} ms");

            Console.WriteLine(
                $"PLINQ AsOrdered : {plinqMs:N0} ms");

            double speedup =
                sequentialMs * 1.0
                / Math.Max(1, plinqMs);

            Console.WriteLine(
                $"Speedup         : {speedup:F2}x");
        }

        private sealed class HourRevenueResult
        {
            public int Hour { get; set; }

            public decimal Revenue { get; set; }
        }

        public static async Task RunTopBranchByMonthAsync(
    string connectionString)
        {
            var options =
                new DbContextOptionsBuilder<FCanteenContext>()
                    .UseSqlServer(
                        connectionString,
                        sqlOptions =>
                        {
                            sqlOptions.CommandTimeout(300);
                        })
                    .Options;

            await using var db =
                new FCanteenContext(options);

            Console.WriteLine();
            Console.WriteLine(
                "Loading data for top-branch-by-month report...");

            var lines =
                await db.TicketLines
                    .AsNoTracking()
                    .Select(x => new MonthlySaleLineData
                    {
                        BranchCode =
                            x.OrderTicket.BranchCode,

                        CreatedAt =
                            x.OrderTicket.CreatedAt,

                        Quantity =
                            x.Quantity,

                        UnitPrice =
                            x.UnitPrice
                    })
                    .ToListAsync();

            Console.WriteLine(
                $"Loaded {lines.Count:N0} TicketLines.");

            // =========================================
            // LINQ tuần tự
            // =========================================

            var stopwatch =
                Stopwatch.StartNew();

            var sequentialResult =
                lines
                    .GroupBy(x => new
                    {
                        Year = x.CreatedAt.Year,
                        Month = x.CreatedAt.Month,
                        x.BranchCode
                    })
                    .Select(g => new BranchMonthRevenue
                    {
                        Year =
                            g.Key.Year,

                        Month =
                            g.Key.Month,

                        BranchCode =
                            g.Key.BranchCode,

                        Revenue =
                            g.Sum(
                                x =>
                                    x.UnitPrice
                                    * x.Quantity)
                    })
                    .GroupBy(x => new
                    {
                        x.Year,
                        x.Month
                    })
                    .Select(g =>
                        g.OrderByDescending(
                                x => x.Revenue)
                            .First())
                    .OrderBy(x => x.Year)
                    .ThenBy(x => x.Month)
                    .ToList();

            stopwatch.Stop();

            long sequentialMs =
                stopwatch.ElapsedMilliseconds;

            // =========================================
            // PLINQ
            // =========================================

            stopwatch.Restart();

            var parallelResult =
                lines
                    .AsParallel()
                    .GroupBy(x => new
                    {
                        Year = x.CreatedAt.Year,
                        Month = x.CreatedAt.Month,
                        x.BranchCode
                    })
                    .Select(g => new BranchMonthRevenue
                    {
                        Year =
                            g.Key.Year,

                        Month =
                            g.Key.Month,

                        BranchCode =
                            g.Key.BranchCode,

                        Revenue =
                            g.Sum(
                                x =>
                                    x.UnitPrice
                                    * x.Quantity)
                    })
                    .GroupBy(x => new
                    {
                        x.Year,
                        x.Month
                    })
                    .Select(g =>
                        g.OrderByDescending(
                                x => x.Revenue)
                            .First())
                    .OrderBy(x => x.Year)
                    .ThenBy(x => x.Month)
                    .ToList();

            stopwatch.Stop();

            long plinqMs =
                stopwatch.ElapsedMilliseconds;

            // =========================================
            // In kết quả
            // =========================================

            Console.WriteLine();
            Console.WriteLine(
                "===== YC3 - TOP BRANCH BY MONTH =====");

            Console.WriteLine(
                $"{"Month",-12} {"Branch",-10} {"Revenue",18}");

            Console.WriteLine(
                "--------------------------------------------");

            foreach (var item in parallelResult)
            {
                Console.WriteLine(
                    $"{item.Month:00}/{item.Year,-7} " +
                    $"{item.BranchCode,-10} " +
                    $"{item.Revenue,18:N0}");
            }

            Console.WriteLine();

            Console.WriteLine(
                $"Sequential LINQ : {sequentialMs:N0} ms");

            Console.WriteLine(
                $"PLINQ           : {plinqMs:N0} ms");

            double speedup =
                sequentialMs * 1.0
                / Math.Max(1, plinqMs);

            Console.WriteLine(
                $"Speedup         : {speedup:F2}x");
        }

        private sealed class MonthlySaleLineData
        {
            public string BranchCode { get; set; }
                = string.Empty;

            public DateTime CreatedAt { get; set; }

            public int Quantity { get; set; }

            public decimal UnitPrice { get; set; }
        }

        private sealed class BranchMonthRevenue
        {
            public int Year { get; set; }

            public int Month { get; set; }

            public string BranchCode { get; set; }
                = string.Empty;

            public decimal Revenue { get; set; }
        }

        public static async Task RunLowRevenueItemsAsync(
    string connectionString)
        {
            var options =
                new DbContextOptionsBuilder<FCanteenContext>()
                    .UseSqlServer(
                        connectionString,
                        sqlOptions =>
                        {
                            sqlOptions.CommandTimeout(300);
                        })
                    .Options;

            await using var db =
                new FCanteenContext(options);

            Console.WriteLine();
            Console.WriteLine(
                "Loading data for low-revenue-items report...");

            var lines =
                await db.TicketLines
                    .AsNoTracking()
                    .Select(x => new SaleLineData
                    {
                        MenuItemId = x.MenuItemId,
                        MenuItemName = x.MenuItem.Name,
                        Quantity = x.Quantity,
                        UnitPrice = x.UnitPrice,
                        Hour = x.OrderTicket.CreatedAt.Hour
                    })
                    .ToListAsync();

            Console.WriteLine(
                $"Loaded {lines.Count:N0} TicketLines.");

            decimal totalRevenue =
                lines.Sum(
                    x => x.UnitPrice * x.Quantity);

            decimal threshold =
                totalRevenue * 0.01m;

            // =========================================
            // LINQ tuần tự
            // =========================================

            var stopwatch =
                Stopwatch.StartNew();

            var sequentialResult =
                lines
                    .GroupBy(x => new
                    {
                        x.MenuItemId,
                        x.MenuItemName
                    })
                    .Select(g => new RevenueResult
                    {
                        MenuItemId =
                            g.Key.MenuItemId,

                        Name =
                            g.Key.MenuItemName,

                        Revenue =
                            g.Sum(
                                x =>
                                    x.UnitPrice
                                    * x.Quantity)
                    })
                    .Where(x => x.Revenue < threshold)
                    .OrderBy(x => x.Revenue)
                    .ToList();

            stopwatch.Stop();

            long sequentialMs =
                stopwatch.ElapsedMilliseconds;

            // =========================================
            // PLINQ
            // =========================================

            stopwatch.Restart();

            var parallelResult =
                lines
                    .AsParallel()
                    .GroupBy(x => new
                    {
                        x.MenuItemId,
                        x.MenuItemName
                    })
                    .Select(g => new RevenueResult
                    {
                        MenuItemId =
                            g.Key.MenuItemId,

                        Name =
                            g.Key.MenuItemName,

                        Revenue =
                            g.Sum(
                                x =>
                                    x.UnitPrice
                                    * x.Quantity)
                    })
                    .Where(x => x.Revenue < threshold)
                    .OrderBy(x => x.Revenue)
                    .ToList();

            stopwatch.Stop();

            long plinqMs =
                stopwatch.ElapsedMilliseconds;

            // =========================================
            // In kết quả
            // =========================================

            Console.WriteLine();
            Console.WriteLine(
                "===== YC3 - ITEMS BELOW 1% TOTAL REVENUE =====");

            Console.WriteLine(
                $"Total revenue : {totalRevenue:N0}");

            Console.WriteLine(
                $"1% threshold : {threshold:N0}");

            Console.WriteLine();

            Console.WriteLine(
                $"{"Menu",-22} {"Revenue",15} {"Share",10} {"Suggestion",-15}");

            Console.WriteLine(
                "----------------------------------------------------------------");

            foreach (var item in parallelResult)
            {
                decimal share =
                    totalRevenue == 0
                        ? 0
                        : item.Revenue
                          / totalRevenue
                          * 100;

                Console.WriteLine(
                    $"{item.Name,-22} " +
                    $"{item.Revenue,15:N0} " +
                    $"{share,9:F2}% " +
                    $"{"Consider remove",-15}");
            }

            if (parallelResult.Count == 0)
            {
                Console.WriteLine(
                    "No menu item is below 1% of total revenue.");
            }

            Console.WriteLine();

            Console.WriteLine(
                $"Sequential LINQ : {sequentialMs:N0} ms");

            Console.WriteLine(
                $"PLINQ           : {plinqMs:N0} ms");

            double speedup =
                sequentialMs * 1.0
                / Math.Max(1, plinqMs);

            Console.WriteLine(
                $"Speedup         : {speedup:F2}x");
        }
    }
}