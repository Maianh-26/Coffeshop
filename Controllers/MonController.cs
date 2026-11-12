using System.ComponentModel.DataAnnotations;
using CoffeeShop.Api.Common;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;
using CoffeeShop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Controllers;

/// <summary>Bảng TenMon + ChiTietMon (giá theo size) + CongThucMon của món</summary>
[ApiController]
[Route("api/v1")]
public class MonController(AppDbContext db, MenuService menu) : ControllerBase
{
    // ============================================================ ĐỌC (công khai — tablet của khách)
    /// <summary>GET /mon — menu kèm giá theo size, giá sau khuyến mãi, trạng thái còn bán</summary>
    [HttpGet("mon"), AllowAnonymous]
    public async Task<Paged<MonDto>> DanhSach([FromQuery] MonQuery q)
    {
        var now = DateTime.Now;
        var query = menu.MonDayDu();
        if (!q.BaoGomNgungBan) query = query.Where(m => !m.NgungBan);
        if (q.MaDanhMuc is { } dm) query = query.Where(m => m.MaDanhMuc == dm);
        if (!string.IsNullOrWhiteSpace(q.TuKhoa)) query = query.Where(m => m.TenMon.Contains(q.TuKhoa));

        // Menu quán cà phê có quy mô nhỏ → tính giá KM / tồn kho trong bộ nhớ
        IEnumerable<MonDto> items = (await query.ToListAsync()).Select(m => m.ToDto(now));
        if (q.CoTheBan is { } ctb) items = items.Where(m => m.CoTheBan == ctb);
        if (q.DangKhuyenMai is { } km) items = items.Where(m => (m.KhuyenMai?.DangApDung ?? false) == km);

        decimal GiaThapNhat(MonDto m) => m.KichThuoc.Where(s => s.DangApDung).Select(s => s.GiaSauGiam).DefaultIfEmpty(0).Min();
        if (q.SapXep == "ban_chay")
        {
            var daBan = await menu.SoLuongDaBan(now.AddDays(-30));
            items = items.OrderByDescending(m => daBan.GetValueOrDefault(m.MaTenMon)).ThenBy(m => m.TenMon);
        }
        else
        {
            items = q.SapXep switch
            {
                "gia_tang" => items.OrderBy(GiaThapNhat),
                "gia_giam" => items.OrderByDescending(GiaThapNhat),
                _ => items.OrderBy(m => m.TenMon),
            };
        }
        var list = items.ToList();
        return new Paged<MonDto>(list.Skip(q.Skip).Take(q.Limit).ToList(), PhanTrang.Of(q.Page, q.Limit, list.Count));
    }

    [HttpGet("mon/{id:int}"), AllowAnonymous]
    public Task<MonDto> ChiTiet(int id) => menu.LayMonDto(id);

    // ============================================================ GHI (QuanLy)
    private async Task KiemTraThamChieu(int? maDanhMuc, int? maKhuyenMai, IEnumerable<string>? sizes)
    {
        if (maDanhMuc is { } dm && !await db.DanhMucs.AnyAsync(d => d.MaDanhMuc == dm)) throw ApiException.NotFound("Danh mục");
        if (maKhuyenMai is { } km and > 0 && !await db.KhuyenMais.AnyAsync(k => k.MaKhuyenMai == km)) throw ApiException.NotFound("Khuyến mãi");
        if (sizes is not null)
        {
            var list = sizes.Select(s => s.ToUpperInvariant()).ToList();
            if (list.Distinct().Count() != list.Count) throw ApiException.BadRequest("Mỗi kích thước chỉ khai báo một lần");
            var coSan = await db.KichThuocs.Select(k => k.MaKichThuoc).ToListAsync();
            var thieu = list.Except(coSan).ToList();
            if (thieu.Count > 0) throw ApiException.NotFound($"Kích thước {string.Join(", ", thieu)}");
        }
    }

