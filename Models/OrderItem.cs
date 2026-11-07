namespace CoffeeShop.Admin.Models;

/// <summary>
/// Topping hoặc tùy chọn được chọn kèm theo món trong đơn hàng
/// </summary>
public class OrderItemTopping
{
    public string ToppingCode { get; set; } = "";
    public string ToppingName { get; set; } = "";
    public decimal Price { get; set; }
}

/// <summary>
/// Chi tiết món đã đặt trong đơn hàng
/// </summary>
public class OrderItem
{
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public decimal BasePrice { get; set; }
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Kích cỡ size nước đã chọn (ví dụ: Size M, Size L)
    /// </summary>
    public string SizeName { get; set; } = "Size M";

    /// <summary>
    /// Giá phụ thu của size nước
    /// </summary>
    public decimal SizeExtraPrice { get; set; } = 0;

    /// <summary>
    /// Danh sách Topping đặt thêm
    /// </summary>
    public List<OrderItemTopping> Toppings { get; set; } = [];

    /// <summary>
    /// Ghi chú món (ví dụ: Ít ngọt, 50% đá)
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Đơn giá 1 sản phẩm đầy đủ = Giá gốc + Phụ thu size + Tổng topping
    /// </summary>
    public decimal UnitPrice
    {
        get => _unitPrice > 0 ? _unitPrice : (BasePrice + SizeExtraPrice + (Toppings != null ? Toppings.Sum(t => t.Price) : 0));
        set => _unitPrice = value;
    }
    private decimal _unitPrice;

    /// <summary>
    /// Thành tiền của dòng món = Đơn giá x Số lượng
    /// </summary>
    public decimal TotalPrice => UnitPrice * Quantity;
}
