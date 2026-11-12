using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Admin.Models;

public class LoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public bool Success { get; set; }

    public string? Token { get; set; }

    public string? FullName { get; set; }

    public string? Role { get; set; }

    public string? Message { get; set; }
}

