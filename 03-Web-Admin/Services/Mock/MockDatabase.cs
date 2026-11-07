using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services.Mock;

/// <summary>
/// Dữ liệu giả lập trong bộ nhớ, dùng khi chưa có backend (Api:UseMock = true).
/// Dữ liệu sống trong suốt phiên trình duyệt; tải lại trang (F5) sẽ về dữ liệu mẫu ban đầu.
/// </summary>
public class MockDatabase
{
    public List<Category> Categories { get; } = [];
    public List<Product> Products { get; } = [];
    public List<Topping> Toppings { get; } = [];
    public List<Ingredient> Ingredients { get; } = [];
    public List<InventoryTransaction> Transactions { get; } = [];
    public List<Order> Orders { get; } = [];
    public List<Promotion> Promotions { get; } = [];
    public List<Account> Accounts { get; } = [];

    /// <summary>
    /// Mật khẩu mặc định của mọi tài khoản giả lập (và tài khoản chủ quán tạo mới trong chế độ giả lập).
    /// </summary>
    public const string DefaultPassword = "123456";

    /// <summary>Tài khoản đăng nhập giả lập: username → mật khẩu.</summary>
    public Dictionary<string, string> Passwords { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["admin"] = DefaultPassword,
        ["nhanvien"] = DefaultPassword,
        ["minh.nv"] = DefaultPassword,
        ["lan.nv"] = DefaultPassword
    };

