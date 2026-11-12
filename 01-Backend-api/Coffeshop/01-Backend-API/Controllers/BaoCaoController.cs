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
    private IQueryable<DonHang> TrongKhoang(KhoangThoiGian r)
    {
        var q = db.DonHangs.AsQueryable();
        if (r.TuNgay is { } tu) q = q.Where(d => d.ThoiGianTao >= tu);
        if (r.DenNgay is { } den) q = q.Where(d => d.ThoiGianTao <= den);
        return q;
    }

    /// <summary>GET /bao-cao/doanh-thu — doanh thu = tổng đơn PAID (đơn hoàn tiền đã chuyển CANCELLED nên không tính)</summary>
    [HttpGet("doanh-thu")]
    public async Task<BaoCaoDoanhThuDto> DoanhThu([FromQuery] KhoangThoiGian r)
    {
        var rows = await TrongKhoang(r)
            .Select(d => new { d.TrangThaiThanhToan, d.PhuongThucThanhToan, d.TongTien, d.ThoiGianTao, d.DaHoanTien })
            .ToListAsync();
        var paid = rows.Where(d => d.TrangThaiThanhToan == TrangThais.Paid).ToList();
        var doanhThu = paid.Sum(d => d.TongTien);
        return new BaoCaoDoanhThuDto(
            r.TuNgay, r.DenNgay, rows.Count, paid.Count,
            rows.Count(d => d.TrangThaiThanhToan == TrangThais.Pending),
            rows.Count(d => d.TrangThaiThanhToan == TrangThais.Cancelled),
            rows.Count(d => d.DaHoanTien),
            doanhThu,
            paid.Count == 0 ? 0 : Math.Round(doanhThu / paid.Count, 0),
            paid.GroupBy(d => d.PhuongThucThanhToan).ToDictionary(g => g.Key, g => g.Sum(d => d.TongTien)),
            paid.GroupBy(d => DateOnly.FromDateTime(d.ThoiGianTao)).OrderBy(g => g.Key)
                .Select(g => new DoanhThuNgayDto(g.Key, g.Count(), g.Sum(d => d.TongTien))).ToList());
    }

    /// <summary>GET /bao-cao/mon-ban-chay — gom theo món (mọi size), chỉ tính đơn PAID</summary>
    [HttpGet("mon-ban-chay")]
    public async Task<List<MonBanChayDto>> MonBanChay([FromQuery] KhoangThoiGian r, [FromQuery, Range(1, 50)] int soLuong = 10)
    {
        var don = TrongKhoang(r).Where(d => d.TrangThaiThanhToan == TrangThais.Paid).Select(d => d.MaDonHang);
        var rows = await (from ct in db.ChiTietDonHangs
                          join cm in db.ChiTietMons on ct.MaChiTietMon equals cm.MaChiTietMon
                          join m in db.Mons on cm.MaTenMon equals m.MaTenMon
                          where don.Contains(ct.MaDonHang)
                          group ct by new { m.MaTenMon, m.TenMon } into g
                          select new MonBanChayDto(g.Key.MaTenMon, g.Key.TenMon, g.Sum(x => x.SoLuong), g.Sum(x => x.DonGiaBan * x.SoLuong)))
                         .ToListAsync();
        return rows.OrderByDescending(x => x.SoLuong).ThenByDescending(x => x.DoanhThu).Take(soLuong).ToList();
    }

    /// <summary>GET /bao-cao/gio-cao-diem — 24 khung giờ, hỗ trợ xếp ca & đặt giờ vàng khuyến mãi</summary>
    [HttpGet("gio-cao-diem")]
    public async Task<List<GioCaoDiemDto>> GioCaoDiem([FromQuery] KhoangThoiGian r)
    {
        var rows = await TrongKhoang(r).Where(d => d.TrangThaiThanhToan == TrangThais.Paid)
            .GroupBy(d => d.ThoiGianTao.Hour)
            .Select(g => new { Gio = g.Key, SoDon = g.Count(), DoanhThu = g.Sum(d => d.TongTien) })
            .ToDictionaryAsync(x => x.Gio);
        return Enumerable.Range(0, 24)
            .Select(h => rows.TryGetValue(h, out var v) ? new GioCaoDiemDto(h, v.SoDon, v.DoanhThu) : new GioCaoDiemDto(h, 0, 0))
            .ToList();
    }
}
