using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Admin.Models;

public class Product
{
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Nhập tên món.")]
    [StringLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Chọn danh mục.")]
    public string CategoryId { get; set; } = "";

    [Required(ErrorMessage = "Nhập giá bán.")]
    [Range(
        1,
        100000000,
        ErrorMessage = "Giá phải từ 1 đến 100.000.000 đ."
    )]
    public decimal? Price { get; set; }

    public string Status { get; set; } = "Ngừng bán";

    [StringLength(500)]
    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public List<RecipeLine> Recipe { get; set; } = [];
}
