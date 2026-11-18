using CoffeeShop.Api.Common;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Controllers;

/// <summary>Quản lý tài khoản nhân viên (chỉ QuanLy). Tài khoản đã xóa (DaXoa) coi như không tồn tại.</summary>
[ApiController]
[Route("api/v1/nguoi-dung")]
[Authorize(Roles = VaiTros.QuanLy)]
public class NguoiDungController(AppDbContext db) : ControllerBase
{
    private IQueryable<NguoiDung> ConHieuLuc => db.NguoiDungs.Where(u => !u.DaXoa);

    private async Task<NguoiDung> Tim(int id) =>
        await ConHieuLuc.FirstOrDefaultAsync(u => u.MaNguoiDung == id) ?? throw ApiException.NotFound("Người dùng");

    /// <summary>Email không bắt buộc: null / rỗng / toàn khoảng trắng → null; có giá trị → chữ thường để so trùng.</summary>
    private static string? ChuanHoaEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private Task<bool> EmailDaDung(string email, int? truMa = null) =>
        db.NguoiDungs.AnyAsync(u => u.Email == email && u.MaNguoiDung != truMa);

    [HttpGet]
    public async Task<List<NguoiDungDto>> DanhSach([FromQuery] string? vaiTro, [FromQuery] bool? trangThai)
    {
        var q = ConHieuLuc.AsNoTracking();
        if (!string.IsNullOrEmpty(vaiTro)) q = q.Where(u => u.VaiTro == vaiTro);
        if (trangThai is { } t) q = q.Where(u => u.TrangThai == t);
        return (await q.OrderBy(u => u.MaNguoiDung).ToListAsync()).Select(u => u.ToDto()).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<NguoiDungDto> ChiTiet(int id) => (await Tim(id)).ToDto();

    [HttpPost]
    public async Task<IActionResult> Tao(NguoiDungTaoRequest req)
    {
        // Tên đăng nhập của tài khoản đã xóa đã được đổi thành "<cũ>#xoa<id>" nên không còn chặn tạo lại
        if (await db.NguoiDungs.AnyAsync(u => u.TenDangNhap == req.TenDangNhap)) throw ApiException.Conflict("Tên đăng nhập đã tồn tại");
        var email = ChuanHoaEmail(req.Email);
        if (email is not null && await EmailDaDung(email)) throw ApiException.Conflict("Email đã được sử dụng");
        var u = new NguoiDung
        {
            TenDangNhap = req.TenDangNhap, MatKhau = Passwords.Hash(req.MatKhau), HoTen = req.HoTen,
            Email = email, VaiTro = req.VaiTro, TrangThai = true,
        };
        db.NguoiDungs.Add(u);
        await db.SaveChangesAsync();
        return StatusCode(201, u.ToDto());
    }

    /// <summary>
    /// Sửa thông tin, đổi vai trò, khóa/mở khóa (trangThai), đặt lại mật khẩu.
    /// Đổi vai trò / khóa → token đang dùng của tài khoản đó bị 401 ngay (kiểm tra trong Program.cs OnTokenValidated).
    /// </summary>
    [HttpPatch("{id:int}")]
    public async Task<NguoiDungDto> CapNhat(int id, NguoiDungCapNhatRequest req)
    {
        var u = await Tim(id);
        if (id == User.MaNguoiDung() && (req.TrangThai == false || req.VaiTro == VaiTros.NhanVien))
            throw ApiException.Conflict("Không thể tự khóa hoặc tự hạ quyền tài khoản của chính mình");
        var email = req.Email?.Trim().ToLowerInvariant(); // null = giữ nguyên
        if (email is not null && await EmailDaDung(email, id)) throw ApiException.Conflict("Email đã được sử dụng");
        u.HoTen = req.HoTen ?? u.HoTen;
        u.Email = email ?? u.Email;
        u.VaiTro = req.VaiTro ?? u.VaiTro;
        u.TrangThai = req.TrangThai ?? u.TrangThai;
        if (req.MatKhauMoi is not null) u.MatKhau = Passwords.Hash(req.MatKhauMoi);
        await db.SaveChangesAsync();
        return u.ToDto();
    }

    /// <summary>
    /// DELETE /nguoi-dung/{id} — xóa mềm (DonHang, LichSuKho còn tham chiếu tới tài khoản).
    /// Đổi TenDangNhap thành "&lt;cũ&gt;#xoa&lt;id&gt;" (ký tự # không được phép khi tạo → không bao giờ trùng tài khoản thật)
    /// và Email = null để tạo lại được tài khoản cùng tên đăng nhập / email. Giữ HoTen cho đơn / lịch sử kho cũ.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Xoa(int id)
    {
        var u = await Tim(id);
        if (id == User.MaNguoiDung()) throw ApiException.Conflict("Không thể xóa tài khoản của chính mình");
        // Phòng hờ: người xóa là Quản lý đang hoạt động và không tự xóa được, nên điều kiện này hầu như luôn đúng
        if (u.VaiTro == VaiTros.QuanLy && u.TrangThai
            && !await ConHieuLuc.AnyAsync(x => x.MaNguoiDung != id && x.VaiTro == VaiTros.QuanLy && x.TrangThai))
            throw ApiException.Conflict("Phải còn ít nhất một tài khoản Quản lý");

        var hauTo = $"#xoa{id}";
        const int doDaiToiDa = 50; // NguoiDung.TenDangNhap VARCHAR(50)
        u.TenDangNhap = (u.TenDangNhap.Length + hauTo.Length > doDaiToiDa
            ? u.TenDangNhap[..(doDaiToiDa - hauTo.Length)]
            : u.TenDangNhap) + hauTo;
        u.Email = null;
        u.TrangThai = false;
        u.DaXoa = true;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
