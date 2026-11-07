using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Admin.Models;

public class InventoryTransaction
{
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Chọn nguyên liệu.")]
    public string IngredientId { get; set; } = "";

    public string IngredientName { get; set; } = "";

    [Required(ErrorMessage = "Chọn loại giao dịch.")]
    public string Type { get; set; } = "Nhập mua";

    [Required(ErrorMessage = "Nhập số lượng.")]
    [Range(0.001, 1000000, ErrorMessage = "Số lượng phải lớn hơn 0.")]
    public decimal Quantity { get; set; }

    public string Unit { get; set; } = "";

    [Range(0, 100000000, ErrorMessage = "Đơn giá từ 0 đến 100.000.000 đ.")]
    public decimal? UnitCost { get; set; }

    public decimal Cost
    {
        get => UnitCost.HasValue ? UnitCost.Value * Quantity : _cost;
        set => _cost = value;
    }
    private decimal _cost;

    public bool IsPurchase
    {
        get => Type == "Nhập mua";
        set
        {
            if (value) Type = "Nhập mua";
        }
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string? Note { get; set; }
}
