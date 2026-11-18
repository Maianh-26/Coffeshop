namespace CoffeeShop.Admin.Models;

// ==========================================================
// Báo cáo — api/v1/bao-cao/* (chỉ QuanLy). Doanh thu = tổng đơn PAID.
// ==========================================================

public class BaoCaoDoanhThu
{
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public int TongSoDon { get; set; }
    public int SoDonDaThanhToan { get; set; }
    public int SoDonChoThanhToan { get; set; }
    public int SoDonHuy { get; set; }
    public int SoDonHoanTien { get; set; }
    public decimal DoanhThu { get; set; }
    public decimal GiaTriDonTrungBinh { get; set; }

    /// <summary>Doanh thu theo phương thức: "CASH" / "QR_TRANSFER" → tiền.</summary>
    public Dictionary<string, decimal> TheoPhuongThuc { get; set; } = [];

    public List<DoanhThuNgay> TheoNgay { get; set; } = [];
}

public class DoanhThuNgay
{
    public DateOnly Ngay { get; set; }
    public int SoDon { get; set; }
    public decimal DoanhThu { get; set; }
}

public class MonBanChay
{
    public int MaTenMon { get; set; }
    public string TenMon { get; set; } = "";
    public int SoLuong { get; set; }

    /// <summary>Tiền món (không gồm topping).</summary>
    public decimal DoanhThu { get; set; }

    /// <summary>Tiền topping gọi kèm món này.</summary>
    public decimal DoanhThuTopping { get; set; }

    /// <summary>DoanhThu + DoanhThuTopping (backend cũ không trả → 0).</summary>
    public decimal TongDoanhThu { get; set; }

    /// <summary>Doanh thu hiển thị: gồm topping; backend cũ chưa trả TongDoanhThu thì dùng tiền món.</summary>
    public decimal DoanhThuGomTopping => Math.Max(TongDoanhThu, DoanhThu);
}

/// <summary>GET api/v1/bao-cao/topping-ban-chay — chỉ tính đơn đã thanh toán.</summary>
public class ToppingBanChay
{
    public int MaTopping { get; set; }
    public string TenTopping { get; set; } = "";

    /// <summary>Tổng số ly có gọi topping này.</summary>
    public int SoLuong { get; set; }

    public decimal DoanhThu { get; set; }
}

public class GioCaoDiem
{
    public int Gio { get; set; }
    public int SoDon { get; set; }
    public decimal DoanhThu { get; set; }
}

/// <summary>
/// Một mốc trên biểu đồ / một dòng bảng báo cáo của trang Tổng quan (một giờ, một ngày hoặc một tháng).
/// Moc: nhãn ngắn dưới cột ("15h", "05/11", "11/2026"); MocDayDu: nhãn đầy đủ cho bảng, tooltip và file CSV.
/// MocPhu: dòng phụ dưới nhãn (ngày, ở cột 00:00 khi biểu đồ theo giờ qua nửa đêm) — mốc có dòng phụ luôn được ghi nhãn.
/// </summary>
public sealed record DongBaoCao(string Moc, string MocDayDu, int SoDon, decimal DoanhThu, string? MocPhu = null);

/// <summary>
/// Khoảng thời gian gửi lên api/v1/bao-cao/*: mốc chính xác tới từng giây.
/// From = null → không giới hạn đầu; To = null → đến hiện tại.
/// </summary>
public class DashboardFilter
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
