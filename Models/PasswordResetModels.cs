using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Admin.Models;

// ==========================================================
// Quên mật khẩu: email → mã xác minh → đặt mật khẩu mới
// API: api/auth/forgot-password, api/auth/verify-reset-code, api/auth/reset-password
// ==========================================================

/// <summary>
/// Bước 1 — yêu cầu gửi mã xác minh tới email của tài khoản.
/// </summary>
public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = "";
}

public class ForgotPasswordResponse
{
    public string? Message { get; set; }

    /// <summary>
    /// Số giây phải chờ trước khi được gửi lại mã.
    /// </summary>
    public int ResendAfterSeconds { get; set; } = 60;

    /// <summary>
    /// CHỈ có ở API giả lập (để demo khi chưa gửi được Gmail). Backend thật không bao giờ trả trường này.
    /// </summary>
    public string? DevCode { get; set; }
}

/// <summary>
/// Bước 2 — kiểm tra mã xác minh 6 số.
/// </summary>
public class VerifyResetCodeRequest
{
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mã xác minh.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác minh gồm 6 chữ số.")]
    public string Code { get; set; } = "";
}

public class VerifyResetCodeResponse
{
    /// <summary>
    /// Token dùng một lần, có hạn — chỉ dùng cho bước đặt mật khẩu mới.
    /// </summary>
    public string ResetToken { get; set; } = "";
}

/// <summary>
/// Bước 3 — đặt mật khẩu mới (form trên web; gửi lên API chỉ ResetToken + NewPassword).
/// </summary>
public class ResetPasswordForm
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự.")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    public string ConfirmPassword { get; set; } = "";
}

public class ResetPasswordRequest
{
    public string ResetToken { get; set; } = "";

    public string NewPassword { get; set; } = "";
}

// ==========================================================
// Đổi mật khẩu khi đã đăng nhập (trang "Tài khoản của tôi")
// API: POST api/auth/change-password
// ==========================================================

public class ChangePasswordForm
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự.")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    public string ConfirmPassword { get; set; } = "";
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";

    public string NewPassword { get; set; } = "";
}

/// <summary>
/// Kết quả chung của các bước: thành công / thông báo lỗi tiếng Việt để hiển thị,
/// kèm mã lỗi máy đọc được (vd "code_expired", "too_many_attempts", "invalid_code").
/// </summary>
public record AuthResult<T>(bool Success, T? Data, string? Error, string? ErrorCode = null, int? RetryAfterSeconds = null)
{
    public static AuthResult<T> Ok(T data) => new(true, data, null);

    public static AuthResult<T> Fail(string error, string? errorCode = null, int? retryAfterSeconds = null) =>
        new(false, default, error, errorCode, retryAfterSeconds);
}

/// <summary>
/// Mã lỗi (ProblemDetails "errorCode") của luồng quên mật khẩu — thống nhất với backend.
/// </summary>
public static class ResetErrorCodes
{
    public const string InvalidCode = "invalid_code";
    public const string CodeExpired = "code_expired";
    public const string TooManyAttempts = "too_many_attempts";
    public const string TokenExpired = "token_expired";
    public const string ResendTooSoon = "resend_too_soon";
    public const string WrongPassword = "wrong_password";
}
