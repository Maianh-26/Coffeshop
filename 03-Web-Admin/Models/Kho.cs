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

    /// <summary>Tồn ≤ mức cảnh báo hoặc dự báo còn ≤ 2 ngày.</summary>
    public bool SapHet { get; set; }

    /// <summary>Tiêu hao trung bình/ngày trong 7 ngày gần nhất.</summary>
    public decimal TieuHaoTrungBinhNgay { get; set; }

    /// <summary>Dự báo số ngày còn dùng được (null nếu chưa có dữ liệu tiêu hao).</summary>
    public decimal? SoNgayConLai { get; set; }
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
