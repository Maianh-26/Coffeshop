using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Admin.Models;

// ==========================================================
// Người dùng & xác thực — api/v1/auth/*, api/v1/nguoi-dung
// ==========================================================

/// <summary>
/// Tài khoản (NguoiDungDto). VaiTro: "QuanLy" | "NhanVien" (xem Utilities/Roles).
/// </summary>
public class NguoiDung
{
    public int MaNguoiDung { get; set; }
    public string TenDangNhap { get; set; } = "";
    public string HoTen { get; set; } = "";
    /// <summary>Không bắt buộc (web không nhập email); null = không có.</summary>
    public string? Email { get; set; }
    public string VaiTro { get; set; } = "";

    /// <summary>
    /// true: hoạt động, false: bị khóa (khóa từ trước; web không còn nút khóa, hiện nhãn "Bị khóa" để mở khóa hoặc xóa).
    /// Tài khoản đã xóa (DELETE) backend không trả về nữa.
    /// </summary>
    public bool TrangThai { get; set; } = true;
}

/// <summary>POST api/v1/auth/dang-nhap</summary>
public class DangNhapRequest
{
    public string TenDangNhap { get; set; } = "";
    public string MatKhau { get; set; } = "";
}

public class DangNhapResponse
{
    public string Token { get; set; } = "";
    public DateTime HetHan { get; set; }
    public NguoiDung NguoiDung { get; set; } = new();
}

/// <summary>POST api/v1/auth/doi-mat-khau → 204</summary>
public class DoiMatKhauRequest
{
    public string MatKhauCu { get; set; } = "";
    public string MatKhauMoi { get; set; } = "";
}

/// <summary>POST api/v1/nguoi-dung (chỉ QuanLy)</summary>
public class NguoiDungTaoRequest
{
    public string TenDangNhap { get; set; } = "";
    public string MatKhau { get; set; } = "";
    public string HoTen { get; set; } = "";

    /// <summary>Không bắt buộc: web không có ô email nên luôn gửi null (backend coi null / "" là không có email).</summary>
    public string? Email { get; set; }

    public string VaiTro { get; set; } = "NhanVien";
}

/// <summary>
/// PATCH api/v1/nguoi-dung/{id}: trường nào null thì backend giữ nguyên.
/// </summary>
public class NguoiDungCapNhatRequest
{
    public string? HoTen { get; set; }
    public string? Email { get; set; }
    public string? VaiTro { get; set; }

    /// <summary>false = khóa tài khoản (web không dùng; xóa tài khoản dùng DELETE).</summary>
    public bool? TrangThai { get; set; }

    /// <summary>Quản lý đặt lại mật khẩu cho nhân viên.</summary>
    public string? MatKhauMoi { get; set; }
}

/// <summary>
/// Form thêm/sửa tài khoản trên trang Tài khoản (kiểm tra giống ràng buộc của backend).
/// </summary>
public class NguoiDungForm
{
    public int MaNguoiDung { get; set; }

    /// <summary>true = form thêm mới → mật khẩu bắt buộc.</summary>
    public bool IsNew { get; set; }

    [Required(ErrorMessage = "Nhập tên đăng nhập.")]
    [RegularExpression("^[a-zA-Z0-9_.]{3,50}$",
        ErrorMessage = "Tên đăng nhập 3–50 ký tự, chỉ gồm chữ không dấu, số, dấu chấm, gạch dưới.")]
    public string TenDangNhap { get; set; } = "";

    [Required(ErrorMessage = "Nhập họ và tên.")]
    [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
    public string HoTen { get; set; } = "";

    public string VaiTro { get; set; } = "NhanVien";

    public bool TrangThai { get; set; } = true;

    /// <summary>Bắt buộc khi thêm; khi sửa để trống = giữ mật khẩu cũ.</summary>
    [RequiredWhenNew(ErrorMessage = "Nhập mật khẩu (tối thiểu 6 ký tự).")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu từ 6 đến 100 ký tự.")]
    public string? MatKhau { get; set; }
}

/// <summary>
/// Bắt buộc khi form đang thêm mới (<see cref="NguoiDungForm.IsNew"/>). Kiểm tra ở mức ô (không phải IValidatableObject)
/// để lỗi hiện dưới ô Mật khẩu cùng lúc với các ô khác — kiểm tra mức form chỉ chạy khi mọi ô đã hợp lệ.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RequiredWhenNewAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        validationContext.ObjectInstance is NguoiDungForm { IsNew: true } && string.IsNullOrEmpty(value as string)
            ? new ValidationResult(ErrorMessage, validationContext.MemberName is { } member ? [member] : null)
            : ValidationResult.Success;
}
