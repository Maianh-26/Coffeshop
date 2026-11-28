using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Tests;

/// <summary>Trạng thái hiển thị của món, topping và tình trạng tồn kho nguyên liệu.</summary>
public class TrangThaiMonKhoTests
{
    [Theory]
    [InlineData(true, true, true, TrangThaiMon.NgungBan)]        // ngừng bán thắng mọi trạng thái khác
    [InlineData(false, false, true, TrangThaiMon.TamHet)]
    [InlineData(false, true, false, TrangThaiMon.HetNguyenLieu)]
    [InlineData(false, true, true, TrangThaiMon.DangBan)]
    public void Mon_ThuTuUuTienTrangThai(bool ngungBan, bool conHang, bool coTheBan, string expected)
    {
        var mon = new Mon { NgungBan = ngungBan, ConHang = conHang, CoTheBan = coTheBan };

        Assert.Equal(expected, TrangThaiMon.Of(mon));
    }

    [Theory]
    [InlineData(true, true, true, TrangThaiMon.NgungBan)]
    [InlineData(false, false, true, TrangThaiMon.TamHet)]
    [InlineData(false, true, false, TrangThaiMon.HetNguyenLieu)]
    [InlineData(false, true, true, TrangThaiMon.DangBan)]
    public void Topping_DungDuNguyenLieu(bool ngungBan, bool conHang, bool duNguyenLieu, string expected)
    {
        var topping = new Topping { NgungBan = ngungBan, ConHang = conHang, DuNguyenLieu = duNguyenLieu };

        Assert.Equal(expected, TrangThaiMon.Of(topping));
    }

    [Theory]
    [InlineData(false, 100, 10, TinhTrangKho.NgungSuDung)]
    [InlineData(true, 0, 10, TinhTrangKho.HetHang)]
    [InlineData(true, -5, 10, TinhTrangKho.HetHang)]   // tồn âm vẫn là hết hàng
    [InlineData(true, 10, 10, TinhTrangKho.SapHet)]    // bằng mức cảnh báo → sắp hết
    [InlineData(true, 11, 10, TinhTrangKho.DuDung)]
    public void Kho_TinhTrangTheoTonVaMucCanhBao(bool dangSuDung, decimal ton, decimal mucCanhBao, string expected)
    {
        var nl = new NguyenLieu { DangSuDung = dangSuDung, SoLuongTon = ton, MucCanhBaoToiThieu = mucCanhBao };

        Assert.Equal(expected, TinhTrangKho.Of(nl));
    }

    [Fact]
    public void Kho_CanNhap_GomSapHetVaHetHang_KhongGomNgungSuDung()
    {
        Assert.True(TinhTrangKho.CanNhap(new NguyenLieu { DangSuDung = true, SoLuongTon = 5, MucCanhBaoToiThieu = 10 }));
        Assert.True(TinhTrangKho.CanNhap(new NguyenLieu { DangSuDung = true, SoLuongTon = 0, MucCanhBaoToiThieu = 10 }));
        Assert.False(TinhTrangKho.CanNhap(new NguyenLieu { DangSuDung = false, SoLuongTon = 0, MucCanhBaoToiThieu = 10 }));
    }

    [Theory]
    [InlineData(-0.5, true)]
    [InlineData(0, false)]
    public void Kho_Am_KhiTonNhoHon0(decimal ton, bool expected)
    {
        Assert.Equal(expected, TinhTrangKho.Am(new NguyenLieu { DangSuDung = true, SoLuongTon = ton }));
    }
}
