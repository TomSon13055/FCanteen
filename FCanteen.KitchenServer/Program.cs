using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FCanteen.Data.Contracts;
using FCanteen.Data.Data;
using FCanteen.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FCanteen.KitchenServer
{
    internal class Program
    {
        private const int TcpPort = 9500;
        private const int UdpPort = 9501;

        private static string _connectionString = string.Empty;

        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        private static readonly object ConsoleLock = new();

        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection not found.");

            var listener = new TcpListener(IPAddress.Any, TcpPort);
            listener.Start();

            Console.WriteLine("==========================================");
            Console.WriteLine(" FCanteen Kitchen Server");
            Console.WriteLine($" TCP Port: {TcpPort}");
            Console.WriteLine($" UDP Port: {UdpPort}");
            Console.WriteLine("==========================================");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("pending");
            Console.WriteLine("soldout <MenuItemId>");
            Console.WriteLine("sync <URL>");
            Console.WriteLine();

            _ = Task.Run(AdminLoopAsync);

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();

                _ = Task.Run(() => HandleClientAsync(client));
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

        private static async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                string remoteAddress =
                    client.Client.RemoteEndPoint?.ToString() ?? "Unknown";

                try
                {
                    using var stream = client.GetStream();

                    using var reader = new StreamReader(
                        stream,
                        Encoding.UTF8,
                        detectEncodingFromByteOrderMarks: false,
                        bufferSize: 1024,
                        leaveOpen: true);

                    using var writer = new StreamWriter(
                        stream,
                        new UTF8Encoding(false),
                        bufferSize: 1024,
                        leaveOpen: true)
                    {
                        AutoFlush = true
                    };

                    var json = await reader.ReadLineAsync();

                    if (string.IsNullOrWhiteSpace(json))
                        return;

                    await WriteLogAsync(
                        "TCP",
                        remoteAddress,
                        $"IN: {json}");

                    OrderResponse response;

                    try
                    {
                        var request =
                            JsonSerializer.Deserialize<OrderRequest>(
                                json,
                                JsonOptions);

                        if (request == null)
                        {
                            response = new OrderResponse
                            {
                                Success = false,
                                Message = "Invalid request."
                            };
                        }
                        else
                        {
                            response = await SaveOrderAsync(request);
                        }
                    }
                    catch (Exception ex)
                    {
                        response = new OrderResponse
                        {
                            Success = false,
                            Message = ex.Message
                        };
                    }

                    var responseJson =
                        JsonSerializer.Serialize(response, JsonOptions);

                    await writer.WriteLineAsync(responseJson);

                    await WriteLogAsync(
                        "TCP",
                        remoteAddress,
                        $"OUT: {responseJson}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Client {remoteAddress} error: {ex.Message}");
                }
            }
        }

        private static async Task<OrderResponse> SaveOrderAsync(
            OrderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CounterName))
            {
                return new OrderResponse
                {
                    Success = false,
                    Message = "Counter name is required."
                };
            }

            if (request.Lines.Count == 0)
            {
                return new OrderResponse
                {
                    Success = false,
                    Message = "Order has no items."
                };
            }

            await using var db = CreateContext();
            await using var transaction =
                await db.Database.BeginTransactionAsync();

            try
            {
                var ids = request.Lines
                    .Select(x => x.MenuItemId)
                    .Distinct()
                    .ToList();

                var menuItems = await db.MenuItems
                    .Where(x => ids.Contains(x.MenuItemId))
                    .ToDictionaryAsync(x => x.MenuItemId);

                decimal total = 0;

                foreach (var line in request.Lines)
                {
                    if (line.Quantity <= 0)
                    {
                        return new OrderResponse
                        {
                            Success = false,
                            Message = "Quantity must be greater than zero."
                        };
                    }

                    if (!menuItems.TryGetValue(
                            line.MenuItemId,
                            out var item))
                    {
                        return new OrderResponse
                        {
                            Success = false,
                            Message =
                                $"Menu item {line.MenuItemId} not found."
                        };
                    }

                    if (!item.IsAvailable)
                    {
                        return new OrderResponse
                        {
                            Success = false,
                            Message =
                                $"{item.Name} is sold out."
                        };
                    }

                    total += item.Price * line.Quantity;
                }

                var ticket = new OrderTicket
                {
                    CounterName = request.CounterName,
                    TotalAmount = total,
                    CreatedAt = DateTime.Now,
                    Status = "Pending"
                };

                foreach (var line in request.Lines)
                {
                    var item = menuItems[line.MenuItemId];

                    ticket.TicketLines.Add(new TicketLine
                    {
                        MenuItemId = item.MenuItemId,
                        Quantity = line.Quantity,
                        UnitPrice = item.Price,
                        Note = line.Note
                    });
                }

                db.OrderTickets.Add(ticket);

                await db.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine();
                Console.WriteLine(
                    $"Received ticket #{ticket.OrderTicketId} " +
                    $"from {ticket.CounterName} - " +
                    $"{ticket.TotalAmount:N0} VND");

                await PrintPendingTicketsAsync();

                return new OrderResponse
                {
                    Success = true,
                    OrderTicketId = ticket.OrderTicketId,
                    TotalAmount = ticket.TotalAmount,
                    Message = "Order accepted."
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static async Task PrintPendingTicketsAsync()
        {
            await using var db = CreateContext();

            var tickets = await db.OrderTickets
                .Where(x => x.Status == "Pending")
                .Include(x => x.TicketLines)
                .ThenInclude(x => x.MenuItem)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();

            lock (ConsoleLock)
            {
                Console.WriteLine();
                Console.WriteLine("===== PENDING TICKETS =====");

                if (tickets.Count == 0)
                {
                    Console.WriteLine("No pending tickets.");
                    return;
                }

                foreach (var ticket in tickets)
                {
                    Console.WriteLine(
                        $"#{ticket.OrderTicketId} | " +
                        $"{ticket.CounterName} | " +
                        $"{ticket.CreatedAt:HH:mm:ss} | " +
                        $"{ticket.TotalAmount:N0} VND");

                    foreach (var line in ticket.TicketLines)
                    {
                        Console.WriteLine(
                            $"   - {line.MenuItem.Name} " +
                            $"x{line.Quantity} " +
                            $"@ {line.UnitPrice:N0} " +
                            $"Note: {line.Note ?? ""}");
                    }
                }

                Console.WriteLine("===========================");
            }
        }

        private static async Task AdminLoopAsync()
        {
            while (true)
            {
                var command = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(command))
                    continue;

                try
                {
                    if (command.Equals(
                        "pending",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        await PrintPendingTicketsAsync();
                    }
                    else if (command.StartsWith(
                        "soldout ",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        var value = command["soldout ".Length..].Trim();

                        if (int.TryParse(value, out int menuItemId))
                        {
                            await MarkSoldOutAsync(menuItemId);
                        }
                        else
                        {
                            Console.WriteLine(
                                "Usage: soldout <MenuItemId>");
                        }
                    }
                    else if (command.StartsWith(
                        "sync ",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        var url = command["sync ".Length..].Trim();

                        await SyncPricesAsync(url);
                    }
                    else
                    {
                        Console.WriteLine("Unknown command.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Command error: {ex.Message}");
                }
            }
        }

        private static async Task MarkSoldOutAsync(int menuItemId)
        {
            await using var db = CreateContext();

            var item = await db.MenuItems
                .FirstOrDefaultAsync(
                    x => x.MenuItemId == menuItemId);

            if (item == null)
            {
                Console.WriteLine("Menu item not found.");
                return;
            }

            if (!item.IsAvailable)
            {
                Console.WriteLine($"{item.Name} is already sold out.");
                return;
            }

            item.IsAvailable = false;

            await db.SaveChangesAsync();

            var message = new SoldOutMessage
            {
                MenuItemId = item.MenuItemId,
                Name = item.Name
            };

            await BroadcastSoldOutAsync(message);

            Console.WriteLine(
                $"SOLD OUT: #{item.MenuItemId} {item.Name}");
        }

        private static async Task BroadcastSoldOutAsync(
            SoldOutMessage message)
        {
            using var udp = new UdpClient();

            udp.EnableBroadcast = true;

            var json =
                JsonSerializer.Serialize(message, JsonOptions);

            var bytes = Encoding.UTF8.GetBytes(json);

            var endpoint =
                new IPEndPoint(IPAddress.Broadcast, UdpPort);

            await udp.SendAsync(
                bytes,
                bytes.Length,
                endpoint);

            await WriteLogAsync(
                "UDP",
                GetLocalIPv4(),
                $"Broadcast SOLD_OUT: {json}");
        }

        private static async Task SyncPricesAsync(string url)
        {
            var uri = new Uri(url);

            Console.WriteLine();
            Console.WriteLine("===== URI =====");
            Console.WriteLine($"Scheme: {uri.Scheme}");
            Console.WriteLine($"Host  : {uri.Host}");
            Console.WriteLine($"Port  : {uri.Port}");

            var dnsWatch = Stopwatch.StartNew();

            var addresses =
                await Dns.GetHostAddressesAsync(uri.Host);

            dnsWatch.Stop();

            Console.WriteLine("IP addresses:");

            foreach (var address in addresses)
            {
                Console.WriteLine($"- {address}");
            }

            await WriteLogAsync(
                "DNS",
                GetLocalIPv4(),
                $"Host={uri.Host}; " +
                $"IPs={string.Join(",", addresses.Select(x => x.ToString()))}; " +
                $"Time={dnsWatch.ElapsedMilliseconds}ms");

            using var httpClient = new HttpClient();

            var watch = Stopwatch.StartNew();

            using var response =
                await httpClient.GetAsync(uri);

            watch.Stop();

            var body =
                await response.Content.ReadAsStringAsync();

            Console.WriteLine();
            Console.WriteLine(
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}");

            Console.WriteLine(
                $"Response time: {watch.ElapsedMilliseconds} ms");

            await WriteLogAsync(
                "HTTP",
                GetLocalIPv4(),
                $"GET {url}; " +
                $"Status={(int)response.StatusCode}; " +
                $"Time={watch.ElapsedMilliseconds}ms");

            response.EnsureSuccessStatusCode();

            var priceList =
                JsonSerializer.Deserialize<List<PriceSyncItem>>(
                    body,
                    JsonOptions)
                ?? new List<PriceSyncItem>();

            await using var db = CreateContext();

            int changed = 0;

            foreach (var remoteItem in priceList)
            {
                var localItem =
                    await db.MenuItems.FindAsync(
                        remoteItem.MenuItemId);

                if (localItem == null)
                    continue;

                if (localItem.Price != remoteItem.Price)
                {
                    Console.WriteLine(
                        $"{localItem.Name}: " +
                        $"{localItem.Price:N0} -> " +
                        $"{remoteItem.Price:N0}");

                    localItem.Price = remoteItem.Price;

                    changed++;
                }
            }

            await db.SaveChangesAsync();

            Console.WriteLine(
                $"Price synchronization completed. " +
                $"Changed: {changed}");
        }

        private static async Task WriteLogAsync(
            string protocol,
            string sourceAddress,
            string content)
        {
            await using var db = CreateContext();

            db.DeviceLogs.Add(new DeviceLog
            {
                Protocol = protocol,
                SourceAddress = sourceAddress,
                Content = content,
                CreatedAt = DateTime.Now
            });

            await db.SaveChangesAsync();
        }

        private static string GetLocalIPv4()
        {
            try
            {
                return Dns.GetHostAddresses(Dns.GetHostName())
                    .FirstOrDefault(
                        x => x.AddressFamily ==
                             AddressFamily.InterNetwork)
                    ?.ToString()
                    ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }
    }
}