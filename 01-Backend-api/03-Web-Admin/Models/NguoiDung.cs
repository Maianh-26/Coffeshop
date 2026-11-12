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
    public string Email { get; set; } = "";
    public string VaiTro { get; set; } = "";

    /// <summary>true: hoạt động, false: bị khóa.</summary>
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
    public string Email { get; set; } = "";
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

    /// <summary>false = khóa tài khoản.</summary>
    public bool? TrangThai { get; set; }

    /// <summary>Quản lý đặt lại mật khẩu cho nhân viên.</summary>
    public string? MatKhauMoi { get; set; }
}

/// <summary>
/// Form thêm/sửa tài khoản trên trang Tài khoản (kiểm tra giống ràng buộc của backend).
/// </summary>
public class NguoiDungForm : IValidatableObject
{
    public int MaNguoiDung { get; set; }

    /// <summary>true = form thêm mới → mật khẩu bắt buộc.</summary>
    public bool IsNew { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsNew && string.IsNullOrEmpty(MatKhau))
        {
            yield return new ValidationResult("Nhập mật khẩu (tối thiểu 6 ký tự).", [nameof(MatKhau)]);
        }
    }

    [Required(ErrorMessage = "Nhập tên đăng nhập.")]
    [RegularExpression("^[a-zA-Z0-9_.]{3,50}$",
        ErrorMessage = "Tên đăng nhập 3–50 ký tự, chỉ gồm chữ không dấu, số, dấu chấm, gạch dưới.")]
    public string TenDangNhap { get; set; } = "";

    [Required(ErrorMessage = "Nhập họ và tên.")]
    [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
    public string HoTen { get; set; } = "";

    [Required(ErrorMessage = "Nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(100, ErrorMessage = "Email không được vượt quá 100 ký tự.")]
    public string Email { get; set; } = "";

    public string VaiTro { get; set; } = "NhanVien";

    public bool TrangThai { get; set; } = true;

    /// <summary>Bắt buộc khi thêm; khi sửa để trống = giữ mật khẩu cũ.</summary>
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu từ 6 đến 100 ký tự.")]
    public string? MatKhau { get; set; }
}
