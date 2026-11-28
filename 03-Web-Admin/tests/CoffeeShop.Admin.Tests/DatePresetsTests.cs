using System.Globalization;
using CoffeeShop.Admin.Utilities;

namespace CoffeeShop.Admin.Tests;

/// <summary>Mốc lọc thời gian dùng chung (Tổng quan, Đơn hàng, Lịch sử kho).</summary>
public class DatePresetsTests
{
    private static readonly DateTime Now = new(2026, 11, 28, 15, 30, 0);

    [Fact]
    public void MacDinh_LaHomNay_VaDungDauDanhSach()
    {
        Assert.Equal(DatePresets.Today, DatePresets.Default);
        Assert.Equal(DatePresets.Today, DatePresets.Options[0]);
        Assert.Equal(DatePresets.Custom, DatePresets.Options[^1]);
    }

    [Theory]
    [InlineData(DatePresets.Today, "2026-11-28 00:00")]
    [InlineData(DatePresets.Last24h, "2026-11-27 15:30")]
    [InlineData(DatePresets.Last7, "2026-11-22 00:00")]    // 7 ngày lịch, tính cả hôm nay
    [InlineData(DatePresets.Last30, "2026-10-30 00:00")]   // 30 ngày lịch, tính cả hôm nay
    public void From_TinhMocBatDau(string preset, string expected)
    {
        Assert.Equal(DateTime.Parse(expected, CultureInfo.InvariantCulture), DatePresets.From(preset, Now));
    }

    [Fact]
    public void From_ChonKhoangNgay_KhongCoMocNhanh()
    {
        Assert.Null(DatePresets.From(DatePresets.Custom, Now));
    }

    [Fact]
    public void ValidateCustom_ChiChonMotNgay_LocDungNgayDo()
    {
        var day = DateTime.Today.AddDays(-8).AddHours(9);   // luôn là ngày quá khứ

        string? error = DatePresets.ValidateCustom(day, null, out var start, out var end);

        Assert.Null(error);
        Assert.Equal(day.Date, start);
        Assert.Equal(day.Date, end);
    }

    [Fact]
    public void ValidateCustom_BaoLoi_KhiNgayKetThucTruocNgayBatDau()
    {
        string? error = DatePresets.ValidateCustom(DateTime.Today.AddDays(-2), DateTime.Today.AddDays(-3), out _, out _);

        Assert.Equal("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.", error);
    }

    [Fact]
    public void ValidateCustom_BaoLoi_KhiChuaChonNgayNao()
    {
        Assert.Equal("Hãy chọn ít nhất một ngày.", DatePresets.ValidateCustom(null, null, out _, out _));
    }

    [Fact]
    public void ValidateCustom_BaoLoi_KhiChonNgayTuongLai()
    {
        var nextYear = DateTime.Today.AddYears(1);

        Assert.Equal("Không chọn ngày trong tương lai.", DatePresets.ValidateCustom(DateTime.Today, nextYear, out _, out _));
    }
}
