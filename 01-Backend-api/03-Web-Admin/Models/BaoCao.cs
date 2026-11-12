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
    public decimal DoanhThu { get; set; }
}

public class GioCaoDiem
{
    public int Gio { get; set; }
    public int SoDon { get; set; }
    public decimal DoanhThu { get; set; }
}

/// <summary>Bộ lọc thời gian của trang Tổng quan.</summary>
public class DashboardFilter
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
