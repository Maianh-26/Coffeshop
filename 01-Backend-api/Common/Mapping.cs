using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;

namespace CoffeeShop.Api.Common;

/// <summary>Entity → DTO (JSON trả về).</summary>
public static class Mapping
{
    public static NguoiDungDto ToDto(this NguoiDung u) =>
        new(u.MaNguoiDung, u.TenDangNhap, u.HoTen, u.Email, u.VaiTro, u.TrangThai);

    public static KichThuocDto ToDto(this KichThuoc k) => new(k.MaKichThuoc, k.TenKichThuoc);

    public static string? Hhmm(TimeSpan? t) => t?.ToString(@"hh\:mm");

    public static KhuyenMaiDto ToDto(this KhuyenMai k, int soMon, DateTime now) =>
        new(k.MaKhuyenMai, k.Code, k.PhanTramGiam, k.NgayBatDau, k.NgayKetThuc,
            Hhmm(k.GioBatDau), Hhmm(k.GioKetThuc), k.DangApDung(now), soMon);

    /// <summary>Giá sau khuyến mãi (làm tròn đến đồng).</summary>
    public static decimal GiaSauGiam(decimal giaBan, KhuyenMai? km, DateTime now) =>
        km is not null && km.DangApDung(now) ? Math.Round(giaBan * (100 - km.PhanTramGiam) / 100, 0) : giaBan;

    /// <summary>Định mức đủ cho 1 phần không (cần Include CongThucs.NguyenLieu).</summary>
    public static bool DuNguyenLieu(IEnumerable<CongThucMon> congThuc) =>
        congThuc.All(c => c.NguyenLieu is { } nl && nl.SoLuongTon >= c.LuongSuDung);

    /// <summary>Cần Include: DanhMuc, KhuyenMai, ChiTietMons.KichThuoc, ChiTietMons.CongThucs.NguyenLieu.</summary>
    public static MonDto ToDto(this Mon m, DateTime now)
    {
        var sizes = m.ChiTietMons
            .OrderBy(c => c.GiaBan)
            .Select(c => new GiaTheoSizeDto(
                c.MaChiTietMon, c.MaKichThuoc, c.KichThuoc?.TenKichThuoc ?? c.MaKichThuoc,
                c.GiaBan, GiaSauGiam(c.GiaBan, m.KhuyenMai, now), c.DangApDung, DuNguyenLieu(c.CongThucs)))
            .ToList();
        var coTheBan = m.ConHang && !m.NgungBan && sizes.Any(s => s.DangApDung && s.DuNguyenLieu);
        var km = m.KhuyenMai is { } k
            ? new KhuyenMaiNgan(k.MaKhuyenMai, k.Code, k.PhanTramGiam, k.DangApDung(now), Hhmm(k.GioBatDau), Hhmm(k.GioKetThuc))
            : null;
        return new MonDto(m.MaTenMon, m.MaDanhMuc, m.DanhMuc?.TenDanhMuc ?? "", m.TenMon, m.HinhAnh,
            m.ConHang, m.NgungBan, coTheBan, km, sizes);
    }

    public static ToppingDto ToDto(this Topping t) =>
        new(t.MaTopping, t.TenTopping, t.GiaThem, t.ConHang, t.NgungBan, DuNguyenLieu(t.CongThucs));

    public static CongThucDongDto ToDto(this CongThucMon c) =>
        new(c.MaCongThuc, c.MaNguyenLieu, c.NguyenLieu?.TenNguyenLieu ?? "", c.NguyenLieu?.DonViTinh ?? "", c.LuongSuDung);

    public static LichSuKhoDto ToDto(this LichSuKho l) =>
        new(l.MaLichSu, l.MaNguyenLieu, l.NguyenLieu?.TenNguyenLieu ?? "", l.MaDonHang, l.MaNguoiDung,
            l.LoaiGiaoDich, l.SoLuongThayDoi, l.ThoiGian, l.GhiChu);

    public static DonHangDto ToDto(this DonHang d, bool kemChiTiet = true) =>
        new(d.MaDonHang, d.MaNguoiDung, d.NguoiDung?.HoTen, d.TongTien, d.PhuongThucThanhToan,
            d.TrangThaiThanhToan, d.ThoiGianTao, d.LyDoHuy, d.DaHoanTien, d.GioHoanTien,
            kemChiTiet
                ? d.ChiTiets.OrderBy(c => c.MaChiTietDon).Select(c => new ChiTietDonDto(
                    c.MaChiTietDon, c.MaChiTietMon, c.TenMonLuu, c.DonGiaBan, c.SoLuong, c.GhiChu,
                    c.Toppings.OrderBy(t => t.MaChiTietTopping)
                        .Select(t => new ToppingDaChonDto(t.MaTopping, t.TenToppingLuu, t.DonGiaTopping)).ToList(),
                    (c.DonGiaBan + c.Toppings.Sum(t => t.DonGiaTopping)) * c.SoLuong)).ToList()
                : null);
}
