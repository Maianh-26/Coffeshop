namespace CoffeeShop.Admin.Models;

/// <summary>
/// Thông tin đơn hàng
/// </summary>
public class Order
{
    public string Code { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? PaidAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    /// <summary>
    /// Trạng thái đơn: "Chờ tiếp nhận", "Đang pha chế", "Hoàn thành", "Đã hủy"
    /// </summary>
    public string Status { get; set; } = "Chờ tiếp nhận";

    /// <summary>
    /// Trạng thái thanh toán: "Chưa thanh toán", "Đã thanh toán"
    /// </summary>
    public string PaymentStatus { get; set; } = "Chưa thanh toán";

    public string CashierName { get; set; } = "Thu ngân";
    public string TableOrArea { get; set; } = "Mang đi";
    public string PaymentMethod { get; set; } = "Tiền mặt"; // "Tiền mặt", "Chuyển khoản QR"
    public string Source { get; set; } = "Tại quầy"; // "Tablet", "Tại quầy"
    public string? Note { get; set; }

    public List<OrderItem> Items { get; set; } = [];

    /// <summary>
    /// Tạm tính (giá gốc các món)
    /// </summary>
    public decimal SubTotal => Items.Sum(i => (i.BasePrice > 0 ? i.BasePrice : i.UnitPrice) * i.Quantity);

    /// <summary>
    /// Tổng tiền phụ thu Topping & Kích cỡ size nước
    /// </summary>
    public decimal ToppingTotal => Items.Sum(i => (i.SizeExtraPrice + (i.Toppings != null ? i.Toppings.Sum(t => t.Price) : 0)) * i.Quantity);

    /// <summary>
    /// Giảm giá / Khuyến mãi áp dụng
    /// </summary>
    public decimal DiscountAmount { get; set; } = 0;

    /// <summary>
    /// Tổng tiền thực tế cần thanh toán của cả đơn hàng
    /// </summary>
    public decimal Total
    {
        get => _total > 0 ? _total : Math.Max(0, Items.Sum(i => i.TotalPrice) - DiscountAmount);
        set => _total = value;
    }
    private decimal _total;
}
