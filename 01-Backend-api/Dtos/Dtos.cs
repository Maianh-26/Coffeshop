using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

// DTO = hình dạng JSON vào/ra. Khớp 1-1 với components/schemas trong openapi/openapi.yaml.
// Tên trường = tên cột trong QuanCaPheDB ở dạng camelCase (MaTenMon → maTenMon).
// Kiểu "int?" + [Required] để phân biệt "không gửi" với "gửi 0".
namespace CoffeeShop.Api.Dtos;

// ================================================================== CHUNG
public record ErrorBody(ErrorInfo Error);
public record ErrorInfo(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Details = null);

public record PhanTrang(int Page, int Limit, int Total, int TotalPages)
{
    public static PhanTrang Of(int page, int limit, int total) => new(page, limit, total, (int)Math.Ceiling(total / (double)limit));
}
public record Paged<T>(IReadOnlyList<T> Data, PhanTrang Meta);

public class PageQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int Limit { get; set; } = 20;
    public int Skip => (Page - 1) * Limit;
}

public class KhoangThoiGian
{
    /// <summary>Từ thời điểm (giờ địa phương), ví dụ 2026-11-01T00:00:00</summary>
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
}

// ================================================================== AUTH & NGƯỜI DÙNG
public class DangNhapRequest
{
    [Required, StringLength(50, MinimumLength = 1)] public string TenDangNhap { get; set; } = "";
    [Required, MinLength(1)] public string MatKhau { get; set; } = "";
}

public record NguoiDungDto(int MaNguoiDung, string TenDangNhap, string HoTen, string Email, string VaiTro, bool TrangThai);
public record DangNhapResponse(string Token, DateTime HetHan, NguoiDungDto NguoiDung);

public class DoiMatKhauRequest
{
    [Required] public string MatKhauCu { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 6)] public string MatKhauMoi { get; set; } = "";
}

public class NguoiDungTaoRequest
{
    [Required, RegularExpression("^[a-zA-Z0-9_.]{3,50}$")] public string TenDangNhap { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 6)] public string MatKhau { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 1)] public string HoTen { get; set; } = "";
    [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = "";
    [Required, AllowedValues("QuanLy", "NhanVien")] public string VaiTro { get; set; } = "NhanVien";
}

public class NguoiDungCapNhatRequest
{
    [StringLength(100, MinimumLength = 1)] public string? HoTen { get; set; }
    [EmailAddress, StringLength(100)] public string? Email { get; set; }
    [AllowedValues("QuanLy", "NhanVien", null)] public string? VaiTro { get; set; }
    /// <summary>false = khóa tài khoản</summary>
    public bool? TrangThai { get; set; }
    /// <summary>Quản lý đặt lại mật khẩu cho nhân viên</summary>
    [StringLength(100, MinimumLength = 6)] public string? MatKhauMoi { get; set; }
}

// ================================================================== DANH MỤC, KÍCH THƯỚC
public record DanhMucDto(int MaDanhMuc, string TenDanhMuc, int SoMon);

public class DanhMucRequest
{
    [Required, StringLength(100, MinimumLength = 1)] public string TenDanhMuc { get; set; } = "";
}

public record KichThuocDto(string MaKichThuoc, string TenKichThuoc);

public class KichThuocRequest
{
    [Required, RegularExpression("^[A-Za-z0-9]{1,10}$")] public string MaKichThuoc { get; set; } = "";
    [Required, StringLength(50, MinimumLength = 1)] public string TenKichThuoc { get; set; } = "";
}

// ================================================================== KHUYẾN MÃI
public record KhuyenMaiDto(
    int MaKhuyenMai, string Code, decimal PhanTramGiam, DateTime NgayBatDau, DateTime NgayKetThuc,
    string? GioBatDau, string? GioKetThuc, bool DangApDung, int SoMonApDung);

