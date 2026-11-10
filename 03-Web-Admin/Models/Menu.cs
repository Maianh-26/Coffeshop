namespace CoffeeShop.Admin.Models;

// ==========================================================
// Thực đơn — api/v1/danh-muc, kich-thuoc, mon, topping, chi-tiet-mon/{id}/cong-thuc
// ==========================================================

public class DanhMuc
{
    public int MaDanhMuc { get; set; }
    public string TenDanhMuc { get; set; } = "";

    /// <summary>Số món đang bán thuộc danh mục.</summary>
    public int SoMon { get; set; }
}

public class DanhMucRequest
{
    public string TenDanhMuc { get; set; } = "";
}

/// <summary>Kích thước ly (S/M/L…). Mã là khóa chính dạng chuỗi.</summary>
public class KichThuoc
{
    public string MaKichThuoc { get; set; } = "";
    public string TenKichThuoc { get; set; } = "";
}

public class KichThuocRequest
{
    public string MaKichThuoc { get; set; } = "";
    public string TenKichThuoc { get; set; } = "";
}

/// <summary>Món (MonDto) kèm giá theo từng size.</summary>
public class Mon
{
    public int MaTenMon { get; set; }
    public int MaDanhMuc { get; set; }
    public string TenDanhMuc { get; set; } = "";
    public string TenMon { get; set; } = "";
    public string? HinhAnh { get; set; }

    /// <summary>Nhân viên báo tạm hết trong ngày = false.</summary>
    public bool ConHang { get; set; } = true;

    /// <summary>Xóa mềm / ngừng kinh doanh.</summary>
    public bool NgungBan { get; set; }

    /// <summary>Backend tự tính: đang bán + còn hàng + có ít nhất 1 size đủ nguyên liệu.</summary>
    public bool CoTheBan { get; set; }

    public KhuyenMaiNgan? KhuyenMai { get; set; }
    public List<GiaTheoSize> KichThuoc { get; set; } = [];
}

public class GiaTheoSize
{
    /// <summary>Mã "món theo size" — dùng khi đặt món và khi lưu công thức.</summary>
    public int MaChiTietMon { get; set; }
    public string MaKichThuoc { get; set; } = "";
    public string TenKichThuoc { get; set; } = "";
    public decimal GiaBan { get; set; }
    public decimal GiaSauGiam { get; set; }
    public bool DangApDung { get; set; } = true;
    public bool DuNguyenLieu { get; set; } = true;
}

public class KhuyenMaiNgan
{
    public int MaKhuyenMai { get; set; }
    public string Code { get; set; } = "";
    public decimal PhanTramGiam { get; set; }
    public bool DangApDung { get; set; }
    public string? GioBatDau { get; set; }
    public string? GioKetThuc { get; set; }
}

public class GiaSizeRequest
{
    public string MaKichThuoc { get; set; } = "";
    public decimal? GiaBan { get; set; }
    public bool DangApDung { get; set; } = true;
}

/// <summary>POST api/v1/mon</summary>
public class MonTaoRequest
{
    public int? MaDanhMuc { get; set; }
    public string TenMon { get; set; } = "";
    public string? HinhAnh { get; set; }
    public int? MaKhuyenMai { get; set; }
    public List<GiaSizeRequest> KichThuoc { get; set; } = [];
}

/// <summary>PATCH api/v1/mon/{id}: trường null = giữ nguyên; MaKhuyenMai = 0 để gỡ khuyến mãi.</summary>
public class MonCapNhatRequest
{
    public int? MaDanhMuc { get; set; }
    public string? TenMon { get; set; }
    public string? HinhAnh { get; set; }
    public int? MaKhuyenMai { get; set; }
    public bool? NgungBan { get; set; }
}

/// <summary>PATCH api/v1/mon/{id}/con-hang và topping/{id}/con-hang</summary>
public class ConHangRequest
{
    public bool? ConHang { get; set; }
}

public class Topping
{
    public int MaTopping { get; set; }
    public string TenTopping { get; set; } = "";
    public decimal GiaThem { get; set; }
    public bool ConHang { get; set; } = true;
    public bool NgungBan { get; set; }
    public bool DuNguyenLieu { get; set; } = true;
}

public class ToppingRequest
{
    public string TenTopping { get; set; } = "";
    public decimal? GiaThem { get; set; }
    public bool ConHang { get; set; } = true;
    public bool NgungBan { get; set; }
}

/// <summary>Một dòng định mức nguyên liệu (của món-size hoặc topping).</summary>
public class CongThucDong
{
    public int MaCongThuc { get; set; }
    public int MaNguyenLieu { get; set; }
    public string TenNguyenLieu { get; set; } = "";
    public string DonViTinh { get; set; } = "";
    public decimal LuongSuDung { get; set; }
}

public class CongThucDongRequest
{
    public int? MaNguyenLieu { get; set; }
    public decimal? LuongSuDung { get; set; }
}

/// <summary>
/// Trạng thái hiển thị của món / topping (tính từ các cờ của backend).
/// </summary>
public static class TrangThaiMon
{
    public const string DangBan = "Đang bán";
    public const string TamHet = "Tạm hết";
    public const string HetNguyenLieu = "Hết nguyên liệu";
    public const string NgungBan = "Ngừng bán";

    public static string Of(Mon m) =>
        m.NgungBan ? NgungBan
        : !m.ConHang ? TamHet
        : !m.CoTheBan ? HetNguyenLieu
        : DangBan;

    public static string Of(Topping t) =>
        t.NgungBan ? NgungBan
        : !t.ConHang ? TamHet
        : !t.DuNguyenLieu ? HetNguyenLieu
        : DangBan;

    public static string Color(string status) => status switch
    {
        DangBan => "green",
        TamHet or HetNguyenLieu => "yellow",
        _ => "red"
    };
}
