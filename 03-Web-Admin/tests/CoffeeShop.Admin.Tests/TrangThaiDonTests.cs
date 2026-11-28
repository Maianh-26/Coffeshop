using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Tests;

/// <summary>Nhãn / sắc thái của trạng thái thanh toán và phục vụ trên trang Đơn hàng.</summary>
public class TrangThaiDonTests
{
    private static DonHang Don(string thanhToan, string? phucVu = null, bool hoanTien = false,
        string phuongThuc = PhuongThucThanhToans.Cash) => new()
    {
        TrangThaiThanhToan = thanhToan,
        TrangThaiPhucVu = phucVu,
        DaHoanTien = hoanTien,
        PhuongThucThanhToan = phuongThuc
    };

    [Fact]
    public void DaChot_ChiKhiDaThu_HoacDaHoanTien()
    {
        Assert.False(PhuongThucThanhToans.DaChot(Don(TrangThaiDon.Pending)));
        Assert.True(PhuongThucThanhToans.DaChot(Don(TrangThaiDon.Paid)));
        Assert.True(PhuongThucThanhToans.DaChot(Don(TrangThaiDon.Cancelled, hoanTien: true)));
        Assert.False(PhuongThucThanhToans.DaChot(Don(TrangThaiDon.Cancelled)));
    }

    [Theory]
    [InlineData(TrangThaiDon.Pending, false, PhuongThucThanhToans.QrTransfer, "Chưa thu")]   // lựa chọn lúc đặt không hiện như sự thật
    [InlineData(TrangThaiDon.Paid, false, PhuongThucThanhToans.Cash, "Đã thu tiền mặt")]
    [InlineData(TrangThaiDon.Paid, false, PhuongThucThanhToans.QrTransfer, "Đã nhận chuyển khoản")]
    [InlineData(TrangThaiDon.Cancelled, false, PhuongThucThanhToans.Cash, "Không thu")]
    [InlineData(TrangThaiDon.Cancelled, true, PhuongThucThanhToans.QrTransfer, "Đã hoàn tiền (chuyển khoản)")]
    public void Summary_MoTaTinhTrangTien(string thanhToan, bool hoanTien, string phuongThuc, string expected)
    {
        Assert.Equal(expected, PhuongThucThanhToans.Summary(Don(thanhToan, hoanTien: hoanTien, phuongThuc: phuongThuc)));
    }

    [Theory]
    [InlineData(TrangThaiDon.Pending, false, "due")]
    [InlineData(TrangThaiDon.Paid, false, "paid")]
    [InlineData(TrangThaiDon.Cancelled, true, "refund")]
    [InlineData(TrangThaiDon.Cancelled, false, "void")]
    public void Tone_ThanhToan(string thanhToan, bool hoanTien, string expected)
    {
        Assert.Equal(expected, PhuongThucThanhToans.Tone(Don(thanhToan, hoanTien: hoanTien)));
    }

    [Theory]
    [InlineData(null, "Chờ phục vụ", "waiting")]   // backend chưa trả trạng thái phục vụ → coi là chờ
    [InlineData(TrangThaiPhucVus.Waiting, "Chờ phục vụ", "waiting")]
    [InlineData(TrangThaiPhucVus.Preparing, "Đang chuẩn bị", "preparing")]
    [InlineData(TrangThaiPhucVus.Served, "Đã phục vụ", "served")]
    public void PhucVu_NhanVaSacThai(string? phucVu, string label, string tone)
    {
        var don = Don(TrangThaiDon.Pending, phucVu);

        Assert.Equal(label, TrangThaiPhucVus.Label(don));
        Assert.Equal(tone, TrangThaiPhucVus.Tone(don));
    }

    [Fact]
    public void PhucVu_DonDaHuy_LuonHienDaHuy()
    {
        var don = Don(TrangThaiDon.Cancelled, TrangThaiPhucVus.Preparing);

        Assert.Equal("Đã hủy", TrangThaiPhucVus.Label(don));
        Assert.Equal("cancelled", TrangThaiPhucVus.Tone(don));
    }
}
