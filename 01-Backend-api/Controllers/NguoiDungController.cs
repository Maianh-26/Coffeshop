using CoffeeShop.Api.Common;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Controllers;

/// <summary>Quản lý tài khoản nhân viên (chỉ QuanLy)</summary>
[ApiController]
[Route("api/v1/nguoi-dung")]
[Authorize(Roles = VaiTros.QuanLy)]
public class NguoiDungController(AppDbContext db) : ControllerBase
{
    private async Task<NguoiDung> Tim(int id) => await db.NguoiDungs.FindAsync(id) ?? throw ApiException.NotFound("Người dùng");

    [HttpGet]
    public async Task<List<NguoiDungDto>> DanhSach([FromQuery] string? vaiTro, [FromQuery] bool? trangThai)
    {
        var q = db.NguoiDungs.AsQueryable();
        if (!string.IsNullOrEmpty(vaiTro)) q = q.Where(u => u.VaiTro == vaiTro);
        if (trangThai is { } t) q = q.Where(u => u.TrangThai == t);
        return (await q.OrderBy(u => u.MaNguoiDung).ToListAsync()).Select(u => u.ToDto()).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<NguoiDungDto> ChiTiet(int id) => (await Tim(id)).ToDto();

    [HttpPost]
    public async Task<IActionResult> Tao(NguoiDungTaoRequest req)
    {
        if (await db.NguoiDungs.AnyAsync(u => u.TenDangNhap == req.TenDangNhap)) throw ApiException.Conflict("Tên đăng nhập đã tồn tại");
        if (await db.NguoiDungs.AnyAsync(u => u.Email == req.Email)) throw ApiException.Conflict("Email đã được sử dụng");
        var u = new NguoiDung
        {
            TenDangNhap = req.TenDangNhap, MatKhau = Passwords.Hash(req.MatKhau), HoTen = req.HoTen,
            Email = req.Email.ToLowerInvariant(), VaiTro = req.VaiTro, TrangThai = true,
        };
        db.NguoiDungs.Add(u);
        await db.SaveChangesAsync();
        return StatusCode(201, u.ToDto());
    }

    /// <summary>Sửa thông tin, đổi vai trò, khóa/mở khóa (trangThai), đặt lại mật khẩu</summary>
    [HttpPatch("{id:int}")]
    public async Task<NguoiDungDto> CapNhat(int id, NguoiDungCapNhatRequest req)
    {
        var u = await Tim(id);
        if (id == User.MaNguoiDung() && (req.TrangThai == false || req.VaiTro == VaiTros.NhanVien))
            throw ApiException.Conflict("Không thể tự khóa hoặc tự hạ quyền tài khoản của chính mình");
        if (req.Email is not null && await db.NguoiDungs.AnyAsync(x => x.Email == req.Email && x.MaNguoiDung != id))
            throw ApiException.Conflict("Email đã được sử dụng");
        u.HoTen = req.HoTen ?? u.HoTen;
        u.Email = req.Email?.ToLowerInvariant() ?? u.Email;
        u.VaiTro = req.VaiTro ?? u.VaiTro;
        u.TrangThai = req.TrangThai ?? u.TrangThai;
        if (req.MatKhauMoi is not null) u.MatKhau = Passwords.Hash(req.MatKhauMoi);
        await db.SaveChangesAsync();
        return u.ToDto();
    }
}
