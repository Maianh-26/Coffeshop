using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Admin.Models;

/// <summary>
/// Model quản lý Topping và Kích cỡ (Size) nước tích hợp.
/// </summary>
public class Topping
{
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Nhập tên Topping / Kích cỡ.")]
    [StringLength(100, ErrorMessage = "Tên không được vượt quá 100 ký tự.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Chọn phân loại.")]
    public string Type { get; set; } = "Topping"; // "Size" hoặc "Topping"

    [Required(ErrorMessage = "Nhập giá phụ thu.")]
    [Range(0, 10000000, ErrorMessage = "Giá phụ thu từ 0 đến 10.000.000 đ.")]
    public decimal ExtraPrice { get; set; } = 0;

    public string Status { get; set; } = "Đang phục vụ"; // "Đang phục vụ", "Tạm hết", "Ngừng bán"

    [StringLength(250)]
    public string? Description { get; set; }

    /// <summary>
    /// Định mức nguyên liệu tiêu hao khi sử dụng Topping hoặc Size này
    /// </summary>
    public List<RecipeLine> Recipe { get; set; } = [];
}

