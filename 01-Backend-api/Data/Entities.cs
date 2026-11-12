// Entity ánh xạ 1-1 với các bảng trong database/QuanCaPheDB.sql (giữ nguyên tên bảng & tên cột).
namespace CoffeeShop.Api.Data;

// ============================================================ KHỐI 1: NGƯỜI DÙNG
public class NguoiDung
{
    public int MaNguoiDung { get; set; }
    public string TenDangNhap { get; set; } = "";
    public string MatKhau { get; set; } = "";          // BCrypt hash
    public string HoTen { get; set; } = "";
    public string Email { get; set; } = "";
    public string VaiTro { get; set; } = VaiTros.NhanVien;
    public bool TrangThai { get; set; } = true;          // true: hoạt động, false: bị khóa
}

// ============================================================ KHỐI 2: MENU
public class DanhMuc
{
    public int MaDanhMuc { get; set; }
    public string TenDanhMuc { get; set; } = "";
    public List<Mon> Mons { get; set; } = new();
}

public class KhuyenMai
{
    public int MaKhuyenMai { get; set; }
    public string Code { get; set; } = "";
    public decimal PhanTramGiam { get; set; }
    public DateTime NgayBatDau { get; set; }
    public DateTime NgayKetThuc { get; set; }
    public TimeSpan? GioBatDau { get; set; }             // Khung giờ vàng (null = cả ngày)
    public TimeSpan? GioKetThuc { get; set; }

    /// <summary>Khuyến mãi có hiệu lực tại thời điểm <paramref name="now"/> không (ngày + khung giờ vàng).</summary>
    public bool DangApDung(DateTime now)
    {
        if (now < NgayBatDau || now > NgayKetThuc) return false;
        if (GioBatDau is { } from && GioKetThuc is { } to) return now.TimeOfDay >= from && now.TimeOfDay <= to;
        return true;
    }
}

/// <summary>Bảng TenMon (C# không cho class trùng tên thuộc tính nên đặt tên class là Mon).</summary>
public class Mon
{
    public int MaTenMon { get; set; }
    public int MaDanhMuc { get; set; }
    public int? MaKhuyenMai { get; set; }
    public string TenMon { get; set; } = "";
    public string? HinhAnh { get; set; }
    public bool ConHang { get; set; } = true;            // Tạm hết hàng trong ngày → false
    public bool NgungBan { get; set; }                   // Ngừng kinh doanh vĩnh viễn → true

    public DanhMuc? DanhMuc { get; set; }
    public KhuyenMai? KhuyenMai { get; set; }
    public List<ChiTietMon> ChiTietMons { get; set; } = new();
}

public class KichThuoc
{
    public string MaKichThuoc { get; set; } = "";        // S, M, L
    public string TenKichThuoc { get; set; } = "";
}

/// <summary>Một món ở một size cụ thể, có giá riêng và công thức riêng.</summary>
public class ChiTietMon
{
    public int MaChiTietMon { get; set; }
    public int MaTenMon { get; set; }
    public string MaKichThuoc { get; set; } = "";
    public decimal GiaBan { get; set; }
    public bool DangApDung { get; set; } = true;

    public Mon? Mon { get; set; }
    public KichThuoc? KichThuoc { get; set; }
    public List<CongThucMon> CongThucs { get; set; } = new();
}

public class Topping
{
    public int MaTopping { get; set; }
    public string TenTopping { get; set; } = "";
    public decimal GiaThem { get; set; }
    public bool ConHang { get; set; } = true;
    public bool NgungBan { get; set; }
    public List<CongThucMon> CongThucs { get; set; } = new();
}

// ============================================================ KHỐI 3: ĐƠN HÀNG
public class DonHang
{
    public int MaDonHang { get; set; }
    public int? MaNguoiDung { get; set; }                // null: khách tự order trên tablet
    public decimal TongTien { get; set; }
    public string PhuongThucThanhToan { get; set; } = "CASH";       // CASH | QR_TRANSFER
    public string TrangThaiThanhToan { get; set; } = TrangThais.Pending; // PENDING | PAID | CANCELLED
    public DateTime ThoiGianTao { get; set; }
    public string? LyDoHuy { get; set; }
    public bool DaHoanTien { get; set; }
    public DateTime? GioHoanTien { get; set; }

    public NguoiDung? NguoiDung { get; set; }
    public List<ChiTietDonHang> ChiTiets { get; set; } = new();
}

public class ChiTietDonHang
{
    public int MaChiTietDon { get; set; }
    public int MaDonHang { get; set; }
    public int MaChiTietMon { get; set; }
    public string TenMonLuu { get; set; } = "";          // Snapshot "Tên món - Size"
    public decimal DonGiaBan { get; set; }               // Snapshot đơn giá (đã trừ khuyến mãi)
    public int SoLuong { get; set; }
    public string? GhiChu { get; set; }

    public List<ChiTietDonHangTopping> Toppings { get; set; } = new();
}

public class ChiTietDonHangTopping
{
    public int MaChiTietTopping { get; set; }
    public int MaChiTietDon { get; set; }
    public int MaTopping { get; set; }
    public string TenToppingLuu { get; set; } = "";
    public decimal DonGiaTopping { get; set; }
}

// ============================================================ KHỐI 4: KHO
public class NguyenLieu
{
    public int MaNguyenLieu { get; set; }
    public string TenNguyenLieu { get; set; } = "";
    public string DonViTinh { get; set; } = "";
    public decimal SoLuongTon { get; set; }
    public decimal MucCanhBaoToiThieu { get; set; } = 10;
    public bool DangSuDung { get; set; } = true;
}

/// <summary>Định mức: gắn với ChiTietMon (món-size) HOẶC Topping, không đồng thời cả hai.</summary>
public class CongThucMon
{
    public int MaCongThuc { get; set; }
    public int? MaChiTietMon { get; set; }
    public int? MaTopping { get; set; }
    public int MaNguyenLieu { get; set; }
    public decimal LuongSuDung { get; set; }
    public NguyenLieu? NguyenLieu { get; set; }
}

public class LichSuKho
{
    public int MaLichSu { get; set; }
    public int MaNguyenLieu { get; set; }
    public int? MaDonHang { get; set; }
    public int? MaNguoiDung { get; set; }
    public string LoaiGiaoDich { get; set; } = "";       // NhapHang | XuatHuy | TieuHaoTheoDon
    public decimal SoLuongThayDoi { get; set; }          // (+) nhập, (-) tiêu hao / hủy
    public DateTime ThoiGian { get; set; }
    public string? GhiChu { get; set; }
    public NguyenLieu? NguyenLieu { get; set; }
}

// ============================================================ HẰNG SỐ
public static class VaiTros
{
    public const string QuanLy = "QuanLy";
    public const string NhanVien = "NhanVien";
    public const string TatCa = "QuanLy,NhanVien";
}

public static class TrangThais
{
    public const string Pending = "PENDING";
    public const string Paid = "PAID";
    public const string Cancelled = "CANCELLED";
}

public static class LoaiGiaoDichs
{
    public const string NhapHang = "NhapHang";
    public const string XuatHuy = "XuatHuy";
    public const string TieuHaoTheoDon = "TieuHaoTheoDon";
}
