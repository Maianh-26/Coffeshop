using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Admin.Models;

public class Ingredient
{
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Nhập tên nguyên liệu.")]
    [StringLength(100, ErrorMessage = "Tên không được vượt quá 100 ký tự.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Nhập đơn vị tính.")]
    [StringLength(20, ErrorMessage = "Đơn vị không được vượt quá 20 ký tự.")]
    public string Unit { get; set; } = "";
}
