using FCanteen.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FCanteen.Data.Data
{
    public class FCanteenContext : DbContext
    {
        public FCanteenContext(DbContextOptions<FCanteenContext> options)
            : base(options)
        {
        }

        public DbSet<MenuItem> MenuItems { get; set; }

        public DbSet<OrderTicket> OrderTickets { get; set; }

        public DbSet<TicketLine> TicketLines { get; set; }

        public DbSet<DeviceLog> DeviceLogs { get; set; }

        public DbSet<Ingredient> Ingredients { get; set; }

        public DbSet<DailySettlement> DailySettlements { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<MenuItem>()
                .Property(x => x.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<OrderTicket>()
                .Property(x => x.TotalAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<TicketLine>()
                .Property(x => x.UnitPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<TicketLine>()
                .HasOne(x => x.OrderTicket)
                .WithMany(x => x.TicketLines)
                .HasForeignKey(x => x.OrderTicketId);

            modelBuilder.Entity<TicketLine>()
                .HasOne(x => x.MenuItem)
                .WithMany(x => x.TicketLines)
                .HasForeignKey(x => x.MenuItemId);

            modelBuilder.Entity<Ingredient>()
                .Property(x => x.StockQuantity)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Ingredient>()
                .Property(x => x.AlertThreshold)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<DailySettlement>()
                .Property(x => x.TotalRevenue)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<OrderTicket>()
                 .HasIndex(x => x.CreatedAt);

            modelBuilder.Entity<OrderTicket>()
                .HasIndex(x => new
                {
                    x.BranchCode,
                    x.CreatedAt
                });

            modelBuilder.Entity<MenuItem>().HasData(
                new MenuItem
                {
                    MenuItemId = 1,
                    Name = "Com ga",
                    Price = 35000m,
                    Unit = "Phan",
                    IsAvailable = true
                },
                new MenuItem
                {
                    MenuItemId = 2,
                    Name = "Com suon",
                    Price = 40000m,
                    Unit = "Phan",
                    IsAvailable = true
                },
                new MenuItem
                {
                    MenuItemId = 3,
                    Name = "Com bo xao",
                    Price = 45000m,
                    Unit = "Phan",
                    IsAvailable = true
                },
                new MenuItem
                {
                    MenuItemId = 4,
                    Name = "Mi xao bo",
                    Price = 40000m,
                    Unit = "Dia",
                    IsAvailable = true
                },
                new MenuItem
                {
                    MenuItemId = 5,
                    Name = "Bun bo",
                    Price = 40000m,
                    Unit = "To",
                    IsAvailable = true
                },
                new MenuItem
                {
                    MenuItemId = 6,
                    Name = "Pho bo",
                    Price = 45000m,
                    Unit = "To",
                    IsAvailable = true
                },
                new MenuItem
                {
                    MenuItemId = 7,
                    Name = "Banh mi thit",
                    Price = 20000m,
                    Unit = "Cai",
                    IsAvailable = true
                },
                new MenuItem
                {
                    MenuItemId = 8,
                    Name = "Banh bao",
                    Price = 15000m,
                    Unit = "Cai",
                    IsAvailable = true
                },
                new MenuItem
                {
                    MenuItemId = 9,
                    Name = "Xoi ga",
                    Price = 25000m,
                    Unit = "Hop",
                    IsAvailable = true
                },
                new MenuItem
                {
                    MenuItemId = 10,
                    Name = "Tra sua",
                    Price = 25000m,
                    Unit = "Ly",
                    IsAvailable = true
                },
                new MenuItem
                {
                    MenuItemId = 11,
                    Name = "Ca phe sua",
                    Price = 20000m,
                    Unit = "Ly",
                    IsAvailable = true
                },
                new MenuItem
                {
                    MenuItemId = 12,
                    Name = "Nuoc cam",
                    Price = 25000m,
                    Unit = "Ly",
                    IsAvailable = true
                },
                new MenuItem
                {
                    MenuItemId = 13,
                    Name = "Nuoc suoi",
                    Price = 10000m,
                    Unit = "Chai",
                    IsAvailable = true
                },
                new MenuItem
                {
                    MenuItemId = 14,
                    Name = "Coca Cola",
                    Price = 15000m,
                    Unit = "Lon",
                    IsAvailable = true
                },
                new MenuItem
                {
                    MenuItemId = 15,
                    Name = "Pepsi",
                    Price = 15000m,
                    Unit = "Lon",
                    IsAvailable = true
                }
            );
        }
    }
}