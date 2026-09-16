using System.Diagnostics;
using System.Runtime.CompilerServices;
using FCanteen.Data.Data;
using Microsoft.EntityFrameworkCore;

namespace FCanteen.Lab02Runner.AsyncDemos
{
    public static class AsyncDailyReport
    {
        private static readonly string[] BranchCodes =
        {
            "DN",
            "HCM",
            "HN"
        };

        // =========================================================
        // YC4 - VERSION 1
        // 4 truy vấn chạy đồng thời bằng Task.WhenAll.
        // Mỗi truy vấn tự tạo một DbContext riêng.
        // =========================================================
        public static async Task<DailyReportResult>
            GenerateDailyReportAsync(
                string connectionString,
                DateTime reportDate,
                CancellationToken cancellationToken)
        {
            DateTime start =
                reportDate.Date;

            DateTime end =
                start.AddDays(1);

            // Query 1
            Task<int> totalTicketsTask =
                GetTotalTicketsAsync(
                    connectionString,
                    start,
                    end,
                    cancellationToken);

            // Query 2
            Task<decimal> totalRevenueTask =
                GetTotalRevenueAsync(
                    connectionString,
                    start,
                    end,
                    cancellationToken);

            // Query 3
            Task<decimal> averageTicketTask =
                GetAverageTicketValueAsync(
                    connectionString,
                    start,
                    end,
                    cancellationToken);

            // Query 4
            Task<List<BranchRevenueResult>> branchRevenueTask =
                GetRevenueByBranchAsync(
                    connectionString,
                    start,
                    end,
                    cancellationToken);

            // Chạy/chờ cả 4 task đồng thời.
            await Task.WhenAll(
                totalTicketsTask,
                totalRevenueTask,
                averageTicketTask,
                branchRevenueTask);

            return new DailyReportResult
            {
                TotalTickets =
                    await totalTicketsTask,

                TotalRevenue =
                    await totalRevenueTask,

                AverageTicketValue =
                    await averageTicketTask,

                BranchRevenues =
                    await branchRevenueTask
            };
        }

        // =========================================================
        // YC4 - VERSION 2
        // Cùng 4 query nhưng await nối tiếp.
        // =========================================================
        public static async Task<DailyReportResult>
            GenerateDailyReportSequentialAsync(
                string connectionString,
                DateTime reportDate,
                CancellationToken cancellationToken)
        {
            DateTime start =
                reportDate.Date;

            DateTime end =
                start.AddDays(1);

            int totalTickets =
                await GetTotalTicketsAsync(
                    connectionString,
                    start,
                    end,
                    cancellationToken);

            decimal totalRevenue =
                await GetTotalRevenueAsync(
                    connectionString,
                    start,
                    end,
                    cancellationToken);

            decimal averageTicketValue =
                await GetAverageTicketValueAsync(
                    connectionString,
                    start,
                    end,
                    cancellationToken);

            List<BranchRevenueResult> branchRevenues =
                await GetRevenueByBranchAsync(
                    connectionString,
                    start,
                    end,
                    cancellationToken);

            return new DailyReportResult
            {
                TotalTickets =
                    totalTickets,

                TotalRevenue =
                    totalRevenue,

                AverageTicketValue =
                    averageTicketValue,

                BranchRevenues =
                    branchRevenues
            };
        }

