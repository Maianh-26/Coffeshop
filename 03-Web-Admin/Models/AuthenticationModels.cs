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


/// <summary>
/// Kết quả gọi API xác thực: thành công kèm dữ liệu, hoặc câu báo lỗi tiếng Việt để hiển thị.
/// </summary>
public record AuthResult<T>(bool Success, T? Data, string? Error, string? ErrorCode = null)
{
    public static AuthResult<T> Ok(T data) => new(true, data, null);

    public static AuthResult<T> Fail(string error, string? errorCode = null) =>
        new(false, default, error, errorCode);
}
