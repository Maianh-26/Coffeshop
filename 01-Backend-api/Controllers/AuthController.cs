using CoffeeShop.Api.Common;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Controllers;

/// <summary>Đăng nhập & tài khoản cá nhân (bảng NguoiDung)</summary>
[ApiController]
[Route("api/v1")]
public class AuthController(AppDbContext db, TokenService tokens) : ControllerBase
{
    /// <summary>GET /health — kiểm tra API + kết nối database</summary>
    [HttpGet("health"), AllowAnonymous]
    public async Task<IActionResult> Health() =>
        Ok(new { status = "ok", database = await db.Database.CanConnectAsync() ? "connected" : "disconnected", time = DateTime.Now });

    /// <summary>POST /auth/dang-nhap</summary>
    [HttpPost("auth/dang-nhap"), AllowAnonymous]
    public async Task<DangNhapResponse> DangNhap(DangNhapRequest req)
    {
        var user = await db.NguoiDungs.FirstOrDefaultAsync(u => u.TenDangNhap == req.TenDangNhap);
        // Tài khoản đã xóa trả giống hệt sai mật khẩu (không tiết lộ tài khoản từng tồn tại).
        // Kiểm tra DaXoa trước TrangThai vì tài khoản đã xóa cũng có TrangThai = 0.
        if (user is null || user.DaXoa || !Passwords.Verify(req.MatKhau, user.MatKhau))
            throw new ApiException(401, "UNAUTHORIZED", "Tên đăng nhập hoặc mật khẩu không đúng");
        if (!user.TrangThai) throw ApiException.Forbidden("Tài khoản đã bị khóa, liên hệ quản lý");
        var (token, hetHan) = tokens.Create(user);
        return new DangNhapResponse(token, hetHan, user.ToDto());
    }

    /// <summary>GET /auth/toi — thông tin tài khoản đang đăng nhập</summary>
    [HttpGet("auth/toi"), Authorize]
    public async Task<NguoiDungDto> Toi() => (await HienTai()).ToDto();

    /// <summary>POST /auth/doi-mat-khau</summary>
    [HttpPost("auth/doi-mat-khau"), Authorize]
    public async Task<IActionResult> DoiMatKhau(DoiMatKhauRequest req)
    {
        var user = await HienTai();
        if (!Passwords.Verify(req.MatKhauCu, user.MatKhau)) throw ApiException.BadRequest("Mật khẩu cũ không đúng");
        user.MatKhau = Passwords.Hash(req.MatKhauMoi);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<NguoiDung> HienTai()
    {
        var user = await db.NguoiDungs.FindAsync(User.MaNguoiDung());
        if (user is null || user.DaXoa || !user.TrangThai) throw new ApiException(401, "UNAUTHORIZED", "Tài khoản không còn hoạt động");
        return user;
    }
}
