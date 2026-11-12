using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Admin.Models;

// ==========================================================
// Khuyến mãi — api/v1/khuyen-mai (giảm % theo món, có thể giới hạn khung giờ vàng)
// ==========================================================

public class KhuyenMai
{
    public int MaKhuyenMai { get; set; }
    public string Code { get; set; } = "";
    public decimal PhanTramGiam { get; set; }
    public DateTime NgayBatDau { get; set; }
    public DateTime NgayKetThuc { get; set; }

    /// <summary>"HH:mm" hoặc null (cả ngày).</summary>
    public string? GioBatDau { get; set; }
    public string? GioKetThuc { get; set; }

    /// <summary>Đang có hiệu lực ngay lúc này (backend tính theo ngày + khung giờ).</summary>
    public bool DangApDung { get; set; }

    public int SoMonApDung { get; set; }
}

/// <summary>
/// POST / PUT api/v1/khuyen-mai. MaTenMons = null → giữ nguyên danh sách món áp dụng.
/// </summary>
public class KhuyenMaiRequest
{
    public string Code { get; set; } = "";
    public decimal? PhanTramGiam { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public string? GioBatDau { get; set; }
    public string? GioKetThuc { get; set; }
    public List<int>? MaTenMons { get; set; }
}

/// <summary>
/// Form thêm/sửa khuyến mãi (kiểm tra giống ràng buộc của backend).
/// </summary>
public class KhuyenMaiForm : IValidatableObject
{
    public int MaKhuyenMai { get; set; }

    [Required(ErrorMessage = "Nhập mã khuyến mãi.")]
    [RegularExpression("^[A-Za-z0-9_-]{3,20}$",
        ErrorMessage = "Mã 3–20 ký tự, chỉ gồm chữ không dấu, số, gạch ngang, gạch dưới.")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Nhập phần trăm giảm.")]
    [Range(0.01, 100, ErrorMessage = "Giảm giá từ 0,01 đến 100%.")]
    public decimal? PhanTramGiam { get; set; }

    [Required(ErrorMessage = "Chọn ngày bắt đầu.")]
    public DateTime? NgayBatDau { get; set; }

    [Required(ErrorMessage = "Chọn ngày kết thúc.")]
    public DateTime? NgayKetThuc { get; set; }

    /// <summary>Khung giờ vàng (tùy chọn) — nhập đủ cả hai hoặc bỏ trống cả hai.</summary>
    public TimeOnly? GioBatDau { get; set; }
    public TimeOnly? GioKetThuc { get; set; }

    public HashSet<int> MaTenMons { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Gửi lên backend: bắt đầu 00:00 ngày đầu, kết thúc 23:59:59 ngày cuối → cùng một ngày vẫn hợp lệ
        if (NgayBatDau.HasValue && NgayKetThuc.HasValue && NgayKetThuc.Value.Date < NgayBatDau.Value.Date)
        {
            yield return new ValidationResult(
                "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.", [nameof(NgayKetThuc)]);
        }

        if (GioBatDau.HasValue != GioKetThuc.HasValue)
        {
            yield return new ValidationResult(
                "Nhập đủ giờ bắt đầu và giờ kết thúc (hoặc bỏ trống cả hai).", [nameof(GioKetThuc)]);
        }
        else if (GioBatDau.HasValue && GioKetThuc.HasValue && GioKetThuc.Value <= GioBatDau.Value)
        {
            yield return new ValidationResult(
                "Giờ kết thúc phải sau giờ bắt đầu.", [nameof(GioKetThuc)]);
        }
    }
}
