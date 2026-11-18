using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Admin.Models;

// ==========================================================
// Kho nguyên liệu — api/v1/nguyen-lieu, lich-su-kho (chỉ QuanLy)
// ==========================================================

public class NguyenLieu
{
    public int MaNguyenLieu { get; set; }
    public string TenNguyenLieu { get; set; } = "";
    public string DonViTinh { get; set; } = "";
    public decimal SoLuongTon { get; set; }
    public decimal MucCanhBaoToiThieu { get; set; }

    /// <summary>false = đã ngừng sử dụng (xóa mềm).</summary>
    public bool DangSuDung { get; set; } = true;

    // Ba trường dự báo dưới đây backend vẫn trả về (giữ để khớp DTO) nhưng giao diện KHÔNG dùng nữa:
    // dự báo theo trung bình 7 ngày không đáng tin khi quán đông bất thường, và quy tắc "còn ≤ 2 ngày" bị ẩn.
    // Tình trạng hiển thị dùng một quy tắc rõ ràng: TinhTrangKho.Of (tồn so với mức cảnh báo).

    /// <summary>[Không dùng trên giao diện] Backend: tồn ≤ mức cảnh báo hoặc dự báo còn ≤ 2 ngày.</summary>
    public bool SapHet { get; set; }

    /// <summary>[Không dùng trên giao diện] Tiêu hao trung bình/ngày trong 7 ngày gần nhất.</summary>
    public decimal TieuHaoTrungBinhNgay { get; set; }

    /// <summary>[Không dùng trên giao diện] Dự báo số ngày còn dùng được.</summary>
    public decimal? SoNgayConLai { get; set; }

    /// <summary>Tình trạng hiển thị, xem <see cref="TinhTrangKho"/>.</summary>
    public string TinhTrang => TinhTrangKho.Of(this);
}

/// <summary>
/// Tình trạng tồn kho hiển thị — MỘT nơi duy nhất cho quy tắc và chuỗi.
/// Quy tắc (giống các phần mềm bán hàng phổ biến: ngưỡng cố định do chủ quán đặt):
/// ngừng sử dụng → "Ngừng sử dụng"; tồn ≤ 0 → "Hết hàng"; tồn ≤ mức cảnh báo → "Sắp hết"; còn lại → "Đủ dùng".
/// </summary>
public static class TinhTrangKho
{
    public const string DuDung = "DU_DUNG";
    public const string SapHet = "SAP_HET";
    public const string HetHang = "HET_HANG";
    public const string NgungSuDung = "NGUNG_SU_DUNG";

    /// <summary>Giá trị bộ lọc gộp: sắp hết + hết hàng (nút "Xem" trên cảnh báo cần nhập thêm).</summary>
    public const string CanNhapFilter = "CAN_NHAP";

    /// <summary>Các lựa chọn của bộ lọc tình trạng (nguyên liệu đang sử dụng).</summary>
    public static readonly string[] FilterOptions = [DuDung, CanNhapFilter, SapHet, HetHang];

    /// <summary>Nguyên liệu có khớp lựa chọn của bộ lọc tình trạng không ("" = tất cả).</summary>
    public static bool Matches(NguyenLieu n, string filter) =>
        filter == "" || (filter == CanNhapFilter ? CanNhap(n) : Of(n) == filter);

    public static string Of(NguyenLieu n) =>
        !n.DangSuDung ? NgungSuDung
        : n.SoLuongTon <= 0 ? HetHang
        : n.SoLuongTon <= n.MucCanhBaoToiThieu ? SapHet
        : DuDung;

    /// <summary>Cần nhập thêm: sắp hết hoặc đã hết (chỉ tính nguyên liệu đang sử dụng).</summary>
    public static bool CanNhap(NguyenLieu n) => Of(n) is SapHet or HetHang;

    public static string Label(string tinhTrang) => tinhTrang switch
    {
        DuDung => "Đủ dùng",
        SapHet => "Sắp hết",
        HetHang => "Hết hàng",
        NgungSuDung => "Ngừng sử dụng",
        CanNhapFilter => "Cần nhập thêm",   // = Sắp hết + Hết hàng
        _ => tinhTrang
    };

    /// <summary>Lớp màu huy hiệu: xanh lá đủ dùng, vàng sắp hết, đỏ hết hàng, xám ngừng sử dụng.</summary>
    public static string Color(string tinhTrang) => tinhTrang switch
    {
        DuDung => "green",
        SapHet => "yellow",
        HetHang => "red",
        _ => "gray"
    };
}

/// <summary>
/// Bộ lọc trạng thái sử dụng của nguyên liệu — chuỗi dùng trong trang Kho.
/// Mặc định hiện tất cả (kể cả ngừng sử dụng) để kiểm tra dữ liệu dễ hơn — giống trang Món.
/// </summary>
public static class LocSuDung
{
    public const string TatCa = "";               // mặc định
    public const string DangSuDung = "active";
    public const string NgungSuDung = "stopped";

