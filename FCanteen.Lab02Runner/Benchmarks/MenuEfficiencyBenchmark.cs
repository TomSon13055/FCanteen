using System.Collections.Concurrent;
using System.Diagnostics;
using FCanteen.Data.Data;
using FCanteen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FCanteen.Lab02Runner.Benchmarks
{
    public static class MenuEfficiencyBenchmark
    {
        private static readonly string[] BranchCodes =
        {
            "DN",
            "HCM",
            "HN"
        };

        // =========================================================
        // Chạy riêng: foreach tuần tự
        // =========================================================
        public static async Task<long> RunSequentialAsync(
            string connectionString)
        {
            var data = await LoadDataAsync(connectionString);

            Console.WriteLine();
            Console.WriteLine(
                "===== YC2 - SEQUENTIAL FOREACH =====");

            var stopwatch = Stopwatch.StartNew();

            var results =
                CalculateSequential(
                    data.MenuItems,
                    data.Lines);

            stopwatch.Stop();

            PrintResults(results);

            Console.WriteLine();
            Console.WriteLine(
                $"Sequential foreach: " +
                $"{stopwatch.ElapsedMilliseconds:N0} ms");

            return stopwatch.ElapsedMilliseconds;
        }

        // =========================================================
        // Chạy riêng: Parallel.ForEach
        // =========================================================
        public static async Task<long> RunParallelForEachAsync(
            string connectionString)
        {
            var data = await LoadDataAsync(connectionString);

            Console.WriteLine();
            Console.WriteLine(
                "===== YC2 - PARALLEL.FOREACH =====");

            var stopwatch = Stopwatch.StartNew();

            var results =
                CalculateParallelForEach(
                    data.MenuItems,
                    data.Lines);

            stopwatch.Stop();

            PrintResults(results);

            Console.WriteLine();
            Console.WriteLine(
                $"Parallel.ForEach: " +
                $"{stopwatch.ElapsedMilliseconds:N0} ms");

            return stopwatch.ElapsedMilliseconds;
        }

        // =========================================================
        // Chạy riêng: Parallel.For với 2, 4, logical cores
        // =========================================================
        public static async Task RunParallelForVariantsAsync(
            string connectionString)
        {
            var data = await LoadDataAsync(connectionString);

            int logicalCores =
                Environment.ProcessorCount;

            Console.WriteLine();
            Console.WriteLine(
                $"Logical processors: {logicalCores}");

            RunParallelForAndPrint(
                data.MenuItems,
                data.Lines,
                2);

            RunParallelForAndPrint(
                data.MenuItems,
                data.Lines,
                4);

            RunParallelForAndPrint(
                data.MenuItems,
                data.Lines,
                logicalCores);
        }

        // =========================================================
        // YC2 hoàn chỉnh:
        // chạy tất cả + speedup + DailySettlement
        // =========================================================
        public static async Task RunAllAndSaveAsync(
            string connectionString)
        {
            var data = await LoadDataAsync(connectionString);

            int logicalCores =
                Environment.ProcessorCount;

            var timings =
                new List<BenchmarkTiming>();

            Console.WriteLine();
            Console.WriteLine(
                "==============================================");

            Console.WriteLine(
                "       YC2 - FULL BENCHMARK");

            Console.WriteLine(
                "==============================================");

            Console.WriteLine(
                $"Logical processors: {logicalCores}");

            Console.WriteLine(
                $"MenuItems          : {data.MenuItems.Count:N0}");

            Console.WriteLine(
                $"TicketLines        : {data.Lines.Count:N0}");

            Console.WriteLine();

            // -----------------------------
            // 1. Sequential foreach
            // -----------------------------
            var stopwatch =
                Stopwatch.StartNew();

            var sequentialResults =
                CalculateSequential(
                    data.MenuItems,
                    data.Lines);

            stopwatch.Stop();

            long sequentialMs =
                stopwatch.ElapsedMilliseconds;

            timings.Add(
                new BenchmarkTiming
                {
                    Name = "foreach",
                    ElapsedMs = sequentialMs
                });

            // -----------------------------
            // 2. Parallel.ForEach
            // -----------------------------
            stopwatch.Restart();

            var parallelForEachResults =
                CalculateParallelForEach(
                    data.MenuItems,
                    data.Lines);

            stopwatch.Stop();

            timings.Add(
                new BenchmarkTiming
                {
                    Name = "Parallel.ForEach",
                    ElapsedMs =
                        stopwatch.ElapsedMilliseconds
                });

            // -----------------------------
            // 3. Parallel.For degree = 2
            // -----------------------------
            stopwatch.Restart();

            var parallel2Results =
                CalculateParallelFor(
                    data.MenuItems,
                    data.Lines,
                    2);

            stopwatch.Stop();

            timings.Add(
                new BenchmarkTiming
                {
                    Name = "Parallel.For (2)",
                    ElapsedMs =
                        stopwatch.ElapsedMilliseconds
                });

            // -----------------------------
            // 4. Parallel.For degree = 4
            // -----------------------------
            stopwatch.Restart();

            var parallel4Results =
                CalculateParallelFor(
                    data.MenuItems,
                    data.Lines,
                    4);

            stopwatch.Stop();

            timings.Add(
                new BenchmarkTiming
                {
                    Name = "Parallel.For (4)",
                    ElapsedMs =
                        stopwatch.ElapsedMilliseconds
                });

            // -----------------------------
            // 5. Parallel.For = logical cores
            // -----------------------------
            stopwatch.Restart();

            var parallelCoreResults =
                CalculateParallelFor(
                    data.MenuItems,
                    data.Lines,
                    logicalCores);

            stopwatch.Stop();

            timings.Add(
                new BenchmarkTiming
                {
                    Name =
                        $"Parallel.For ({logicalCores})",

                    ElapsedMs =
                        stopwatch.ElapsedMilliseconds
                });

            // =====================================================
            // In bảng benchmark
            // =====================================================
            Console.WriteLine();
            Console.WriteLine(
                "======================================================");

            Console.WriteLine(
                $"{"Method",-25} {"Time (ms)",12} {"Speedup",12}");

            Console.WriteLine(
                "======================================================");

            foreach (var timing in timings)
            {
                double speedup =
                    sequentialMs * 1.0
                    / Math.Max(1, timing.ElapsedMs);

                Console.WriteLine(
                    $"{timing.Name,-25} " +
                    $"{timing.ElapsedMs,12:N0} " +
                    $"{speedup,12:F2}x");
            }

            Console.WriteLine(
                "======================================================");

            // =====================================================
            // Kiểm tra kết quả các phiên bản giống nhau
            // =====================================================
            double sequentialChecksum =
                sequentialResults.Sum(
                    x => x.EfficiencyScore);

            Console.WriteLine();
            Console.WriteLine(
                $"Result checksum: " +
                $"{sequentialChecksum:F4}");

            // =====================================================
            // Tìm phiên bản nhanh nhất
            // =====================================================
            var fastest =
                timings
                    .OrderBy(x => x.ElapsedMs)
                    .First();

            Console.WriteLine();

            Console.WriteLine(
                $"Fastest method: {fastest.Name}");

            Console.WriteLine(
                $"Fastest time  : {fastest.ElapsedMs:N0} ms");

            // =====================================================
            // Ghi DailySettlement
            // =====================================================
            await SaveDailySettlementsAsync(
                connectionString,
                fastest.ElapsedMs);

            Console.WriteLine();
            Console.WriteLine(
                "DailySettlement saved successfully.");
        }

        // =========================================================
        // Load dữ liệu - không tính vào thời gian benchmark
        // =========================================================
        private static async Task<LoadedData> LoadDataAsync(
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
                "Loading MenuItems and TicketLines...");

            var menuItems =
                await db.MenuItems
                    .AsNoTracking()
                    .OrderBy(x => x.MenuItemId)
                    .ToListAsync();

            var lines =
                await db.TicketLines
                    .AsNoTracking()
                    .Select(x => new LineData
                    {
                        MenuItemId =
                            x.MenuItemId,

                        Quantity =
                            x.Quantity,

                        UnitPrice =
                            x.UnitPrice,

                        CreatedAt =
                            x.OrderTicket.CreatedAt
                    })
                    .ToListAsync();

            Console.WriteLine(
                $"Loaded {lines.Count:N0} TicketLines.");

            return new LoadedData
            {
                MenuItems = menuItems,
                Lines = lines
            };
        }

        // =========================================================
        // Sequential
        // =========================================================
        private static List<MenuEfficiencyResult>
            CalculateSequential(
                List<MenuItem> menuItems,
                List<LineData> lines)
        {
            var results =
                new List<MenuEfficiencyResult>();

            foreach (var menuItem in menuItems)
            {
                results.Add(
                    CalculateOneMenu(
                        menuItem,
                        lines));
            }

            return results;
        }

        // =========================================================
        // Parallel.ForEach
        // =========================================================
        private static List<MenuEfficiencyResult>
            CalculateParallelForEach(
                List<MenuItem> menuItems,
                List<LineData> lines)
        {
            var results =
                new ConcurrentBag<MenuEfficiencyResult>();

            Parallel.ForEach(
                menuItems,
                menuItem =>
                {
                    results.Add(
                        CalculateOneMenu(
                            menuItem,
                            lines));
                });

            return results
                .OrderBy(x => x.MenuItemId)
                .ToList();
        }

        // =========================================================
        // Parallel.For
        // =========================================================
        private static List<MenuEfficiencyResult>
            CalculateParallelFor(
                List<MenuItem> menuItems,
                List<LineData> lines,
                int maxDegree)
        {
            var results =
                new ConcurrentBag<MenuEfficiencyResult>();

            var options =
                new ParallelOptions
                {
                    MaxDegreeOfParallelism =
                        maxDegree
                };

            Parallel.For(
                0,
                menuItems.Count,
                options,
                index =>
                {
                    results.Add(
                        CalculateOneMenu(
                            menuItems[index],
                            lines));
                });

            return results
                .OrderBy(x => x.MenuItemId)
                .ToList();
        }

        private static void RunParallelForAndPrint(
            List<MenuItem> menuItems,
            List<LineData> lines,
            int maxDegree)
        {
            var stopwatch =
                Stopwatch.StartNew();

            CalculateParallelFor(
                menuItems,
                lines,
                maxDegree);

            stopwatch.Stop();

            Console.WriteLine(
                $"Parallel.For " +
                $"(MaxDegree={maxDegree}): " +
                $"{stopwatch.ElapsedMilliseconds:N0} ms");
        }

        // =========================================================
        // Tính chỉ số cho một món
        // =========================================================
        private static MenuEfficiencyResult CalculateOneMenu(
            MenuItem menuItem,
            List<LineData> lines)
        {
            long totalQuantity = 0;
            decimal totalRevenue = 0;
            long peakQuantity = 0;

            foreach (var line in lines)
            {
                if (line.MenuItemId
                    != menuItem.MenuItemId)
                {
                    continue;
                }

                totalQuantity +=
                    line.Quantity;

                totalRevenue +=
                    line.UnitPrice
                    * line.Quantity;

                int hour =
                    line.CreatedAt.Hour;

                if (hour >= 11
                    && hour < 13)
                {
                    peakQuantity +=
                        line.Quantity;
                }
            }

            double peakRatio =
                totalQuantity == 0
                    ? 0
                    : peakQuantity * 1.0
                      / totalQuantity;

            double score =
                CalculateHeavyScore(
                    totalRevenue,
                    totalQuantity,
                    peakRatio);

            return new MenuEfficiencyResult
            {
                MenuItemId =
                    menuItem.MenuItemId,

                Name =
                    menuItem.Name,

                TotalQuantity =
                    totalQuantity,

                TotalRevenue =
                    totalRevenue,

                PeakRatio =
                    peakRatio,

                EfficiencyScore =
                    score
            };
        }

        // =========================================================
        // Công thức CPU-bound cố tình đủ nặng
        // =========================================================
        private static double CalculateHeavyScore(
            decimal revenue,
            long quantity,
            double peakRatio)
        {
            double baseValue =
                Math.Log10((double)revenue + 1)
                * Math.Sqrt(quantity + 1)
                * (1 + peakRatio);

            double accumulator = 0;

            for (int i = 1; i <= 200_000; i++)
            {
                accumulator +=
                    Math.Sqrt(baseValue + i)
                    * Math.Sin(i * 0.001)
                    * Math.Cos(i * 0.0005);
            }

            return
                baseValue
                + peakRatio * 100
                + quantity * 0.001
                + Math.Abs(accumulator)
                  * 0.000001;
        }

        // =========================================================
        // Ghi DailySettlement
        // =========================================================
        private static async Task SaveDailySettlementsAsync(
            string connectionString,
            long calculationTimeMs)
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

            DateTime today =
                DateTime.Today;

            DateTime tomorrow =
                today.AddDays(1);

            // Nếu chạy benchmark lại thì xóa settlement
            // của hôm nay để không tạo bản ghi trùng.
            var existing =
                await db.DailySettlements
                    .Where(
                        x =>
                            BranchCodes.Contains(
                                x.BranchCode)
                            && x.SettlementDate >= today
                            && x.SettlementDate < tomorrow)
                    .ToListAsync();

            if (existing.Count > 0)
            {
                db.DailySettlements.RemoveRange(
                    existing);
            }

            foreach (string branchCode in BranchCodes)
            {
                var tickets =
                    await db.OrderTickets
                        .AsNoTracking()
                        .Where(
                            x =>
                                x.BranchCode == branchCode
                                && x.CreatedAt >= today
                                && x.CreatedAt < tomorrow)
                        .Select(
                            x => new
                            {
                                x.OrderTicketId,
                                x.TotalAmount
                            })
                        .ToListAsync();

                var settlement =
                    new DailySettlement
                    {
                        SettlementDate =
                            today,

                        BranchCode =
                            branchCode,

                        TotalTickets =
                            tickets.Count,

                        TotalRevenue =
                            tickets.Sum(
                                x => x.TotalAmount),

                        CalculationTimeMs =
                            calculationTimeMs
                    };

                db.DailySettlements.Add(
                    settlement);
            }

            await db.SaveChangesAsync();
        }

        private static void PrintResults(
            List<MenuEfficiencyResult> results)
        {
            foreach (var result in results)
            {
                Console.WriteLine(
                    $"{result.MenuItemId,2} | " +
                    $"{result.Name,-20} | " +
                    $"Qty: {result.TotalQuantity,8:N0} | " +
                    $"Revenue: {result.TotalRevenue,12:N0} | " +
                    $"Peak: {result.PeakRatio,6:P2} | " +
                    $"Score: {result.EfficiencyScore,12:F2}");
            }
        }

        private sealed class LoadedData
        {
            public List<MenuItem> MenuItems { get; set; }
                = new();

            public List<LineData> Lines { get; set; }
                = new();
        }

        private sealed class LineData
        {
            public int MenuItemId { get; set; }

            public int Quantity { get; set; }

            public decimal UnitPrice { get; set; }

            public DateTime CreatedAt { get; set; }
        }

        private sealed class MenuEfficiencyResult
        {
            public int MenuItemId { get; set; }

            public string Name { get; set; }
                = string.Empty;

            public long TotalQuantity { get; set; }

            public decimal TotalRevenue { get; set; }

            public double PeakRatio { get; set; }

            public double EfficiencyScore { get; set; }
        }

        private sealed class BenchmarkTiming
        {
            public string Name { get; set; }
                = string.Empty;

            public long ElapsedMs { get; set; }
        }
    }
}