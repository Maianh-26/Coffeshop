using System.Globalization;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Dựng chuỗi query (?a=1&amp;b=2) — bỏ qua tham số null/rỗng, mã hóa giá trị, ngày giờ theo ISO 8601.
/// </summary>
public static class Query
{
    public static string Build(string path, params (string Key, object? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => p.Value is not null && p.Value is not "")
            .Select(p => $"{p.Key}={Uri.EscapeDataString(Format(p.Value!))}")
            .ToList();

        return parts.Count == 0 ? path : $"{path}?{string.Join("&", parts)}";
    }

    private static string Format(object value) => value switch
    {
        DateTime d => d.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    /// <summary>
    /// Cuối ngày (23:59:59.997 — mốc lớn nhất của kiểu SQL DATETIME) để bộ lọc "đến ngày" (backend: &lt;=)
    /// bao gồm cả đơn tạo trong giây cuối cùng của ngày.
    /// </summary>
    public static DateTime? EndOfDay(DateTime? date) => date?.Date.AddDays(1).AddMilliseconds(-3);
}
