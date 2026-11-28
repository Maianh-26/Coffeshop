using System.Globalization;

namespace CoffeeShop.Admin.Utilities;

/// <summary>
/// Mốc lọc thời gian dùng chung cho mọi trang có lọc theo thời gian (Tổng quan, Đơn hàng, Lịch sử kho) —
/// cùng một bộ lựa chọn, cùng mặc định "Hôm nay" (chủ quán quyết định 25/11/2026, theo thiết kế trang Tổng quan).
/// Hôm nay = từ 00:00 hôm nay; 24 giờ qua = lùi đúng 24 giờ từ lúc này; 7 / 30 ngày qua = 7 / 30 ngày lịch,
/// tính cả hôm nay (từ 00:00 của 6 / 29 ngày trước). Các mốc nhanh không có mốc kết thúc → luôn gồm cả bản ghi
/// vừa tạo. "Chọn khoảng ngày…" mở 2 ô Từ ngày / Đến ngày. Không có "Tất cả thời gian" — khoảng dài dùng "Chọn khoảng ngày…".
/// </summary>
public static class DatePresets
{
    public const string Today = "today";     // từ 00:00 hôm nay tới hiện tại — lựa chọn mặc định
    public const string Last24h = "24h";
    public const string Last7 = "7d";
    public const string Last30 = "30d";
    public const string Custom = "custom";   // tự chọn Từ ngày / Đến ngày

    /// <summary>Lựa chọn mặc định của mọi trang (và là mốc nút Làm mới đưa về).</summary>
    public const string Default = Today;

    /// <summary>Thứ tự trong ô chọn: Hôm nay · 24 giờ · 7 ngày · 30 ngày · Chọn khoảng ngày…</summary>
    public static readonly string[] Options = [Today, Last24h, Last7, Last30, Custom];

    public static string Label(string preset) => preset switch
    {
        Today => "Hôm nay",
        Last24h => "Trong 24 giờ qua",
        Last7 => "Trong 7 ngày qua",
        Last30 => "Trong 30 ngày qua",
        _ => "Chọn khoảng ngày…"
    };

    /// <summary>Mốc bắt đầu của một mốc nhanh (giống cách trang Tổng quan tính); null với "Chọn khoảng ngày…".</summary>
    public static DateTime? From(string preset, DateTime now) => preset switch
    {
        Today => now.Date,
        Last24h => now.AddHours(-24),
        Last7 => now.Date.AddDays(-6),
        Last30 => now.Date.AddDays(-29),
        _ => null
    };

    /// <summary>Giá trị cho thuộc tính min / max của ô &lt;input type="date"&gt;.</summary>
    public static string IsoDate(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Kiểm tra khoảng ngày tự chọn. Chỉ chọn một đầu → lọc đúng ngày đó.
    /// Trả về lỗi (tiếng Việt) hoặc null kèm khoảng đã chuẩn hóa về 00:00.
    /// </summary>
    public static string? ValidateCustom(DateTime? from, DateTime? to, out DateTime start, out DateTime end)
    {
        start = end = default;

        if (from is null && to is null) return "Hãy chọn ít nhất một ngày.";

        start = (from ?? to)!.Value.Date;
        end = (to ?? from)!.Value.Date;

        if (end < start) return "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.";

        // Ô ngày có max nhưng vẫn gõ tay được ngày vượt mốc (form dùng novalidate) → kiểm tra lại cả 2 đầu
        if (start > DateTime.Today || end > DateTime.Today) return "Không chọn ngày trong tương lai.";

        return null;
    }
}
