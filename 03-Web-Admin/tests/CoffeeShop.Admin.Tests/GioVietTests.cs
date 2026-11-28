using CoffeeShop.Admin.Utilities;

namespace CoffeeShop.Admin.Tests;

/// <summary>Đồng hồ giờ Việt Nam dùng chung cho mọi mốc lọc thời gian của web.</summary>
public class GioVietTests
{
    [Fact]
    public void Now_LaUtcCong7Gio_KhongPhuThuocMuiGioMay()
    {
        DateTime truoc = DateTime.UtcNow.AddHours(7);
        DateTime now = GioViet.Now;
        DateTime sau = DateTime.UtcNow.AddHours(7);

        Assert.InRange(now, DateTime.SpecifyKind(truoc, DateTimeKind.Unspecified), DateTime.SpecifyKind(sau, DateTimeKind.Unspecified));
        Assert.Equal(DateTimeKind.Unspecified, now.Kind);   // giống giờ backend trả về (không kèm offset)
    }

    [Fact]
    public void Today_La0GioCuaNgayHienTai()
    {
        DateTime truoc = DateTime.UtcNow.AddHours(7).Date;
        DateTime today = GioViet.Today;
        DateTime sau = DateTime.UtcNow.AddHours(7).Date;

        Assert.Equal(TimeSpan.Zero, today.TimeOfDay);
        Assert.InRange(today, truoc, sau);   // không chập chờn khi chạy đúng lúc qua 0 giờ
    }

    [Fact]
    public void TuUtc_Cong7Gio()
    {
        var utc = new DateTime(2026, 11, 28, 17, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 11, 29, 0, 30, 0), GioViet.TuUtc(utc));
    }
}
