using System.Diagnostics;
using FCanteen.Data.Data;
using FCanteen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FCanteen.Lab02Runner.Seeders
{
    public static class LargeDataSeeder
    {
        private const int TargetTicketCount = 50_000;
        private const int LinesPerTicket = 4;

        // Mỗi lần chỉ lưu 200 phiếu.
        // 200 Ticket + 800 TicketLine = khoảng 1.000 entity / batch.
        private const int BatchSize = 50;

        private static readonly string[] BranchCodes =
        {
            "DN",
            "HCM",
            "HN"
        };

        public static async Task SeedAsync(string connectionString)
        {
            var options = new DbContextOptionsBuilder<FCanteenContext>()
    .UseSqlServer(
        connectionString,
        sqlOptions =>
        {
            sqlOptions.CommandTimeout(300);

            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        })
    .Options;

            // ==============================
            // 1. KIỂM TRA DỮ LIỆU HIỆN CÓ
            // ==============================

            int existingLab02Tickets;

            List<MenuItem> menuItems;

            await using (var db = new FCanteenContext(options))
            {
                existingLab02Tickets =
                    await db.OrderTickets
                        .CountAsync(
                            x => BranchCodes.Contains(x.BranchCode));

                menuItems =
                    await db.MenuItems
                        .AsNoTracking()
                        .OrderBy(x => x.MenuItemId)
                        .ToListAsync();
            }

            if (existingLab02Tickets >= TargetTicketCount)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"Large data already exists: " +
                    $"{existingLab02Tickets:N0} tickets.");

                await PrintFinalStatisticsAsync(options);

                return;
            }

            if (menuItems.Count < LinesPerTicket)
            {
                throw new InvalidOperationException(
                    $"Need at least {LinesPerTicket} MenuItems before seeding.");
            }

            int remainingTickets =
                TargetTicketCount - existingLab02Tickets;

            // ==============================
            // 2. CHUẨN BỊ SINH DỮ LIỆU
            // ==============================

            var random = new Random(2026);

            DateTime startDate =
                DateTime.Today.AddMonths(-6);

            int totalDays =
                (DateTime.Today - startDate).Days + 1;

            var stopwatch =
                Stopwatch.StartNew();

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("       FCANTEEN LARGE DATA SEEDER");
            Console.WriteLine("========================================");

            Console.WriteLine(
                $"Target OrderTickets    : {TargetTicketCount:N0}");

            Console.WriteLine(
                $"Target TicketLines     : " +
                $"{TargetTicketCount * LinesPerTicket:N0}");

            Console.WriteLine(
                $"Existing Lab02 tickets : {existingLab02Tickets:N0}");

            Console.WriteLine(
                $"Remaining to seed      : {remainingTickets:N0}");

            Console.WriteLine(
                $"Batch size             : {BatchSize:N0} tickets");

            Console.WriteLine(
                $"Branches               : " +
                $"{string.Join(", ", BranchCodes)}");

            Console.WriteLine(
                $"Data period            : " +
                $"{startDate:dd/MM/yyyy} - " +
                $"{DateTime.Today:dd/MM/yyyy}");

            Console.WriteLine(
                "Peak-hour ratio        : 70% at 11:00-13:00");

            Console.WriteLine(
                "AutoDetectChanges      : False");

            Console.WriteLine("========================================");
            Console.WriteLine();

            // ==============================
            // 3. CHẠY THEO TỪNG BATCH
            // ==============================

            for (int offset = 0;
                 offset < remainingTickets;
                 offset += BatchSize)
            {
                int currentBatchSize =
                    Math.Min(
                        BatchSize,
                        remainingTickets - offset);

                // Mỗi batch dùng DbContext riêng.
                await using var db =
                    new FCanteenContext(options);

                // YC1 yêu cầu tắt AutoDetectChanges.
                db.ChangeTracker.AutoDetectChangesEnabled = false;

                var tickets =
                    new List<OrderTicket>(currentBatchSize);

                for (int i = 0;
                     i < currentBatchSize;
                     i++)
                {
                    int globalIndex =
                        existingLab02Tickets
                        + offset
                        + i;

                    // Chia đều 3 cơ sở.
                    string branchCode =
                        BranchCodes[
                            globalIndex % BranchCodes.Length];

                    DateTime createdAt =
                        CreateOrderTime(
                            startDate,
                            totalDays,
                            globalIndex,
                            random);

                    // Chọn 4 món khác nhau cho mỗi phiếu.
                    var selectedItems =
                        menuItems
                            .OrderBy(_ => random.Next())
                            .Take(LinesPerTicket)
                            .ToList();

                    var ticket =
                        new OrderTicket
                        {
                            CounterName =
                                $"QUAY{random.Next(1, 4):00}",

                            BranchCode =
                                branchCode,

                            CreatedAt =
                                createdAt,

                            Status =
                                "Completed",

                            TotalAmount =
                                0
                        };

                    decimal totalAmount = 0;

                    foreach (var item in selectedItems)
                    {
                        int quantity =
                            random.Next(1, 4);

                        var line =
                            new TicketLine
                            {
                                MenuItemId =
                                    item.MenuItemId,

                                Quantity =
                                    quantity,

                                UnitPrice =
                                    item.Price,

                                Note =
                                    random.NextDouble() < 0.10
                                        ? "Generated data"
                                        : null,

                                // Gắn rõ line vào ticket.
                                OrderTicket =
                                    ticket
                            };

                        ticket.TicketLines.Add(line);

                        totalAmount +=
                            item.Price * quantity;
                    }

                    ticket.TotalAmount =
                        totalAmount;

                    tickets.Add(ticket);
                }

                // Thêm cả batch.
                db.OrderTickets.AddRange(tickets);

                // Lưu batch xuống SQL Server.
                await db.SaveChangesAsync();

                int completed =
                    existingLab02Tickets
                    + offset
                    + currentBatchSize;

                double percent =
                    completed * 100.0
                    / TargetTicketCount;

                Console.WriteLine(
                    $"Seeded " +
                    $"{completed,6:N0}/{TargetTicketCount:N0} tickets " +
                    $"({percent,6:F2}%) " +
                    $"| Expected lines: " +
                    $"{completed * LinesPerTicket,7:N0} " +
                    $"| {stopwatch.ElapsedMilliseconds,8:N0} ms");
            }

            // ==============================
            // 4. KẾT THÚC
            // ==============================

            stopwatch.Stop();

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("            SEED COMPLETED");
            Console.WriteLine("========================================");

            await PrintFinalStatisticsAsync(options);

            Console.WriteLine(
                $"Seeder elapsed    : " +
                $"{stopwatch.ElapsedMilliseconds:N0} ms");

            Console.WriteLine(
                $"Seeder elapsed    : " +
                $"{stopwatch.Elapsed.TotalSeconds:N2} seconds");

            Console.WriteLine("========================================");
        }

        private static DateTime CreateOrderTime(
            DateTime startDate,
            int totalDays,
            int index,
            Random random)
        {
            // Chia tương đối đều dữ liệu trong 6 tháng.
            int dayOffset =
                index % totalDays;

            DateTime date =
                startDate.AddDays(dayOffset);

            int hour;

            // 70% đơn hàng nằm trong giờ cao điểm 11h-13h.
            if (random.NextDouble() < 0.70)
            {
                // Random.Next(11, 13)
                // => 11 hoặc 12
                // => từ 11:00 đến 12:59.
                hour =
                    random.Next(11, 13);
            }
            else
            {
                // 30% còn lại nằm trong 7h-20h,
                // nhưng không rơi vào 11h hoặc 12h.
                do
                {
                    hour =
                        random.Next(7, 21);
                }
                while (hour is 11 or 12);
            }

            int minute =
                random.Next(0, 60);

            int second =
                random.Next(0, 60);

            return date
                .AddHours(hour)
                .AddMinutes(minute)
                .AddSeconds(second);
        }

        private static async Task PrintFinalStatisticsAsync(
            DbContextOptions<FCanteenContext> options)
        {
            await using var db =
                new FCanteenContext(options);

            int finalTicketCount =
                await db.OrderTickets
                    .CountAsync(
                        x => BranchCodes.Contains(x.BranchCode));

            int finalLineCount =
                await db.TicketLines
                    .CountAsync(
                        x => BranchCodes.Contains(
                            x.OrderTicket.BranchCode));

            int dnCount =
                await db.OrderTickets
                    .CountAsync(
                        x => x.BranchCode == "DN");

            int hcmCount =
                await db.OrderTickets
                    .CountAsync(
                        x => x.BranchCode == "HCM");

            int hnCount =
                await db.OrderTickets
                    .CountAsync(
                        x => x.BranchCode == "HN");

            int peakHourCount =
                await db.OrderTickets
                    .CountAsync(
                        x =>
                            BranchCodes.Contains(x.BranchCode)
                            && x.CreatedAt.Hour >= 11
                            && x.CreatedAt.Hour < 13);

            double peakPercentage =
                finalTicketCount == 0
                    ? 0
                    : peakHourCount * 100.0
                      / finalTicketCount;

            Console.WriteLine(
                $"Final OrderTickets : {finalTicketCount:N0}");

            Console.WriteLine(
                $"Final TicketLines  : {finalLineCount:N0}");

            Console.WriteLine();

            Console.WriteLine(
                $"DN                 : {dnCount:N0}");

            Console.WriteLine(
                $"HCM                : {hcmCount:N0}");

            Console.WriteLine(
                $"HN                 : {hnCount:N0}");

            Console.WriteLine();

            Console.WriteLine(
                $"11h-13h tickets    : {peakHourCount:N0}");

            Console.WriteLine(
                $"Peak-hour ratio    : {peakPercentage:F2}%");
        }
    }
}