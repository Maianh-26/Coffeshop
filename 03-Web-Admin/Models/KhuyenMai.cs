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
/// Trạng thái hiển thị của khuyến mãi (một chỗ duy nhất cho chữ và màu), tính theo khoảng ngày:
/// Chưa bắt đầu → Đang áp dụng (trong khoảng ngày) → Đã kết thúc.
/// Trước đây có thêm "Ngoài khung giờ" (trong khoảng ngày nhưng lúc này ngoài giờ vàng) — đã bỏ (chủ quán 25/11/2026):
/// đó chỉ là trạng thái tạm thời đổi qua lại nhiều lần trong ngày, khuyến mãi vẫn còn hiệu lực và tự áp lại khi tới giờ vàng,
/// nên lọc theo nó không có ích và dễ nhầm với "Đã kết thúc". Lúc ngoài giờ vàng chỉ ghi chú nhỏ ở cột Giờ vàng (<see cref="OutsideGoldenHours"/>).
/// </summary>
public static class TrangThaiKhuyenMai
{
    public const string DangApDung = "Đang áp dụng";
    public const string ChuaBatDau = "Chưa bắt đầu";
    public const string DaKetThuc = "Đã kết thúc";

    public static readonly string[] Options = [DangApDung, ChuaBatDau, DaKetThuc];

    // Backend tính DangApDung bằng giờ máy chủ: nếu nó báo đang hiệu lực thì tin nó (lệch giờ máy khách không làm sai)
    public static string Of(KhuyenMai p, DateTime now) =>
        p.DangApDung ? DangApDung
        : now > p.NgayKetThuc ? DaKetThuc
        : now < p.NgayBatDau ? ChuaBatDau
        : DangApDung;

    /// <summary>Trong khoảng ngày nhưng lúc này ngoài khung giờ vàng (giảm giá sẽ tự áp lại khi tới giờ).</summary>
    public static bool OutsideGoldenHours(KhuyenMai p, DateTime now) =>
        !p.DangApDung && p.GioBatDau is not null && Of(p, now) == DangApDung;

    public static string Color(string status) => status switch
    {
        DangApDung => "green",
        DaKetThuc => "gray",
        _ => "yellow"
    };
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