    /// <summary>Mã xác minh quên mật khẩu đang chờ: email → thông tin mã.</summary>
    public Dictionary<string, ResetCode> ResetCodes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Nhật ký xin mã theo email (kể cả email không có tài khoản): dùng cho giới hạn gửi lại / số lần mỗi giờ.
    /// Không bị xóa khi mã hết hạn hay bị hủy → không lách được giới hạn.
    /// </summary>
    public Dictionary<string, List<DateTime>> ResetRequestLog { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Token đặt lại mật khẩu (dùng một lần): token → (username, hết hạn).</summary>
    public Dictionary<string, (string Username, DateTime ExpiresAt)> ResetTokens { get; } = [];

    public class ResetCode
    {
        public string Username { get; init; } = "";
        public string Code { get; init; } = "";
        public DateTime ExpiresAt { get; init; }
        public DateTime SentAt { get; init; }
        public int FailedAttempts { get; set; }
    }

    public MockDatabase()
    {
        SeedCatalog();
        SeedInventory();
        SeedOrders();
        SeedPromotionsAndAccounts();
    }

    /// <summary>
    /// Sinh mã kế tiếp theo tiền tố, ví dụ "SP" + danh sách có SP0005 → "SP0006".
    /// </summary>
    public static string NextCode(IEnumerable<string> existing, string prefix, int width = 4)
    {
        int max = 0;

        foreach (string code in existing)
        {
            if (code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(code[prefix.Length..], out int number))
            {
                max = Math.Max(max, number);
                width = Math.Max(width, code.Length - prefix.Length);
            }
        }

        return prefix + (max + 1).ToString().PadLeft(width, '0');
    }

    private void SeedCatalog()
    {
        Categories.AddRange(
        [
            new() { Code = "DM0001", Name = "Cà phê", SortOrder = 1, Visible = true },
            new() { Code = "DM0002", Name = "Trà sữa", SortOrder = 2, Visible = true },
            new() { Code = "DM0003", Name = "Trà trái cây", SortOrder = 3, Visible = true },
            new() { Code = "DM0004", Name = "Bánh & Ăn kèm", SortOrder = 4, Visible = true }
        ]);

        Ingredients.AddRange(
        [
            new() { Code = "NL0001", Name = "Cà phê hạt Arabica", Unit = "g" },
            new() { Code = "NL0002", Name = "Sữa đặc Ngôi Sao", Unit = "ml" },
            new() { Code = "NL0003", Name = "Sữa tươi thanh trùng", Unit = "ml" },
            new() { Code = "NL0004", Name = "Trân châu đen thô", Unit = "g" },
            new() { Code = "NL0005", Name = "Bột phô mai Macchiato", Unit = "g" },
            new() { Code = "NL0006", Name = "Bột Pudding trứng", Unit = "g" },
            new() { Code = "NL0007", Name = "Ly giấy 500ml", Unit = "cái" },
            new() { Code = "NL0008", Name = "Ly giấy 700ml", Unit = "cái" }
        ]);

        Toppings.AddRange(
        [
            new() { Code = "SZ-M", Name = "Size M (Vừa 500ml)", Type = "Size", ExtraPrice = 0, Status = "Đang phục vụ", Description = "Kích cỡ chuẩn mặc định", Recipe = [new() { IngredientId = "NL0007", Quantity = 1 }] },
            new() { Code = "SZ-L", Name = "Size L (Lớn 700ml)", Type = "Size", ExtraPrice = 6000, Status = "Đang phục vụ", Description = "Kích cỡ lớn (+6.000 đ)", Recipe = [new() { IngredientId = "NL0008", Quantity = 1 }] },
            new() { Code = "SZ-XL", Name = "Size XL (Khổng lồ 1000ml)", Type = "Size", ExtraPrice = 12000, Status = "Đang phục vụ", Description = "Kích cỡ siêu to (+12.000 đ)" },
            new() { Code = "TP-TC", Name = "Trân châu đen", Type = "Topping", ExtraPrice = 8000, Status = "Đang phục vụ", Description = "Trân châu đường đen dẻo bùi", Recipe = [new() { IngredientId = "NL0004", Quantity = 35 }] },
            new() { Code = "TP-TCTRANG", Name = "Trân châu trắng", Type = "Topping", ExtraPrice = 8000, Status = "Đang phục vụ", Description = "Trân châu thủy tinh giòn sần sật" },
            new() { Code = "TP-KEM", Name = "Kem Cheese Macchiato", Type = "Topping", ExtraPrice = 10000, Status = "Đang phục vụ", Description = "Lớp kem béo ngậy vị mặn nhẹ", Recipe = [new() { IngredientId = "NL0005", Quantity = 25 }] },
            new() { Code = "TP-FLAN", Name = "Pudding trứng", Type = "Topping", ExtraPrice = 10000, Status = "Đang phục vụ", Description = "Pudding mềm mịn tan trong miệng", Recipe = [new() { IngredientId = "NL0006", Quantity = 30 }] },
            new() { Code = "TP-THACH", Name = "Thạch nha đam", Type = "Topping", ExtraPrice = 6000, Status = "Tạm hết", Description = "Nha đam giòn ngọt thanh nhiệt" }
        ]);

        Products.AddRange(
        [
            new() { Code = "SP0001", Name = "Cà phê sữa đá", CategoryId = "DM0001", Price = 29000, Status = "Đang bán", Description = "Cà phê pha phin truyền thống thơm đượm", Recipe = [new() { IngredientId = "NL0001", Quantity = 25 }, new() { IngredientId = "NL0002", Quantity = 40 }] },
            new() { Code = "SP0002", Name = "Bạc xỉu", CategoryId = "DM0001", Price = 32000, Status = "Đang bán", Description = "Nhiều sữa ít cà phê, béo ngậy ngọt ngào", Recipe = [new() { IngredientId = "NL0001", Quantity = 15 }, new() { IngredientId = "NL0002", Quantity = 30 }, new() { IngredientId = "NL0003", Quantity = 80 }] },
            new() { Code = "SP0003", Name = "Trà sữa Oolong nướng", CategoryId = "DM0002", Price = 42000, Status = "Đang bán", Description = "Vị trà oolong đậm đà hòa quyện khói nướng đặc trưng" },
            new() { Code = "SP0004", Name = "Trà đào cam sả", CategoryId = "DM0003", Price = 39000, Status = "Đang bán", Description = "Thanh mát giải nhiệt cùng đào miếng giòn ngọt" },
            new() { Code = "SP0005", Name = "Trà sữa Matcha Nhật", CategoryId = "DM0002", Price = 45000, Status = "Đang bán", Description = "Matcha Uji nguyên chất thơm thanh tao" },
            new() { Code = "SP0006", Name = "Cà phê đen đá", CategoryId = "DM0001", Price = 25000, Status = "Ngừng bán", Description = "Cà phê phin nguyên chất, đậm vị", Recipe = [new() { IngredientId = "NL0001", Quantity = 25 }] }
        ]);
    }

    private void SeedInventory()
    {
        DateTime today = DateTime.Today;

        (string ingredient, decimal quantity, decimal unitCost, int daysAgo)[] purchases =
        [
            ("NL0001", 5000, 350, 6),
            ("NL0002", 10000, 60, 6),
            ("NL0003", 20000, 35, 4),
            ("NL0004", 3000, 90, 3),
            ("NL0007", 500, 900, 2),
            ("NL0008", 500, 1100, 1)
        ];

        foreach (var p in purchases)
        {
            Ingredient ingredient = Ingredients.First(i => i.Code == p.ingredient);

            Transactions.Add(new InventoryTransaction
            {
                Code = NextCode(Transactions.Select(t => t.Code), "PK"),
                IngredientId = ingredient.Code,
                IngredientName = ingredient.Name,
                Unit = ingredient.Unit,
                Type = "Nhập mua",
                Quantity = p.quantity,
                UnitCost = p.unitCost,
                CreatedAt = today.AddDays(-p.daysAgo).AddHours(8)
            });
        }
    }

    private void SeedOrders()
    {
        DateTime now = DateTime.Now;

        // Lịch sử 6 ngày trước: mỗi ngày vài đơn đã thanh toán, để Dashboard có số liệu.
        int sequence = 100;

        for (int day = 6; day >= 1; day--)
        {
            for (int i = 0; i < 2 + day % 3; i++)
            {
                DateTime created = now.Date.AddDays(-day).AddHours(8 + i * 3);
                Product product = Products[(day + i) % 5];

                Orders.Add(new Order
                {
                    Code = $"DH{sequence++:00000}",
                    CreatedAt = created,
                    AcceptedAt = created.AddMinutes(2),
                    PaidAt = created.AddMinutes(5),
                    Status = "Hoàn thành",
                    PaymentStatus = "Đã thanh toán",
                    PaymentMethod = i % 2 == 0 ? "Tiền mặt" : "Chuyển khoản QR",
                    Source = i % 2 == 0 ? "Tablet" : "Tại quầy",
                    CashierName = "Nguyễn Thu Hà",
                    TableOrArea = i % 2 == 0 ? $"Bàn {i + 1:00}" : "Mang đi",
                    Items =
                    [
                        new OrderItem
                        {
                            ProductCode = product.Code,
                            ProductName = product.Name,
                            BasePrice = product.Price ?? 0,
                            Quantity = 1 + i % 2,
                            SizeName = "Size M (Vừa 500ml)"
                        }
                    ]
                });
            }
        }

        // Ba đơn hôm nay (giống dữ liệu mẫu cũ trong trang Đơn hàng).
        Orders.Add(new Order
        {
            Code = "DH00123",
            CreatedAt = now.AddHours(-1),
            AcceptedAt = now.AddHours(-1).AddMinutes(2),
            PaidAt = now.AddHours(-1).AddMinutes(5),
            Status = "Hoàn thành",
            PaymentStatus = "Đã thanh toán",
            CashierName = "Nguyễn Thu Hà",
            TableOrArea = "Mang đi",
            PaymentMethod = "Tiền mặt",
            Source = "Tại quầy",
            Items = [new OrderItem { ProductCode = "SP0001", ProductName = "Cà phê sữa đá", BasePrice = 29000, Quantity = 2, SizeName = "Size M (Vừa 500ml)" }]
        });

        Orders.Add(new Order
        {
            Code = "DH00124",
            CreatedAt = now.AddMinutes(-20),
            AcceptedAt = now.AddMinutes(-18),
            Status = "Đang pha chế",
            PaymentStatus = "Chưa thanh toán",
            CashierName = "Tablet (Khách gọi)",
            TableOrArea = "Bàn 02 (Sân vườn)",
            PaymentMethod = "Chuyển khoản QR",
            Source = "Tablet",
            Items =
            [
                new OrderItem
                {
                    ProductCode = "SP0004", ProductName = "Trà đào cam sả", BasePrice = 39000, Quantity = 2,
                    SizeName = "Size L (Lớn 700ml)", SizeExtraPrice = 6000, Note = "Đậm vị đào",
                    Toppings =
                    [
                        new() { ToppingCode = "TP-THACH", ToppingName = "Thạch nha đam", Price = 6000 },
                        new() { ToppingCode = "TP-TCTRANG", ToppingName = "Trân châu trắng", Price = 8000 }
                    ]
                }
            ]
        });

        Orders.Add(new Order
        {
            Code = "DH00125",
            CreatedAt = now.AddMinutes(-5),
            Status = "Chờ tiếp nhận",
            PaymentStatus = "Chưa thanh toán",
            CashierName = "Tablet (Khách gọi)",
            TableOrArea = "Bàn 04 (Tầng 1)",
            PaymentMethod = "Tiền mặt",
            Source = "Tablet",
            Items =
            [
                new OrderItem
                {
                    ProductCode = "SP0003", ProductName = "Trà sữa Oolong nướng", BasePrice = 42000, Quantity = 1,
                    SizeName = "Size L (Lớn 700ml)", SizeExtraPrice = 6000, Note = "50% đường, ít đá",
                    Toppings =
                    [
                        new() { ToppingCode = "TP-TC", ToppingName = "Trân châu đen", Price = 8000 },
                        new() { ToppingCode = "TP-KEM", ToppingName = "Kem Cheese", Price = 10000 }
                    ]
                },
                new OrderItem
                {
                    ProductCode = "SP0001", ProductName = "Cà phê sữa đá", BasePrice = 29000, Quantity = 1,
                    SizeName = "Size M (Vừa 500ml)", Note = "Nhiều sữa ít ngọt",
                    Toppings = [new() { ToppingCode = "TP-THACH", ToppingName = "Thạch nha đam", Price = 6000 }]
                }
            ]
        });
    }

    private void SeedPromotionsAndAccounts()
    {
        DateTime today = DateTime.Today;

        Promotions.AddRange(
        [
            new() { Code = "KM0001", Name = "Giảm 10% cuối tuần", DiscountPercent = 10, StartDate = today.AddDays(-3), EndDate = today.AddDays(4) },
            new() { Code = "KM0002", Name = "Khai trương giảm 20%", DiscountPercent = 20, StartDate = today.AddDays(-40), EndDate = today.AddDays(-30) }
        ]);

        Accounts.AddRange(
        [
            // Mật khẩu của tất cả: DefaultPassword (123456)
            new() { Code = "TK0001", FullName = "Chủ quán", Username = "admin", Email = "admin@coffeeshop.local", Role = "Admin" },
            new() { Code = "TK0002", FullName = "Nguyễn Thu Hà", Username = "nhanvien", Email = "thuha@coffeeshop.local", Role = "Nhân viên" },
            new() { Code = "TK0003", FullName = "Lê Minh", Username = "minh.nv", Email = "minh.nv@coffeeshop.local", Role = "Nhân viên" },
            new() { Code = "TK0004", FullName = "Phạm Ngọc Lan", Username = "lan.nv", Email = "lan.nv@coffeeshop.local", Role = "Nhân viên" }
        ]);
    }

    /// <summary>
    /// Giả lập khách vừa đặt một đơn mới từ tablet.
    /// </summary>
    public Order AddRandomTabletOrder()
    {
        var sellable = Products.Where(p => p.Status == "Đang bán").ToList();
        Product product = sellable[Random.Shared.Next(sellable.Count)];

        var order = new Order
        {
            Code = NextCode(Orders.Select(o => o.Code), "DH", 5),
            CreatedAt = DateTime.Now,
            Status = "Chờ tiếp nhận",
            PaymentStatus = "Chưa thanh toán",
            CashierName = "Tablet (Khách gọi)",
            TableOrArea = $"Bàn {Random.Shared.Next(1, 13):00}",
            PaymentMethod = "Tiền mặt",
            Source = "Tablet",
            Items =
            [
                new OrderItem
                {
                    ProductCode = product.Code,
                    ProductName = product.Name,
                    BasePrice = product.Price ?? 0,
                    Quantity = Random.Shared.Next(1, 3),
                    SizeName = "Size M (Vừa 500ml)"
                }
            ]
        };

        Orders.Add(order);
        return order;
    }
}
