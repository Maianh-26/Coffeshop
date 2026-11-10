namespace CoffeeShop.Admin.Models;

// ==========================================================
// Đơn hàng — api/v1/don-hang
// Backend chỉ có trạng thái THANH TOÁN: PENDING → PAID, hoặc CANCELLED.
// (Chưa có bước "tiếp nhận / pha chế / hoàn thành" — đã đề xuất với nhóm backend.)
// ==========================================================

public class DonHang
{
    public int MaDonHang { get; set; }

    /// <summary>null = khách tự đặt trên tablet; có giá trị = nhân viên tạo tại quầy.</summary>
    public int? MaNguoiDung { get; set; }
    public string? TenNhanVien { get; set; }

    public decimal TongTien { get; set; }

    /// <summary>"CASH" | "QR_TRANSFER"</summary>
    public string PhuongThucThanhToan { get; set; } = PhuongThucThanhToans.Cash;

    /// <summary>"PENDING" | "PAID" | "CANCELLED"</summary>
    public string TrangThaiThanhToan { get; set; } = TrangThaiDon.Pending;

    public DateTime ThoiGianTao { get; set; }
    public string? LyDoHuy { get; set; }
    public bool DaHoanTien { get; set; }
    public DateTime? GioHoanTien { get; set; }
    public List<ChiTietDon>? ChiTiet { get; set; }

    /// <summary>Nguồn đơn suy ra từ người tạo.</summary>
    public string Nguon => MaNguoiDung is null ? "Tablet" : "Tại quầy";

    public int SoPhan => ChiTiet?.Sum(c => c.SoLuong) ?? 0;
}

public class ChiTietDon
{
    public int MaChiTietDon { get; set; }
    public int MaChiTietMon { get; set; }

    /// <summary>Tên món + size lưu cố định lúc đặt, ví dụ "Bạc xỉu - Lớn".</summary>
    public string TenMonLuu { get; set; } = "";

    /// <summary>Đơn giá đã áp khuyến mãi (chưa gồm topping).</summary>
    public decimal DonGiaBan { get; set; }

    public int SoLuong { get; set; }
    public string? GhiChu { get; set; }
    public List<ToppingDaChon> Toppings { get; set; } = [];

    /// <summary>(Đơn giá + topping) × số lượng.</summary>
    public decimal ThanhTien { get; set; }
}

public class ToppingDaChon
{
    public int MaTopping { get; set; }
    public string TenToppingLuu { get; set; } = "";
    public decimal DonGiaTopping { get; set; }
}

/// <summary>POST api/v1/don-hang</summary>
public class DatMonRequest
{
    public string? PhuongThucThanhToan { get; set; }
    public List<DongDatMonRequest> ChiTiet { get; set; } = [];
}

public class DongDatMonRequest
{
    public int? MaChiTietMon { get; set; }
    public int? SoLuong { get; set; }
    public string? GhiChu { get; set; }

    /// <summary>Mã topping gọi thêm (mỗi topping tối đa 1 phần/ly, tối đa 10).</summary>
    public List<int>? Toppings { get; set; }
}

/// <summary>POST api/v1/don-hang/{id}/thanh-toan</summary>
public class ThanhToanRequest
{
    public string PhuongThucThanhToan { get; set; } = PhuongThucThanhToans.Cash;

    /// <summary>Tiền khách đưa (tiền mặt) để tính tiền thừa.</summary>
    public decimal? TienKhachDua { get; set; }
}

public class ThanhToanResponse
{
    public DonHang DonHang { get; set; } = new();
    public decimal TienThua { get; set; }
    public List<CanhBaoKho> CanhBaoKho { get; set; } = [];
}

public class CanhBaoKho
{
    public int MaNguyenLieu { get; set; }
    public string TenNguyenLieu { get; set; } = "";
    public decimal SoLuongTon { get; set; }
    public decimal MucCanhBaoToiThieu { get; set; }
    public string DonViTinh { get; set; } = "";
}

/// <summary>POST api/v1/don-hang/{id}/huy (chỉ QuanLy)</summary>
public class HuyDonRequest
{
    public string LyDoHuy { get; set; } = "";
}

/// <summary>Chi tiết lỗi 422 HET_NGUYEN_LIEU khi đặt món.</summary>
public class ThieuNguyenLieu
{
    public string TenNguyenLieu { get; set; } = "";
    public decimal CanDung { get; set; }
    public decimal ConTon { get; set; }
    public string DonViTinh { get; set; } = "";
}

/// <summary>
/// Trạng thái đơn của backend và nhãn tiếng Việt — MỘT nơi duy nhất cho các chuỗi này.
/// </summary>
public static class TrangThaiDon
{
    public const string Pending = "PENDING";
    public const string Paid = "PAID";
    public const string Cancelled = "CANCELLED";

    public static string Label(string? status) => status switch
    {
        Pending => "Chờ thanh toán",
        Paid => "Đã thanh toán",
        Cancelled => "Đã hủy",
        _ => status ?? ""
    };

    public static string Color(string? status) => status switch
    {
        Pending => "orange",
        Paid => "green",
        Cancelled => "red",
        _ => "gray"
    };
}

public static class PhuongThucThanhToans
{
    public const string Cash = "CASH";
    public const string QrTransfer = "QR_TRANSFER";

    public static string Label(string? method) => method switch
    {
        Cash => "Tiền mặt",
        QrTransfer => "Chuyển khoản QR",
        _ => method ?? ""
    };
}