public class KhuyenMaiRequest
{
    [Required, RegularExpression("^[A-Za-z0-9_-]{3,20}$")] public string Code { get; set; } = "";
    [Required, Range(0.01, 100)] public decimal? PhanTramGiam { get; set; }
    [Required] public DateTime? NgayBatDau { get; set; }
    [Required] public DateTime? NgayKetThuc { get; set; }
    /// <summary>Giờ vàng bắt đầu, dạng "HH:mm" (bỏ trống = cả ngày)</summary>
    [RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$")] public string? GioBatDau { get; set; }
    [RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$")] public string? GioKetThuc { get; set; }
    /// <summary>Danh sách món áp dụng khuyến mãi này (ghi vào TenMon.MaKhuyenMai)</summary>
    public List<int>? MaTenMons { get; set; }
}

// ================================================================== MÓN
public record KhuyenMaiNgan(int MaKhuyenMai, string Code, decimal PhanTramGiam, bool DangApDung, string? GioBatDau, string? GioKetThuc);

public record GiaTheoSizeDto(
    int MaChiTietMon, string MaKichThuoc, string TenKichThuoc, decimal GiaBan, decimal GiaSauGiam,
    bool DangApDung, bool DuNguyenLieu);

public record MonDto(
    int MaTenMon, int MaDanhMuc, string TenDanhMuc, string TenMon, string? HinhAnh,
    bool ConHang, bool NgungBan,
    /// Tính tự động: đang bán + còn hàng + có ít nhất 1 size đủ nguyên liệu
    bool CoTheBan,
    KhuyenMaiNgan? KhuyenMai, IReadOnlyList<GiaTheoSizeDto> KichThuoc);

public class MonQuery : PageQuery
{
    [Range(1, int.MaxValue)] public int? MaDanhMuc { get; set; }
    [StringLength(100)] public string? TuKhoa { get; set; }
    /// <summary>true = chỉ lấy món đang bán được</summary>
    public bool? CoTheBan { get; set; }
    public bool? DangKhuyenMai { get; set; }
    /// <summary>Quản lý xem cả món đã ngừng bán</summary>
    public bool BaoGomNgungBan { get; set; }
    [AllowedValues("ten", "gia_tang", "gia_giam", "ban_chay")] public string SapXep { get; set; } = "ten";
}

public class GiaSizeRequest
{
    [Required, StringLength(10, MinimumLength = 1)] public string MaKichThuoc { get; set; } = "";
    [Required, Range(0, 1_000_000_000)] public decimal? GiaBan { get; set; }
    public bool DangApDung { get; set; } = true;
}

public class MonTaoRequest
{
    [Required, Range(1, int.MaxValue)] public int? MaDanhMuc { get; set; }
    [Required, StringLength(150, MinimumLength = 1)] public string TenMon { get; set; } = "";
    [StringLength(255)] public string? HinhAnh { get; set; }
    [Range(1, int.MaxValue)] public int? MaKhuyenMai { get; set; }
    [Required, MinLength(1), MaxLength(10)] public List<GiaSizeRequest> KichThuoc { get; set; } = new();
}

/// <summary>PATCH: trường nào không gửi thì giữ nguyên.</summary>
public class MonCapNhatRequest
{
    [Range(1, int.MaxValue)] public int? MaDanhMuc { get; set; }
    [StringLength(150, MinimumLength = 1)] public string? TenMon { get; set; }
    [StringLength(255)] public string? HinhAnh { get; set; }
    /// <summary>Gắn khuyến mãi; gửi 0 để gỡ khuyến mãi</summary>
    [Range(0, int.MaxValue)] public int? MaKhuyenMai { get; set; }
    public bool? NgungBan { get; set; }
}

public class ConHangRequest
{
    [Required] public bool? ConHang { get; set; }
}

// ================================================================== TOPPING
public record ToppingDto(int MaTopping, string TenTopping, decimal GiaThem, bool ConHang, bool NgungBan, bool DuNguyenLieu);

public class ToppingRequest
{
    [Required, StringLength(100, MinimumLength = 1)] public string TenTopping { get; set; } = "";
    [Required, Range(0, 1_000_000_000)] public decimal? GiaThem { get; set; }
    public bool ConHang { get; set; } = true;
    public bool NgungBan { get; set; }
}

// ================================================================== CÔNG THỨC
public class CongThucDongRequest
{
    [Required, Range(1, int.MaxValue)] public int? MaNguyenLieu { get; set; }
    [Required, Range(0.01, 99_999_999)] public decimal? LuongSuDung { get; set; }
}
public record CongThucDongDto(int MaCongThuc, int MaNguyenLieu, string TenNguyenLieu, string DonViTinh, decimal LuongSuDung);

// ================================================================== KHO
public record NguyenLieuDto(
    int MaNguyenLieu, string TenNguyenLieu, string DonViTinh, decimal SoLuongTon, decimal MucCanhBaoToiThieu,
    bool DangSuDung, bool SapHet,
    /// Trung bình tiêu hao/ngày trong 7 ngày gần nhất (từ LichSuKho)
    decimal TieuHaoTrungBinhNgay,
    /// Dự báo số ngày còn dùng được (null nếu chưa có dữ liệu tiêu hao)
    decimal? SoNgayConLai);

public class NguyenLieuRequest
{
    [Required, StringLength(100, MinimumLength = 1)] public string TenNguyenLieu { get; set; } = "";
    [Required, StringLength(20, MinimumLength = 1)] public string DonViTinh { get; set; } = "";
    [Range(0, 9_999_999_999)] public decimal SoLuongTon { get; set; }
    [Range(0, 9_999_999_999)] public decimal MucCanhBaoToiThieu { get; set; } = 10;
    public bool DangSuDung { get; set; } = true;
}

public class NguyenLieuCapNhatRequest
{
    [StringLength(100, MinimumLength = 1)] public string? TenNguyenLieu { get; set; }
    [StringLength(20, MinimumLength = 1)] public string? DonViTinh { get; set; }
    [Range(0, 9_999_999_999)] public decimal? MucCanhBaoToiThieu { get; set; }
    public bool? DangSuDung { get; set; }
}

public class GiaoDichKhoRequest
{
    [Required, Range(0.01, 9_999_999_999)] public decimal? SoLuong { get; set; }
    [StringLength(255)] public string? GhiChu { get; set; }
}

public record LichSuKhoDto(
    int MaLichSu, int MaNguyenLieu, string TenNguyenLieu, int? MaDonHang, int? MaNguoiDung,
    string LoaiGiaoDich, decimal SoLuongThayDoi, DateTime ThoiGian, string? GhiChu);

public class LichSuKhoQuery : PageQuery
{
    [Range(1, int.MaxValue)] public int? MaNguyenLieu { get; set; }
    [AllowedValues("NhapHang", "XuatHuy", "TieuHaoTheoDon", null)] public string? LoaiGiaoDich { get; set; }
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
}

// ================================================================== ĐƠN HÀNG
public class DongDatMonRequest
{
    [Required, Range(1, int.MaxValue)] public int? MaChiTietMon { get; set; }
    [Required, Range(1, 50)] public int? SoLuong { get; set; }
    [StringLength(255)] public string? GhiChu { get; set; }
    /// <summary>Mã topping gọi thêm (mỗi topping tối đa 1 phần/ly)</summary>
    [MaxLength(10)] public List<int>? Toppings { get; set; }
}

public class DatMonRequest
{
    [AllowedValues("CASH", "QR_TRANSFER", null)] public string? PhuongThucThanhToan { get; set; }
    [Required, MinLength(1), MaxLength(50)] public List<DongDatMonRequest> ChiTiet { get; set; } = new();
}

public record ToppingDaChonDto(int MaTopping, string TenToppingLuu, decimal DonGiaTopping);

public record ChiTietDonDto(
    int MaChiTietDon, int MaChiTietMon, string TenMonLuu, decimal DonGiaBan, int SoLuong, string? GhiChu,
    IReadOnlyList<ToppingDaChonDto> Toppings, decimal ThanhTien);

public record DonHangDto(
    int MaDonHang, int? MaNguoiDung, string? TenNhanVien, decimal TongTien,
    string PhuongThucThanhToan, string TrangThaiThanhToan, DateTime ThoiGianTao,
    string? LyDoHuy, bool DaHoanTien, DateTime? GioHoanTien,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ChiTietDonDto>? ChiTiet);

public class DonHangQuery : PageQuery
{
    [AllowedValues("PENDING", "PAID", "CANCELLED", null)] public string? TrangThai { get; set; }
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
}

public class ThanhToanRequest
{
    [Required, AllowedValues("CASH", "QR_TRANSFER")] public string PhuongThucThanhToan { get; set; } = "CASH";
    /// <summary>Tiền khách đưa (tiền mặt) để tính tiền thừa</summary>
    [Range(0, 1_000_000_000)] public decimal? TienKhachDua { get; set; }
}

public record CanhBaoKhoDto(int MaNguyenLieu, string TenNguyenLieu, decimal SoLuongTon, decimal MucCanhBaoToiThieu, string DonViTinh);
public record ThanhToanResponse(DonHangDto DonHang, decimal TienThua, IReadOnlyList<CanhBaoKhoDto> CanhBaoKho);

public class HuyDonRequest
{
    [Required, StringLength(255, MinimumLength = 1)] public string LyDoHuy { get; set; } = "";
}

// ================================================================== BÁO CÁO
public record DoanhThuNgayDto(DateOnly Ngay, int SoDon, decimal DoanhThu);
public record BaoCaoDoanhThuDto(
    DateTime? TuNgay, DateTime? DenNgay, int TongSoDon, int SoDonDaThanhToan, int SoDonChoThanhToan, int SoDonHuy,
    int SoDonHoanTien, decimal DoanhThu, decimal GiaTriDonTrungBinh,
    Dictionary<string, decimal> TheoPhuongThuc, IReadOnlyList<DoanhThuNgayDto> TheoNgay);

public record MonBanChayDto(int MaTenMon, string TenMon, int SoLuong, decimal DoanhThu);
public record GioCaoDiemDto(int Gio, int SoDon, decimal DoanhThu);
