using System.ComponentModel.DataAnnotations;
using CoffeeShop.Api.Common;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Controllers;

/// <summary>Bảng Topping + CongThucMon của topping</summary>
[ApiController]
[Route("api/v1/topping")]
public class ToppingController(AppDbContext db) : ControllerBase
{
    private IQueryable<Topping> DayDu() => db.Toppings.Include(t => t.CongThucs).ThenInclude(c => c.NguyenLieu);

    private async Task<Topping> Tim(int id) =>
        await DayDu().FirstOrDefaultAsync(t => t.MaTopping == id) ?? throw ApiException.NotFound("Topping");

    /// <summary>Khách chỉ thấy topping đang phục vụ; quản lý xem tất cả với ?baoGomNgungBan=true</summary>
    [HttpGet, AllowAnonymous]
    public async Task<List<ToppingDto>> DanhSach([FromQuery] bool baoGomNgungBan = false)
    {
        var q = DayDu();
        if (!baoGomNgungBan) q = q.Where(t => !t.NgungBan);
        return (await q.OrderBy(t => t.TenTopping).ToListAsync()).Select(t => t.ToDto()).ToList();
    }

    [HttpGet("{id:int}"), AllowAnonymous]
    public async Task<ToppingDto> ChiTiet(int id) => (await Tim(id)).ToDto();

    [HttpPost, Authorize(Roles = VaiTros.QuanLy)]
    public async Task<IActionResult> Tao(ToppingRequest req)
    {
        var t = new Topping { TenTopping = req.TenTopping, GiaThem = req.GiaThem!.Value, ConHang = req.ConHang, NgungBan = req.NgungBan };
        db.Toppings.Add(t);
        await db.SaveChangesAsync();
        return StatusCode(201, t.ToDto());
    }

    [HttpPut("{id:int}"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<ToppingDto> Sua(int id, ToppingRequest req)
    {
        var t = await Tim(id);
        (t.TenTopping, t.GiaThem, t.ConHang, t.NgungBan) = (req.TenTopping, req.GiaThem!.Value, req.ConHang, req.NgungBan);
        await db.SaveChangesAsync();
        return t.ToDto();
    }

    /// <summary>Nhân viên báo topping tạm hết / có lại</summary>
    [HttpPatch("{id:int}/con-hang"), Authorize(Roles = VaiTros.TatCa)]
    public async Task<ToppingDto> ConHang(int id, ConHangRequest req)
    {
        var t = await Tim(id);
        t.ConHang = req.ConHang!.Value;
        await db.SaveChangesAsync();
        return t.ToDto();
    }

    /// <summary>Xóa mềm: NgungBan = 1</summary>
    [HttpDelete("{id:int}"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<IActionResult> Xoa(int id)
    {
        var t = await Tim(id);
        t.NgungBan = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/cong-thuc"), Authorize(Roles = VaiTros.TatCa)]
    public async Task<List<CongThucDongDto>> CongThuc(int id) =>
        (await Tim(id)).CongThucs.OrderBy(c => c.MaCongThuc).Select(c => c.ToDto()).ToList();

    [HttpPut("{id:int}/cong-thuc"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<List<CongThucDongDto>> LuuCongThuc(int id, [MaxLength(50)] List<CongThucDongRequest> req)
    {
        await Tim(id);
        db.ChangeTracker.Clear();
        await CongThucHelper.ThayThe(db, req, null, id);
        return await CongThuc(id);
    }
}