    /// <summary>POST /mon — tạo món kèm bảng giá theo size</summary>
    [HttpPost("mon"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<IActionResult> Tao(MonTaoRequest req)
    {
        await KiemTraThamChieu(req.MaDanhMuc, req.MaKhuyenMai, req.KichThuoc.Select(k => k.MaKichThuoc));
        var mon = new Mon
        {
            MaDanhMuc = req.MaDanhMuc!.Value, TenMon = req.TenMon, HinhAnh = req.HinhAnh, MaKhuyenMai = req.MaKhuyenMai,
            ChiTietMons = req.KichThuoc.Select(k => new ChiTietMon
            {
                MaKichThuoc = k.MaKichThuoc.ToUpperInvariant(), GiaBan = k.GiaBan!.Value, DangApDung = k.DangApDung,
            }).ToList(),
        };
        db.Mons.Add(mon);
        await db.SaveChangesAsync();
        return StatusCode(201, await menu.LayMonDto(mon.MaTenMon));
    }

    /// <summary>PATCH /mon/{id} — sửa thông tin, gắn/gỡ khuyến mãi (maKhuyenMai = 0 để gỡ), ngừng/mở bán</summary>
    [HttpPatch("mon/{id:int}"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<MonDto> Sua(int id, MonCapNhatRequest req)
    {
        var m = await db.Mons.FindAsync(id) ?? throw ApiException.NotFound("Món");
        await KiemTraThamChieu(req.MaDanhMuc, req.MaKhuyenMai, null);
        m.MaDanhMuc = req.MaDanhMuc ?? m.MaDanhMuc;
        m.TenMon = req.TenMon ?? m.TenMon;
        m.HinhAnh = req.HinhAnh ?? m.HinhAnh;
        if (req.MaKhuyenMai is { } km) m.MaKhuyenMai = km == 0 ? null : km;
        m.NgungBan = req.NgungBan ?? m.NgungBan;
        await db.SaveChangesAsync();
        return await menu.LayMonDto(id);
    }

    /// <summary>PATCH /mon/{id}/con-hang — nhân viên báo tạm hết / có hàng lại trong ngày</summary>
    [HttpPatch("mon/{id:int}/con-hang"), Authorize(Roles = VaiTros.TatCa)]
    public async Task<MonDto> ConHang(int id, ConHangRequest req)
    {
        var m = await db.Mons.FindAsync(id) ?? throw ApiException.NotFound("Món");
        m.ConHang = req.ConHang!.Value;
        await db.SaveChangesAsync();
        return await menu.LayMonDto(id);
    }

    /// <summary>DELETE /mon/{id} — xóa mềm: NgungBan = 1 (giữ lịch sử đơn hàng)</summary>
    [HttpDelete("mon/{id:int}"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<IActionResult> Xoa(int id)
    {
        var m = await db.Mons.FindAsync(id) ?? throw ApiException.NotFound("Món");
        m.NgungBan = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ============================================================ GIÁ THEO SIZE (ChiTietMon)
    /// <summary>PUT /mon/{id}/kich-thuoc — thêm/sửa giá từng size. Size không gửi lên sẽ bị tắt (DangApDung = 0),
    /// không xóa vật lý vì ChiTietDonHang tham chiếu tới.</summary>
    [HttpPut("mon/{id:int}/kich-thuoc"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<MonDto> CapNhatGia(int id, [MinLength(1), MaxLength(10)] List<GiaSizeRequest> req)
    {
        var m = await db.Mons.Include(x => x.ChiTietMons).FirstOrDefaultAsync(x => x.MaTenMon == id) ?? throw ApiException.NotFound("Món");
        await KiemTraThamChieu(null, null, req.Select(r => r.MaKichThuoc));
        foreach (var r in req)
        {
            var ma = r.MaKichThuoc.ToUpperInvariant();
            var ct = m.ChiTietMons.FirstOrDefault(c => c.MaKichThuoc == ma);
            if (ct is null) m.ChiTietMons.Add(new ChiTietMon { MaKichThuoc = ma, GiaBan = r.GiaBan!.Value, DangApDung = r.DangApDung });
            else (ct.GiaBan, ct.DangApDung) = (r.GiaBan!.Value, r.DangApDung);
        }
        var giu = req.Select(r => r.MaKichThuoc.ToUpperInvariant()).ToHashSet();
        foreach (var ct in m.ChiTietMons.Where(c => !giu.Contains(c.MaKichThuoc))) ct.DangApDung = false;
        await db.SaveChangesAsync();
        return await menu.LayMonDto(id);
    }

    // ============================================================ CÔNG THỨC (CongThucMon theo ChiTietMon)
    [HttpGet("chi-tiet-mon/{maChiTietMon:int}/cong-thuc"), Authorize(Roles = VaiTros.TatCa)]
    public async Task<List<CongThucDongDto>> CongThuc(int maChiTietMon)
    {
        if (!await db.ChiTietMons.AnyAsync(c => c.MaChiTietMon == maChiTietMon)) throw ApiException.NotFound("Chi tiết món");
        return (await db.CongThucMons.Include(c => c.NguyenLieu).Where(c => c.MaChiTietMon == maChiTietMon)
            .OrderBy(c => c.MaCongThuc).ToListAsync()).Select(c => c.ToDto()).ToList();
    }

    /// <summary>PUT /chi-tiet-mon/{id}/cong-thuc — thay toàn bộ định mức của 1 món-size</summary>
    [HttpPut("chi-tiet-mon/{maChiTietMon:int}/cong-thuc"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<List<CongThucDongDto>> LuuCongThuc(int maChiTietMon, [MaxLength(50)] List<CongThucDongRequest> req)
    {
        if (!await db.ChiTietMons.AnyAsync(c => c.MaChiTietMon == maChiTietMon)) throw ApiException.NotFound("Chi tiết món");
        await CongThucHelper.ThayThe(db, req, maChiTietMon, null);
        return await CongThuc(maChiTietMon);
    }
}

public static class CongThucHelper
{
    /// <summary>Xóa định mức cũ và ghi định mức mới (cho ChiTietMon HOẶC Topping).</summary>
    public static async Task ThayThe(AppDbContext db, List<CongThucDongRequest> req, int? maChiTietMon, int? maTopping)
    {
        var ids = req.Select(r => r.MaNguyenLieu!.Value).ToList();
        if (ids.Distinct().Count() != ids.Count) throw ApiException.BadRequest("Mỗi nguyên liệu chỉ xuất hiện một lần trong công thức");
        var coSan = await db.NguyenLieus.Where(n => ids.Contains(n.MaNguyenLieu)).Select(n => n.MaNguyenLieu).ToListAsync();
        var thieu = ids.Except(coSan).ToList();
        if (thieu.Count > 0) throw ApiException.NotFound($"Nguyên liệu #{string.Join(", #", thieu)}");

        await using var tx = await db.Database.BeginTransactionAsync();
        var cu = await db.CongThucMons.Where(c => c.MaChiTietMon == maChiTietMon && c.MaTopping == maTopping).ToListAsync();
        db.CongThucMons.RemoveRange(cu);
        await db.SaveChangesAsync();
        db.CongThucMons.AddRange(req.Select(r => new CongThucMon
        {
            MaChiTietMon = maChiTietMon, MaTopping = maTopping, MaNguyenLieu = r.MaNguyenLieu!.Value, LuongSuDung = r.LuongSuDung!.Value,
        }));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        db.ChangeTracker.Clear();
    }
}
