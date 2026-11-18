using System.ComponentModel.DataAnnotations;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Controllers;

/// <summary>Báo cáo cho chủ quán (QuanLy)</summary>
[ApiController]
[Route("api/v1/bao-cao")]
[Authorize(Roles = VaiTros.QuanLy)]
public class BaoCaoController(AppDbContext db) : ControllerBase
{
    // Mỗi số liệu lọc theo mốc thời gian riêng (hợp đồng C1): tuNgay so sánh >=, denNgay so sánh <=.

    /// <summary>Đơn TẠO trong khoảng — cho tongSoDon, soDonChoThanhToan, soDonHuy.</summary>
    private IQueryable<DonHang> TaoTrongKhoang(KhoangThoiGian r)
    {
        var q = db.DonHangs.AsNoTracking();
        if (r.TuNgay is { } tu) q = q.Where(d => d.ThoiGianTao >= tu);
        if (r.DenNgay is { } den) q = q.Where(d => d.ThoiGianTao <= den);
        return q;
    }

    /// <summary>
    /// Đơn PAID có giờ THU TIỀN trong khoảng — cho doanh thu, món bán chạy, giờ cao điểm.
    /// Đơn tạo 23:50, thu 00:10 hôm sau → tính vào hôm sau. Đơn đã hoàn tiền đã chuyển CANCELLED nên không tính.
    /// </summary>
    private IQueryable<DonHang> DaThuTrongKhoang(KhoangThoiGian r)
    {
        var q = db.DonHangs.AsNoTracking().Where(d => d.TrangThaiThanhToan == TrangThais.Paid && d.ThoiGianThanhToan != null);
        if (r.TuNgay is { } tu) q = q.Where(d => d.ThoiGianThanhToan >= tu);
        if (r.DenNgay is { } den) q = q.Where(d => d.ThoiGianThanhToan <= den);
        return q;
    }

    /// <summary>GET /bao-cao/doanh-thu — doanh thu = tổng đơn PAID theo giờ thu tiền</summary>
    [HttpGet("doanh-thu")]
    public async Task<BaoCaoDoanhThuDto> DoanhThu([FromQuery] KhoangThoiGian r)
    {
        var tao = await TaoTrongKhoang(r)
            .GroupBy(d => d.TrangThaiThanhToan)
            .Select(g => new { TrangThai = g.Key, SoDon = g.Count() })
            .ToDictionaryAsync(x => x.TrangThai, x => x.SoDon);

        var hoanTien = db.DonHangs.AsNoTracking().Where(d => d.DaHoanTien && d.GioHoanTien != null);
        if (r.TuNgay is { } tu) hoanTien = hoanTien.Where(d => d.GioHoanTien >= tu);
        if (r.DenNgay is { } den) hoanTien = hoanTien.Where(d => d.GioHoanTien <= den);
        var soDonHoanTien = await hoanTien.CountAsync();

        var paid = await DaThuTrongKhoang(r)
            .Select(d => new { d.PhuongThucThanhToan, d.TongTien, ThoiGianThanhToan = d.ThoiGianThanhToan!.Value })
            .ToListAsync();
        var doanhThu = paid.Sum(d => d.TongTien);
        return new BaoCaoDoanhThuDto(
            r.TuNgay, r.DenNgay, tao.Values.Sum(), paid.Count,
            tao.GetValueOrDefault(TrangThais.Pending),
            tao.GetValueOrDefault(TrangThais.Cancelled),
            soDonHoanTien,
            doanhThu,
            paid.Count == 0 ? 0 : Math.Round(doanhThu / paid.Count, 0),
            paid.GroupBy(d => d.PhuongThucThanhToan).ToDictionary(g => g.Key, g => g.Sum(d => d.TongTien)),
            paid.GroupBy(d => DateOnly.FromDateTime(d.ThoiGianThanhToan)).OrderBy(g => g.Key)
                .Select(g => new DoanhThuNgayDto(g.Key, g.Count(), g.Sum(d => d.TongTien))).ToList());
    }

