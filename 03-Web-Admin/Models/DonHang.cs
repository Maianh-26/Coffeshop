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

    /// <summary>
    /// [ĐỀ XUẤT — backend chưa có] Số trên bảng gỗ nhân viên đưa cho khách (1–50). null = chưa nhập.
    /// Số bị khóa khi đơn chưa phục vụ xong; giải phóng khi đơn "Đã phục vụ" hoặc bị hủy (đơn vẫn giữ số để tra cứu).
    /// </summary>
    public int? SoNhanMon { get; set; }

    /// <summary>
    /// [ĐỀ XUẤT — backend chưa có] "WAITING" | "PREPARING" | "SERVED". null (backend cũ) = WAITING.
    /// </summary>
    public string? TrangThaiPhucVu { get; set; }

    /// <summary>Nguồn đơn suy ra từ người tạo.</summary>
    public string Nguon => MaNguoiDung is null ? "Tablet" : "Tại quầy";

    public int SoPhan => ChiTiet?.Sum(c => c.SoLuong) ?? 0;

    public bool DaHuy => TrangThaiThanhToan == TrangThaiDon.Cancelled;

    /// <summary>Trạng thái phục vụ thực tế (backend cũ không trả trường này → coi là chờ phục vụ).</summary>
    public string PhucVu => TrangThaiPhucVu ?? TrangThaiPhucVus.Waiting;

    /// <summary>Đơn còn đang phục vụ (chưa xong, chưa hủy) → số nhận món đang bị khóa.</summary>
    public bool DangPhucVu => !DaHuy && PhucVu != TrangThaiPhucVus.Served;

    /// <summary>Chỉ sửa món khi đơn chưa thanh toán, chưa hủy và chưa phục vụ.</summary>
    public bool CoTheSua => TrangThaiThanhToan == TrangThaiDon.Pending && PhucVu != TrangThaiPhucVus.Served;
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

/// <summary>[ĐỀ XUẤT] PUT api/v1/don-hang/{id}/so-nhan-mon — null = bỏ số.</summary>
public class SoNhanMonRequest
{
    public int? SoNhanMon { get; set; }
}

/// <summary>[ĐỀ XUẤT] PUT api/v1/don-hang/{id}/trang-thai-phuc-vu</summary>
public class TrangThaiPhucVuRequest
{
    public string TrangThaiPhucVu { get; set; } = "";
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
        Pending => "Chưa thu",
        Paid => "Đã thu",
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
        QrTransfer => "Chuyển khoản",
        _ => method ?? ""
    };

    /// <summary>Nhãn ngắn trên bảng: Chưa thu / Đã thu / Đã hoàn tiền / Không thu.</summary>
    public static string ShortLabel(DonHang d) => d.TrangThaiThanhToan switch
    {
        TrangThaiDon.Cancelled when d.DaHoanTien => "Đã hoàn tiền",
        TrangThaiDon.Cancelled => "Không thu",
        _ => TrangThaiDon.Label(d.TrangThaiThanhToan)
    };

    /// <summary>Sắc thái (class CSS <c>status-*</c>): due = vàng, paid = xanh lá, refund = đỏ, void = xám.</summary>
    public static string Tone(DonHang d) => d.TrangThaiThanhToan switch
    {
        TrangThaiDon.Paid => "paid",
        TrangThaiDon.Cancelled when d.DaHoanTien => "refund",
        TrangThaiDon.Cancelled => "void",
        _ => "due"
    };

    /// <summary>Tình trạng tiền của đơn bằng một câu ngắn, ví dụ "Đã thu tiền mặt", "Chưa thu (chuyển khoản)".</summary>
    public static string Summary(DonHang d) => d.TrangThaiThanhToan switch
    {
        TrangThaiDon.Paid when d.PhuongThucThanhToan == QrTransfer => "Đã nhận chuyển khoản",
        TrangThaiDon.Paid => "Đã thu tiền mặt",
        TrangThaiDon.Cancelled when !d.DaHoanTien => ShortLabel(d),
        _ => $"{ShortLabel(d)} ({Label(d.PhuongThucThanhToan).ToLowerInvariant()})"
    };
}

/// <summary>
/// [ĐỀ XUẤT — backend chưa có] Trạng thái phục vụ món — MỘT nơi duy nhất cho các chuỗi này.
/// "Đã hủy" lấy từ trạng thái thanh toán CANCELLED.
/// </summary>
public static class TrangThaiPhucVus
{
    public const string Waiting = "WAITING";
    public const string Preparing = "PREPARING";
    public const string Served = "SERVED";

    /// <summary>Giá trị lọc "Đã hủy" trên giao diện (gửi đi dưới dạng trangThai=CANCELLED).</summary>
    public const string CancelledFilter = "CANCELLED";

    public static string Label(string? status) => status switch
    {
        Waiting => "Chờ phục vụ",
        Preparing => "Đang chuẩn bị",
        Served => "Đã phục vụ",
        CancelledFilter => "Đã hủy",
        _ => status ?? ""
    };

    public static string Label(DonHang d) => d.DaHuy ? "Đã hủy" : Label(d.PhucVu);

    /// <summary>
    /// Sắc thái hiển thị (class CSS <c>status-*</c>), 4 màu: waiting = vàng, preparing = xanh dương,
    /// served = xanh lá, cancelled = đỏ. Luôn kèm chữ, không chỉ dựa vào màu.
    /// </summary>
    public static string Tone(DonHang d) => d.DaHuy ? "cancelled" : d.PhucVu switch
    {
        Waiting => "waiting",
        Preparing => "preparing",
        Served => "served",
        _ => "void"
    };
}

/// <summary>Bảng gỗ số nhận món của quán.</summary>
public static class SoNhanMons
{
    public const int Min = 1;
    public const int Max = 50;
}