        // =========================================================
        // Chạy demo YC4:
        // - tìm ngày mới nhất có dữ liệu
        // - chạy sequential
        // - chạy Task.WhenAll
        // - đo Stopwatch
        // - CancellationToken 30 giây
        // - demo IAsyncEnumerable
        // =========================================================
        public static async Task RunComparisonAsync(
            string connectionString)
        {
            Console.WriteLine();
            Console.WriteLine(
                "==============================================");

            Console.WriteLine(
                "       YC4 - ASYNC EF CORE");

            Console.WriteLine(
                "==============================================");

            // -----------------------------------------------------
            // Tìm ngày mới nhất đang có dữ liệu
            // -----------------------------------------------------
            DateTime? latestDate;

            try
            {
                using var findDateCts =
                    new CancellationTokenSource(
                        TimeSpan.FromSeconds(30));

                latestDate =
                    await GetLatestTicketDateAsync(
                        connectionString,
                        findDateCts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine(
                    "Finding report date exceeded 30 seconds.");

                return;
            }

            if (latestDate == null)
            {
                Console.WriteLine(
                    "No OrderTicket data found.");

                return;
            }

            DateTime reportDate =
                latestDate.Value.Date;

            Console.WriteLine(
                $"Report date: {reportDate:dd/MM/yyyy}");

            Console.WriteLine();

            try
            {
                // =================================================
                // 1. SEQUENTIAL AWAIT
                // =================================================

                using var sequentialCts =
                    new CancellationTokenSource(
                        TimeSpan.FromSeconds(30));

                var stopwatch =
                    Stopwatch.StartNew();

                DailyReportResult sequentialResult =
                    await GenerateDailyReportSequentialAsync(
                        connectionString,
                        reportDate,
                        sequentialCts.Token);

                stopwatch.Stop();

                long sequentialMs =
                    stopwatch.ElapsedMilliseconds;

                // =================================================
                // 2. TASK.WHENALL
                // =================================================

                using var whenAllCts =
                    new CancellationTokenSource(
                        TimeSpan.FromSeconds(30));

                stopwatch.Restart();

                DailyReportResult whenAllResult =
                    await GenerateDailyReportAsync(
                        connectionString,
                        reportDate,
                        whenAllCts.Token);

                stopwatch.Stop();

                long whenAllMs =
                    stopwatch.ElapsedMilliseconds;

                // =================================================
                // IN DAILY REPORT
                // =================================================

                Console.WriteLine(
                    "===== DAILY REPORT =====");

                PrintReport(
                    whenAllResult);

                // =================================================
                // IN PERFORMANCE
                // =================================================

                Console.WriteLine();
                Console.WriteLine(
                    "===== PERFORMANCE =====");

                Console.WriteLine(
                    $"Sequential await : " +
                    $"{sequentialMs:N0} ms");

                Console.WriteLine(
                    $"Task.WhenAll     : " +
                    $"{whenAllMs:N0} ms");

                double speedup =
                    sequentialMs * 1.0
                    / Math.Max(1, whenAllMs);

                Console.WriteLine(
                    $"Speedup          : " +
                    $"{speedup:F2}x");

                // =================================================
                // IASYNCENUMERABLE
                // =================================================

                Console.WriteLine();
                Console.WriteLine(
                    "===== HIGH VALUE TICKETS =====");

                Console.WriteLine(
                    $"Streaming tickets on " +
                    $"{reportDate:dd/MM/yyyy} " +
                    $">= 200,000 VND");

                Console.WriteLine();

                int displayed =
                    0;

                using var streamCts =
                    new CancellationTokenSource(
                        TimeSpan.FromSeconds(30));

                await foreach (
                    HighValueTicket ticket
                    in StreamHighValueTicketsAsync(
                        connectionString,
                        reportDate,
                        200_000m,
                        streamCts.Token))
                {
                    Console.WriteLine(
                        $"#{ticket.OrderTicketId,-7} " +
                        $"| {ticket.BranchCode,-4} " +
                        $"| {ticket.CounterName,-7} " +
                        $"| {ticket.CreatedAt:dd/MM/yyyy HH:mm} " +
                        $"| {ticket.TotalAmount,12:N0}");

                    displayed++;

                    // Chỉ in tối đa 20 record ra màn hình.
                    if (displayed >= 20)
                    {
                        break;
                    }
                }

                Console.WriteLine();

                Console.WriteLine(
                    $"Displayed {displayed} " +
                    $"high-value tickets.");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine();

                Console.WriteLine(
                    "Operation cancelled because " +
                    "it exceeded 30 seconds.");
            }
        }

        // =========================================================
        // TÌM NGÀY MỚI NHẤT CÓ DỮ LIỆU
        // =========================================================
        private static async Task<DateTime?>
            GetLatestTicketDateAsync(
                string connectionString,
                CancellationToken cancellationToken)
        {
            await using var db =
                CreateContext(
                    connectionString);

            return await db.OrderTickets
                .AsNoTracking()
                .Where(
                    x => BranchCodes.Contains(
                        x.BranchCode))
                .MaxAsync(
                    x => (DateTime?)x.CreatedAt,
                    cancellationToken);
        }

        // =========================================================
        // QUERY 1
        // Tổng số phiếu trong ngày
        // DbContext riêng
        // =========================================================
        private static async Task<int>
            GetTotalTicketsAsync(
                string connectionString,
                DateTime start,
                DateTime end,
                CancellationToken cancellationToken)
        {
            await using var db =
                CreateContext(
                    connectionString);

            return await db.OrderTickets
                .AsNoTracking()
                .CountAsync(
                    x =>
                        BranchCodes.Contains(
                            x.BranchCode)
                        && x.CreatedAt >= start
                        && x.CreatedAt < end,
                    cancellationToken);
        }

        // =========================================================
        // QUERY 2
        // Tổng doanh thu trong ngày
        // DbContext riêng
        // =========================================================
        private static async Task<decimal>
            GetTotalRevenueAsync(
                string connectionString,
                DateTime start,
                DateTime end,
                CancellationToken cancellationToken)
        {
            await using var db =
                CreateContext(
                    connectionString);

            decimal? result =
                await db.OrderTickets
                    .AsNoTracking()
                    .Where(
                        x =>
                            BranchCodes.Contains(
                                x.BranchCode)
                            && x.CreatedAt >= start
                            && x.CreatedAt < end)
                    .Select(
                        x => (decimal?)x.TotalAmount)
                    .SumAsync(
                        cancellationToken);

            return result ?? 0;
        }

        // =========================================================
        // QUERY 3
        // Giá trị phiếu trung bình
        // DbContext riêng
        // =========================================================
        private static async Task<decimal>
            GetAverageTicketValueAsync(
                string connectionString,
                DateTime start,
                DateTime end,
                CancellationToken cancellationToken)
        {
            await using var db =
                CreateContext(
                    connectionString);

            decimal? result =
                await db.OrderTickets
                    .AsNoTracking()
                    .Where(
                        x =>
                            BranchCodes.Contains(
                                x.BranchCode)
                            && x.CreatedAt >= start
                            && x.CreatedAt < end)
                    .Select(
                        x => (decimal?)x.TotalAmount)
                    .AverageAsync(
                        cancellationToken);

            return result ?? 0;
        }

        // =========================================================
        // QUERY 4
        // Doanh thu theo từng cơ sở
        // DbContext riêng
        // =========================================================
        private static async Task<List<BranchRevenueResult>>
            GetRevenueByBranchAsync(
                string connectionString,
                DateTime start,
                DateTime end,
                CancellationToken cancellationToken)
        {
            await using var db =
                CreateContext(
                    connectionString);

            return await db.OrderTickets
                .AsNoTracking()
                .Where(
                    x =>
                        BranchCodes.Contains(
                            x.BranchCode)
                        && x.CreatedAt >= start
                        && x.CreatedAt < end)
                .GroupBy(
                    x => x.BranchCode)
                .Select(
                    g => new BranchRevenueResult
                    {
                        BranchCode =
                            g.Key,

                        Revenue =
                            g.Sum(
                                x => x.TotalAmount)
                    })
                .OrderBy(
                    x => x.BranchCode)
                .ToListAsync(
                    cancellationToken);
        }

        // =========================================================
        // IAsyncEnumerable
        //
        // Không gọi ToListAsync().
        // Các record được đọc dần bằng await foreach.
        // =========================================================
        public static async IAsyncEnumerable<HighValueTicket>
            StreamHighValueTicketsAsync(
                string connectionString,
                DateTime reportDate,
                decimal minimumAmount,
                [EnumeratorCancellation]
                CancellationToken cancellationToken)
        {
            await using var db =
                CreateContext(
                    connectionString);

            DateTime start =
                reportDate.Date;

            DateTime end =
                start.AddDays(1);

            var query =
                db.OrderTickets
                    .AsNoTracking()
                    .Where(
                        x =>
                            BranchCodes.Contains(
                                x.BranchCode)
                            && x.CreatedAt >= start
                            && x.CreatedAt < end
                            && x.TotalAmount
                                >= minimumAmount)
                    .OrderByDescending(
                        x => x.TotalAmount)
                    .Select(
                        x => new HighValueTicket
                        {
                            OrderTicketId =
                                x.OrderTicketId,

                            BranchCode =
                                x.BranchCode,

                            CounterName =
                                x.CounterName,

                            TotalAmount =
                                x.TotalAmount,

                            CreatedAt =
                                x.CreatedAt
                        })
                    .AsAsyncEnumerable();

            await foreach (
                HighValueTicket ticket
                in query.WithCancellation(
                    cancellationToken))
            {
                yield return ticket;
            }
        }

        // =========================================================
        // Tạo DbContext mới.
        //
        // Rất quan trọng:
        // mỗi query của Task.WhenAll gọi method này riêng.
        // Không dùng chung DbContext cho nhiều task.
        // =========================================================
        private static FCanteenContext CreateContext(
            string connectionString)
        {
            var options =
                new DbContextOptionsBuilder<FCanteenContext>()
                    .UseSqlServer(
                        connectionString,
                        sqlOptions =>
                        {
                            // SQL command có thể chờ lâu hơn,
                            // nhưng CancellationToken ở phía ngoài
                            // vẫn tự hủy sau 30 giây.
                            sqlOptions.CommandTimeout(300);
                        })
                    .Options;

            return new FCanteenContext(
                options);
        }

        // =========================================================
        // In Daily Report
        // =========================================================
        private static void PrintReport(
            DailyReportResult report)
        {
            Console.WriteLine(
                $"Total tickets  : " +
                $"{report.TotalTickets:N0}");

            Console.WriteLine(
                $"Total revenue  : " +
                $"{report.TotalRevenue:N0}");

            Console.WriteLine(
                $"Average ticket : " +
                $"{report.AverageTicketValue:N0}");

            Console.WriteLine();
            Console.WriteLine(
                "Revenue by branch:");

            if (report.BranchRevenues.Count == 0)
            {
                Console.WriteLine(
                    "  No branch data.");
            }
            else
            {
                foreach (
                    BranchRevenueResult branch
                    in report.BranchRevenues)
                {
                    Console.WriteLine(
                        $"  {branch.BranchCode,-4}: " +
                        $"{branch.Revenue,15:N0}");
                }
            }
        }

        // =========================================================
        // DTOs
        // =========================================================
        public sealed class DailyReportResult
        {
            public int TotalTickets { get; set; }

            public decimal TotalRevenue { get; set; }

            public decimal AverageTicketValue { get; set; }

            public List<BranchRevenueResult>
                BranchRevenues
            { get; set; }
                = new();
        }

        public sealed class BranchRevenueResult
        {
            public string BranchCode { get; set; }
                = string.Empty;

            public decimal Revenue { get; set; }
        }

        public sealed class HighValueTicket
        {
            public int OrderTicketId { get; set; }

            public string BranchCode { get; set; }
                = string.Empty;

            public string CounterName { get; set; }
                = string.Empty;

            public decimal TotalAmount { get; set; }

            public DateTime CreatedAt { get; set; }
        }
    }
}