    /// <summary>GET /bao-cao/mon-ban-chay — gom theo món (mọi size), chỉ tính đơn PAID theo giờ thu tiền</summary>
    [HttpGet("mon-ban-chay")]
    public async Task<List<MonBanChayDto>> MonBanChay([FromQuery] KhoangThoiGian r, [FromQuery, Range(1, 50)] int soLuong = 10)
    {
        var don = DaThuTrongKhoang(r).Select(d => d.MaDonHang);
        var mon = await (from ct in db.ChiTietDonHangs
                         join cm in db.ChiTietMons on ct.MaChiTietMon equals cm.MaChiTietMon
                         join m in db.Mons on cm.MaTenMon equals m.MaTenMon
                         where don.Contains(ct.MaDonHang)
                         group ct by new { m.MaTenMon, m.TenMon } into g
                         select new { g.Key.MaTenMon, g.Key.TenMon, SoLuong = g.Sum(x => x.SoLuong), DoanhThu = g.Sum(x => x.DonGiaBan * x.SoLuong) })
                        .ToListAsync();
        // Tiền topping gom theo món: truy vấn riêng (SQL Server không cho SUM trên biểu thức chứa subquery)
        var topping = await (from t in db.ChiTietDonHangToppings
                             join ct in db.ChiTietDonHangs on t.MaChiTietDon equals ct.MaChiTietDon
                             join cm in db.ChiTietMons on ct.MaChiTietMon equals cm.MaChiTietMon
                             where don.Contains(ct.MaDonHang)
                             group new { t.DonGiaTopping, ct.SoLuong } by cm.MaTenMon into g
                             select new { MaTenMon = g.Key, TienTopping = g.Sum(x => x.DonGiaTopping * x.SoLuong) })
                            .ToDictionaryAsync(x => x.MaTenMon, x => x.TienTopping);
        return mon
            .Select(x =>
            {
                var tienTopping = topping.GetValueOrDefault(x.MaTenMon);
                return new MonBanChayDto(x.MaTenMon, x.TenMon, x.SoLuong, x.DoanhThu, tienTopping, x.DoanhThu + tienTopping);
            })
            .OrderByDescending(x => x.SoLuong).ThenByDescending(x => x.DoanhThu).Take(soLuong).ToList();
    }

    /// <summary>
    /// GET /bao-cao/topping-ban-chay — đơn PAID theo giờ thu tiền. SoLuong = tổng số ly có gọi topping,
    /// DoanhThu = Σ DonGiaTopping × số ly. Gom theo mã topping, tên lấy tên hiện tại (đổi tên không tách dòng).
    /// </summary>
    [HttpGet("topping-ban-chay")]
    public async Task<List<ToppingBanChayDto>> ToppingBanChay([FromQuery] KhoangThoiGian r, [FromQuery, Range(1, 50)] int soLuong = 10)
    {
        var don = DaThuTrongKhoang(r).Select(d => d.MaDonHang);
        var rows = await (from t in db.ChiTietDonHangToppings
                          join ct in db.ChiTietDonHangs on t.MaChiTietDon equals ct.MaChiTietDon
                          join tp in db.Toppings on t.MaTopping equals tp.MaTopping
                          where don.Contains(ct.MaDonHang)
                          group new { t.DonGiaTopping, ct.SoLuong } by new { tp.MaTopping, tp.TenTopping } into g
                          select new ToppingBanChayDto(g.Key.MaTopping, g.Key.TenTopping, g.Sum(x => x.SoLuong), g.Sum(x => x.DonGiaTopping * x.SoLuong)))
                         .ToListAsync();
        return rows.OrderByDescending(x => x.SoLuong).ThenByDescending(x => x.DoanhThu).Take(soLuong).ToList();
    }

    /// <summary>GET /bao-cao/gio-cao-diem — 24 khung giờ (theo giờ thu tiền), hỗ trợ xếp ca & đặt giờ vàng khuyến mãi</summary>
    [HttpGet("gio-cao-diem")]
    public async Task<List<GioCaoDiemDto>> GioCaoDiem([FromQuery] KhoangThoiGian r)
    {
        var rows = await DaThuTrongKhoang(r)
            .GroupBy(d => d.ThoiGianThanhToan!.Value.Hour)
            .Select(g => new { Gio = g.Key, SoDon = g.Count(), DoanhThu = g.Sum(d => d.TongTien) })
            .ToDictionaryAsync(x => x.Gio);
        return Enumerable.Range(0, 24)
            .Select(h => rows.TryGetValue(h, out var v) ? new GioCaoDiemDto(h, v.SoDon, v.DoanhThu) : new GioCaoDiemDto(h, 0, 0))
            .ToList();
    }
}
