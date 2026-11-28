namespace CoffeeShop.Api.Common;

/// <summary>
/// Đồng hồ của hệ thống: giờ Việt Nam (UTC+7, không có giờ mùa hè), KHÔNG phụ thuộc múi giờ của máy chủ —
/// máy chủ cloud thường chạy UTC, dùng <c>DateTime.Now</c> thì "Hôm nay", hạn khuyến mãi, báo cáo lệch 7 giờ.
/// Kind = Unspecified, giống giá trị đọc từ cột DATETIME, nên JSON trả về mọi mốc giờ cùng một dạng (không kèm offset).
/// </summary>
public static class GioViet
{
    private static readonly TimeSpan LechUtc = TimeSpan.FromHours(7);

    /// <summary>Thời điểm hiện tại theo giờ Việt Nam.</summary>
    public static DateTime Now => TuUtc(DateTime.UtcNow);

    /// <summary>00:00 hôm nay theo giờ Việt Nam.</summary>
    public static DateTime Today => Now.Date;

    /// <summary>Đổi một mốc UTC sang giờ Việt Nam.</summary>
    public static DateTime TuUtc(DateTime utc) => DateTime.SpecifyKind(utc + LechUtc, DateTimeKind.Unspecified);
}
