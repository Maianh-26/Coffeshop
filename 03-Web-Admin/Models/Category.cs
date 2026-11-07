using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Admin.Models;

public class Category
{
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Nhập tên danh mục.")]
    [StringLength(80)]
    public string Name { get; set; } = "";

    [Range(1, 999, ErrorMessage = "Thứ tự từ 1 đến 999.")]
    public int SortOrder { get; set; } = 1;

    public bool Visible { get; set; } = true;
}
