using System.Globalization;

namespace CoffeeShop.Admin.Utilities;

/// <summary>
/// Mốc lọc thời gian dùng chung cho các trang có danh sách theo thời gian (Đơn hàng, Lịch sử kho).
/// Mốc nhanh tính lùi đúng 24 giờ / 7 ngày / 30 ngày từ thời điểm hiện tại (khoảng trượt),
/// không có mốc kết thúc → luôn gồm cả bản ghi vừa tạo. "Chọn khoảng ngày…" mở 2 ô Từ ngày / Đến ngày.
/// </summary>
public static class DatePresets
{
    public const string All = "";
    public const string Last24h = "24h";
    public const string Last7 = "7d";
    public const string Last30 = "30d";
    public const string Custom = "custom";   // tự chọn Từ ngày / Đến ngày

    public static readonly string[] Options = [All, Last24h, Last7, Last30, Custom];

    public static string Label(string preset) => preset switch
    {
        Last24h => "Trong 24 giờ qua",
        Last7 => "Trong 7 ngày qua",
        Last30 => "Trong 30 ngày qua",
        Custom => "Chọn khoảng ngày…",
        _ => "Tất cả thời gian"
    };

    /// <summary>Mốc bắt đầu của một lựa chọn; null = không lọc theo thời gian.</summary>
    public static DateTime? From(string preset, DateTime now) => preset switch
    {
        Last24h => now.AddHours(-24),
        Last7 => now.AddDays(-7),
        Last30 => now.AddDays(-30),
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
