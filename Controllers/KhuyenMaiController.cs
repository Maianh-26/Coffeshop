using CoffeeShop.Api.Common;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Controllers;

/// <summary>Bảng KhuyenMai (gắn vào TenMon.MaKhuyenMai, hỗ trợ khung giờ vàng)</summary>
[ApiController]
[Route("api/v1/khuyen-mai")]
public class KhuyenMaiController(AppDbContext db) : ControllerBase
{
    private async Task<KhuyenMai> Tim(int id) => await db.KhuyenMais.FindAsync(id) ?? throw ApiException.NotFound("Khuyến mãi");

    private async Task<KhuyenMaiDto> Dto(KhuyenMai k) =>
        k.ToDto(await db.Mons.CountAsync(m => m.MaKhuyenMai == k.MaKhuyenMai), DateTime.Now);

    /// <summary>?dangApDung=true → chỉ khuyến mãi đang có hiệu lực ngay lúc này</summary>
    [HttpGet, AllowAnonymous]
    public async Task<List<KhuyenMaiDto>> DanhSach([FromQuery] bool? dangApDung)
    {
        var now = DateTime.Now;
        var soMon = await db.Mons.Where(m => m.MaKhuyenMai != null).GroupBy(m => m.MaKhuyenMai!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
        var list = (await db.KhuyenMais.OrderByDescending(k => k.NgayBatDau).ToListAsync())
            .Select(k => k.ToDto(soMon.GetValueOrDefault(k.MaKhuyenMai), now));
        if (dangApDung is { } d) list = list.Where(k => k.DangApDung == d);
        return list.ToList();
    }

    [HttpGet("{id:int}"), AllowAnonymous]
    public async Task<KhuyenMaiDto> ChiTiet(int id) => await Dto(await Tim(id));

    private static (TimeSpan?, TimeSpan?) KiemTra(KhuyenMaiRequest r)
    {
        if (r.NgayBatDau >= r.NgayKetThuc) throw ApiException.BadRequest("ngayBatDau phải trước ngayKetThuc");
        if ((r.GioBatDau is null) != (r.GioKetThuc is null)) throw ApiException.BadRequest("Phải nhập đủ cả gioBatDau và gioKetThuc (hoặc bỏ trống cả hai)");
        TimeSpan? tu = r.GioBatDau is null ? null : TimeSpan.Parse(r.GioBatDau);
        TimeSpan? den = r.GioKetThuc is null ? null : TimeSpan.Parse(r.GioKetThuc);
        if (tu >= den) throw ApiException.BadRequest("gioBatDau phải trước gioKetThuc");
        return (tu, den);
    }

    private async Task GanMon(int maKhuyenMai, List<int>? maTenMons)
    {
        if (maTenMons is null) return;
        var mons = await db.Mons.Where(m => maTenMons.Contains(m.MaTenMon) || m.MaKhuyenMai == maKhuyenMai).ToListAsync();
        var thieu = maTenMons.Except(mons.Select(m => m.MaTenMon)).ToList();
        if (thieu.Count > 0) throw ApiException.NotFound($"Món #{string.Join(", #", thieu)}");
        foreach (var m in mons) m.MaKhuyenMai = maTenMons.Contains(m.MaTenMon) ? maKhuyenMai : null;
    }

    /// <summary>Tạo khuyến mãi và (tùy chọn) gắn luôn vào danh sách món</summary>
    [HttpPost, Authorize(Roles = VaiTros.QuanLy)]
    public async Task<IActionResult> Tao(KhuyenMaiRequest req)
    {
        var (tu, den) = KiemTra(req);
        var code = req.Code.ToUpperInvariant();
        if (await db.KhuyenMais.AnyAsync(k => k.Code == code)) throw ApiException.Conflict("Code khuyến mãi đã tồn tại");
        await using var tx = await db.Database.BeginTransactionAsync();
        var k = new KhuyenMai
        {
            Code = code, PhanTramGiam = req.PhanTramGiam!.Value, NgayBatDau = req.NgayBatDau!.Value,
            NgayKetThuc = req.NgayKetThuc!.Value, GioBatDau = tu, GioKetThuc = den,
        };
        db.KhuyenMais.Add(k);
        await db.SaveChangesAsync();
        await GanMon(k.MaKhuyenMai, req.MaTenMons);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return StatusCode(201, await Dto(k));
    }

    /// <summary>Sửa toàn bộ khuyến mãi. Nếu gửi maTenMons thì danh sách món áp dụng được thay thế.</summary>
    [HttpPut("{id:int}"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<KhuyenMaiDto> Sua(int id, KhuyenMaiRequest req)
    {
        var k = await Tim(id);
        var (tu, den) = KiemTra(req);
        var code = req.Code.ToUpperInvariant();
        if (await db.KhuyenMais.AnyAsync(x => x.Code == code && x.MaKhuyenMai != id)) throw ApiException.Conflict("Code khuyến mãi đã tồn tại");
        (k.Code, k.PhanTramGiam, k.NgayBatDau, k.NgayKetThuc, k.GioBatDau, k.GioKetThuc) =
            (code, req.PhanTramGiam!.Value, req.NgayBatDau!.Value, req.NgayKetThuc!.Value, tu, den);
        await GanMon(id, req.MaTenMons);
        await db.SaveChangesAsync();
        return await Dto(k);
    }

    /// <summary>Xóa khuyến mãi: tự gỡ khỏi các món đang gắn</summary>
    [HttpDelete("{id:int}"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<IActionResult> Xoa(int id)
    {
        var k = await Tim(id);
        await GanMon(id, new List<int>());
        db.KhuyenMais.Remove(k);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
