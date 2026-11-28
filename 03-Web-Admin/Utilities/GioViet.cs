namespace CoffeeShop.Admin.Utilities;

/// <summary>
/// Đồng hồ của web admin: giờ Việt Nam (UTC+7, không có giờ mùa hè), giống backend (01-Backend-api/Common/GioViet).
/// Mọi mốc "Hôm nay", "24 giờ qua", đơn treo, hạn khuyến mãi đều tính theo đồng hồ này, nên máy quầy / điện thoại
/// đặt sai múi giờ vẫn lọc đúng như máy chủ. Kind = Unspecified, giống giờ backend trả về (không kèm offset).
/// </summary>
public static class GioViet
{
    private static readonly TimeSpan LechUtc = TimeSpan.FromHours(7);

    /// <summary>Thời điểm hiện tại theo giờ Việt Nam.</summary>
    public static DateTime Now => TuUtc(DateTime.UtcNow);

    /// <summary>00:00 hôm nay theo giờ Việt Nam.</summary>
    public static DateTime Today => Now.Date;

    /// <summary>Đổi một mốc UTC (ví dụ giờ hết hạn của token) sang giờ Việt Nam để hiển thị.</summary>
    public static DateTime TuUtc(DateTime utc) => DateTime.SpecifyKind(utc + LechUtc, DateTimeKind.Unspecified);
}
