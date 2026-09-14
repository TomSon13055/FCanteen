using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FCanteen.Data.Contracts;
using FCanteen.Data.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FCanteen.PosClient
{
    internal class Program
    {
        private static string _connectionString = string.Empty;
        private static string _serverHost = "127.0.0.1";
        private static int _tcpPort = 9500;
        private static int _udpPort = 9501;

        private static readonly ConcurrentDictionary<int, LocalMenuItem>
            Menu = new();

        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        static async Task Main(string[] args)
        {
            string counterName =
                args.Length > 0
                    ? args[0]
                    : "QUAY01";

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection not found.");

            _serverHost =
                configuration["Networking:KitchenServerHost"]
                ?? "127.0.0.1";

            _tcpPort =
                int.Parse(
                    configuration["Networking:TcpPort"]
                    ?? "9500");

            _udpPort =
                int.Parse(
                    configuration["Networking:UdpPort"]
                    ?? "9501");

            await LoadMenuAsync();

            _ = Task.Run(ListenForSoldOutAsync);

            Console.WriteLine(
                $"FCanteen POS - {counterName}");

            Console.WriteLine(
                $"Kitchen Server: {_serverHost}:{_tcpPort}");

            while (true)
            {
                await LoadMenuAsync();

                DisplayMenu();

                var lines = new List<OrderLineRequest>();

                decimal subtotal = 0;

                Console.WriteLine();
                Console.WriteLine(
                    "Nhap mon. 0 = gui phieu, -1 = thoat.");

                while (true)
                {
                    Console.Write("MenuItemId: ");

                    var idText =
                        Console.ReadLine();

                    if (!int.TryParse(idText, out int id))
                    {
                        Console.WriteLine("Invalid ID.");
                        continue;
                    }

                    if (id == -1)
                        return;

                    if (id == 0)
                        break;

                    if (!Menu.TryGetValue(id, out var item))
                    {
                        Console.WriteLine("Menu item not found.");
                        continue;
                    }

                    if (!item.IsAvailable)
                    {
                        Console.WriteLine(
                            $"{item.Name} is SOLD OUT.");
                        continue;
                    }

                    Console.Write("Quantity: ");

                    if (!int.TryParse(
                            Console.ReadLine(),
                            out int quantity)
                        || quantity <= 0)
                    {
                        Console.WriteLine(
                            "Invalid quantity.");
                        continue;
                    }

                    Console.Write("Note: ");

                    var note =
                        Console.ReadLine();

                    if (!item.IsAvailable)
                    {
                        Console.WriteLine(
                            $"{item.Name} has just sold out.");

                        continue;
                    }

                    lines.Add(new OrderLineRequest
                    {
                        MenuItemId = item.MenuItemId,
                        Quantity = quantity,
                        Note =
                            string.IsNullOrWhiteSpace(note)
                                ? null
                                : note
                    });

                    subtotal += item.Price * quantity;

                    Console.WriteLine(
                        $"Added: {item.Name} x{quantity}");

                    Console.WriteLine(
                        $"Tam tinh: {subtotal:N0} VND");
                }

                if (lines.Count == 0)
                {
                    Console.WriteLine(
                        "No item selected.");

                    continue;
                }

                Console.WriteLine();
                Console.WriteLine(
                    $"CLIENT subtotal: {subtotal:N0} VND");

                var request = new OrderRequest
                {
                    CounterName = counterName,
                    Lines = lines
                };

                var response =
                    await SendOrderAsync(request);

                Console.WriteLine();
                Console.WriteLine("===== SERVER RESPONSE =====");
                Console.WriteLine(
                    $"Success : {response.Success}");

                Console.WriteLine(
                    $"Message : {response.Message}");

                if (response.Success)
                {
                    Console.WriteLine(
                        $"Ticket  : #{response.OrderTicketId}");

                    Console.WriteLine(
                        $"SERVER total: " +
                        $"{response.TotalAmount:N0} VND");
                }

                Console.WriteLine("===========================");
                Console.WriteLine();
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

        private static async Task LoadMenuAsync()
        {
            await using var db =
                CreateContext();

            var items =
                await db.MenuItems
                    .AsNoTracking()
                    .OrderBy(x => x.MenuItemId)
                    .ToListAsync();

            Menu.Clear();

            foreach (var item in items)
            {
                Menu[item.MenuItemId] =
                    new LocalMenuItem
                    {
                        MenuItemId = item.MenuItemId,
                        Name = item.Name,
                        Price = item.Price,
                        Unit = item.Unit,
                        IsAvailable =
                            item.IsAvailable
                    };
            }
        }

        private static void DisplayMenu()
        {
            Console.WriteLine();
            Console.WriteLine(
                "==============================================================");

            Console.WriteLine(
                $"{"ID",-4} {"Name",-22} {"Price",12} {"Unit",-8} Status");

            Console.WriteLine(
                "==============================================================");

            foreach (var item in
                     Menu.Values.OrderBy(x => x.MenuItemId))
            {
                Console.WriteLine(
                    $"{item.MenuItemId,-4} " +
                    $"{item.Name,-22} " +
                    $"{item.Price,12:N0} " +
                    $"{item.Unit,-8} " +
                    $"{(item.IsAvailable ? "Available" : "SOLD OUT")}");
            }

            Console.WriteLine(
                "==============================================================");
        }

        private static async Task<OrderResponse> SendOrderAsync(
            OrderRequest request)
        {
            try
            {
                using var client =
                    new TcpClient();

                await client.ConnectAsync(
                    _serverHost,
                    _tcpPort);

                using var stream =
                    client.GetStream();

                using var reader =
                    new StreamReader(
                        stream,
                        Encoding.UTF8,
                        detectEncodingFromByteOrderMarks: false,
                        bufferSize: 1024,
                        leaveOpen: true);

                using var writer =
                    new StreamWriter(
                        stream,
                        new UTF8Encoding(false),
                        bufferSize: 1024,
                        leaveOpen: true)
                    {
                        AutoFlush = true
                    };

                var json =
                    JsonSerializer.Serialize(
                        request,
                        JsonOptions);

                await writer.WriteLineAsync(json);

                var responseJson =
                    await reader.ReadLineAsync();

                if (string.IsNullOrWhiteSpace(responseJson))
                {
                    return new OrderResponse
                    {
                        Success = false,
                        Message =
                            "Server returned no response."
                    };
                }

                return JsonSerializer.Deserialize<OrderResponse>(
                           responseJson,
                           JsonOptions)
                       ?? new OrderResponse
                       {
                           Success = false,
                           Message =
                               "Invalid server response."
                       };
            }
            catch (Exception ex)
            {
                return new OrderResponse
                {
                    Success = false,
                    Message =
                        $"Cannot connect to Kitchen Server: " +
                        $"{ex.Message}"
                };
            }
        }

        private static async Task ListenForSoldOutAsync()
        {
            using var udp =
                new UdpClient(AddressFamily.InterNetwork);

            udp.Client.ExclusiveAddressUse = false;

            udp.Client.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true);

            udp.Client.Bind(
                new IPEndPoint(
                    IPAddress.Any,
                    _udpPort));

            while (true)
            {
                try
                {
                    var result =
                        await udp.ReceiveAsync();

                    var json =
                        Encoding.UTF8.GetString(
                            result.Buffer);

                    var message =
                        JsonSerializer.Deserialize<SoldOutMessage>(
                            json,
                            JsonOptions);

                    if (message == null
                        || message.Type != "SOLD_OUT")
                    {
                        continue;
                    }

                    if (Menu.TryGetValue(
                            message.MenuItemId,
                            out var item))
                    {
                        item.IsAvailable = false;
                    }

                    Console.WriteLine();
                    Console.WriteLine(
                        $"*** UDP ALERT: " +
                        $"{message.Name} " +
                        $"(#{message.MenuItemId}) SOLD OUT ***");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"UDP error: {ex.Message}");
                }
            }
        }

        private sealed class LocalMenuItem
        {
            public int MenuItemId { get; set; }

            public string Name { get; set; }
                = string.Empty;

            public decimal Price { get; set; }

            public string Unit { get; set; }
                = string.Empty;

            public bool IsAvailable { get; set; }
        }
    }
}