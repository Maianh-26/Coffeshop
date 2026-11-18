using CoffeeShop.Api.Common;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Controllers;

/// <summary>Bảng DanhMuc và KichThuoc</summary>
[ApiController]
[Route("api/v1")]
public class DanhMucController(AppDbContext db) : ControllerBase
{
    // ============================================================ DANH MỤC
    private async Task<DanhMuc> Tim(int id) => await db.DanhMucs.FindAsync(id) ?? throw ApiException.NotFound("Danh mục");

    private async Task<DanhMucDto> Dto(DanhMuc d) =>
        new(d.MaDanhMuc, d.TenDanhMuc, await db.Mons.CountAsync(m => m.MaDanhMuc == d.MaDanhMuc && !m.NgungBan));

    [HttpGet("danh-muc"), AllowAnonymous]
    public async Task<List<DanhMucDto>> DanhSach() =>
        await db.DanhMucs.OrderBy(d => d.TenDanhMuc)
            .Select(d => new DanhMucDto(d.MaDanhMuc, d.TenDanhMuc, d.Mons.Count(m => !m.NgungBan)))
            .ToListAsync();

    [HttpGet("danh-muc/{id:int}"), AllowAnonymous]
    public async Task<DanhMucDto> ChiTiet(int id) => await Dto(await Tim(id));

    [HttpPost("danh-muc"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<IActionResult> Tao(DanhMucRequest req)
    {
        var d = new DanhMuc { TenDanhMuc = req.TenDanhMuc };
        db.DanhMucs.Add(d);
        await db.SaveChangesAsync();
        return StatusCode(201, await Dto(d));
    }

    [HttpPut("danh-muc/{id:int}"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<DanhMucDto> Sua(int id, DanhMucRequest req)
    {
        var d = await Tim(id);
        d.TenDanhMuc = req.TenDanhMuc;
        await db.SaveChangesAsync();
        return await Dto(d);
    }

    [HttpDelete("danh-muc/{id:int}"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<IActionResult> Xoa(int id)
    {
        var d = await Tim(id);
        var n = await db.Mons.CountAsync(m => m.MaDanhMuc == id);
        if (n > 0) throw ApiException.Conflict($"Danh mục còn {n} món (kể cả món ngừng bán), hãy chuyển món sang danh mục khác trước");
        db.DanhMucs.Remove(d);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ============================================================ KÍCH THƯỚC
    [HttpGet("kich-thuoc"), AllowAnonymous]
    public async Task<List<KichThuocDto>> DanhSachKichThuoc() =>
        (await db.KichThuocs.OrderBy(k => k.MaKichThuoc.Length).ThenByDescending(k => k.MaKichThuoc).ToListAsync()).Select(k => k.ToDto()).ToList();

    [HttpPost("kich-thuoc"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<IActionResult> TaoKichThuoc(KichThuocRequest req)
    {
        var ma = req.MaKichThuoc.ToUpperInvariant();
        if (await db.KichThuocs.AnyAsync(k => k.MaKichThuoc == ma)) throw ApiException.Conflict($"Kích thước {ma} đã tồn tại");
        var k = new KichThuoc { MaKichThuoc = ma, TenKichThuoc = req.TenKichThuoc };
        db.KichThuocs.Add(k);
        await db.SaveChangesAsync();
        return StatusCode(201, k.ToDto());
    }

    [HttpPut("kich-thuoc/{ma}"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<KichThuocDto> SuaKichThuoc(string ma, KichThuocRequest req)
    {
        var k = await db.KichThuocs.FindAsync(ma.ToUpperInvariant()) ?? throw ApiException.NotFound("Kích thước");
        k.TenKichThuoc = req.TenKichThuoc;
        await db.SaveChangesAsync();
        return k.ToDto();
    }

    [HttpDelete("kich-thuoc/{ma}"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<IActionResult> XoaKichThuoc(string ma)
    {
        var k = await db.KichThuocs.FindAsync(ma.ToUpperInvariant()) ?? throw ApiException.NotFound("Kích thước");
        if (await db.ChiTietMons.AnyAsync(c => c.MaKichThuoc == k.MaKichThuoc))
            throw ApiException.Conflict("Kích thước đang được dùng trong bảng giá món");
        db.KichThuocs.Remove(k);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
