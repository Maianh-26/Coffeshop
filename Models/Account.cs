using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Admin.Models;

public class Account
{
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Nhập họ và tên.")]
    [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Nhập tên đăng nhập.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập từ 3 đến 50 ký tự.")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = "";

    public string Role { get; set; } = "";
}