    /// <summary>Lựa chọn của ô lọc (sau ô "Tất cả trạng thái").</summary>
    public static readonly string[] Options = [DangSuDung, NgungSuDung];

    public static string Label(string loc) => loc switch
    {
        DangSuDung => "Đang sử dụng",
        NgungSuDung => "Ngừng sử dụng",
        _ => "Tất cả trạng thái"
    };
}

/// <summary>POST api/v1/nguyen-lieu</summary>
public class NguyenLieuRequest
{
    public string TenNguyenLieu { get; set; } = "";
    public string DonViTinh { get; set; } = "";
    public decimal SoLuongTon { get; set; }
    public decimal MucCanhBaoToiThieu { get; set; } = 10;
    public bool DangSuDung { get; set; } = true;
}

/// <summary>PATCH api/v1/nguyen-lieu/{id}: không sửa trực tiếp tồn kho (dùng nhập kho / xuất hủy).</summary>
public class NguyenLieuCapNhatRequest
{
    public string? TenNguyenLieu { get; set; }
    public string? DonViTinh { get; set; }
    public decimal? MucCanhBaoToiThieu { get; set; }
    public bool? DangSuDung { get; set; }
}

/// <summary>POST api/v1/nguyen-lieu/{id}/nhap-kho và /xuat-huy</summary>
public class GiaoDichKhoRequest
{
    public decimal? SoLuong { get; set; }
    public string? GhiChu { get; set; }
}

public class LichSuKho
{
    public int MaLichSu { get; set; }
    public int MaNguyenLieu { get; set; }
    public string TenNguyenLieu { get; set; } = "";
    public int? MaDonHang { get; set; }
    public int? MaNguoiDung { get; set; }

    /// <summary>"NhapHang" | "XuatHuy" | "TieuHaoTheoDon"</summary>
    public string LoaiGiaoDich { get; set; } = "";

    /// <summary>Dương khi nhập, âm khi xuất / tiêu hao.</summary>
    public decimal SoLuongThayDoi { get; set; }

    public DateTime ThoiGian { get; set; }
    public string? GhiChu { get; set; }
}

public static class LoaiGiaoDichKho
{
    public const string NhapHang = "NhapHang";
    public const string XuatHuy = "XuatHuy";
    public const string TieuHaoTheoDon = "TieuHaoTheoDon";

    public static string Label(string? loai) => loai switch
    {
        NhapHang => "Nhập hàng",
        XuatHuy => "Xuất hủy",
        TieuHaoTheoDon => "Tiêu hao theo đơn",
        _ => loai ?? ""
    };

    public static string Color(string? loai) => loai switch
    {
        NhapHang => "green",
        XuatHuy => "red",
        _ => "yellow"
    };
}

/// <summary>Form thêm/sửa nguyên liệu.</summary>
public class NguyenLieuForm
{
    public int MaNguyenLieu { get; set; }

    [Required(ErrorMessage = "Nhập tên nguyên liệu.")]
    [StringLength(100, ErrorMessage = "Tên không được vượt quá 100 ký tự.")]
    public string TenNguyenLieu { get; set; } = "";

    [Required(ErrorMessage = "Nhập đơn vị tính.")]
    [StringLength(20, ErrorMessage = "Đơn vị không được vượt quá 20 ký tự.")]
    public string DonViTinh { get; set; } = "";

    /// <summary>Chỉ dùng khi thêm mới (tồn kho ban đầu).</summary>
    [Range(0, 9_999_999_999, ErrorMessage = "Tồn kho ban đầu không được âm.")]
    public decimal SoLuongTon { get; set; }

    [Range(0, 9_999_999_999, ErrorMessage = "Mức cảnh báo không được âm.")]
    public decimal MucCanhBaoToiThieu { get; set; } = 10;

    public bool DangSuDung { get; set; } = true;
}

/// <summary>Form tạo phiếu nhập kho / xuất hủy.</summary>
public class GiaoDichKhoForm
{
    /// <summary>LoaiGiaoDichKho.NhapHang hoặc XuatHuy.</summary>
    public string Loai { get; set; } = LoaiGiaoDichKho.NhapHang;

    [Range(1, int.MaxValue, ErrorMessage = "Chọn nguyên liệu.")]
    public int MaNguyenLieu { get; set; }

    [Required(ErrorMessage = "Nhập số lượng.")]
    [Range(0.01, 9_999_999_999, ErrorMessage = "Số lượng phải lớn hơn 0.")]
    public decimal? SoLuong { get; set; }

    [StringLength(255, ErrorMessage = "Ghi chú không được vượt quá 255 ký tự.")]
    public string? GhiChu { get; set; }
}
