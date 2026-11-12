using CoffeeShop.Api.Common;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;
using CoffeeShop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Controllers;

/// <summary>Bảng NguyenLieu + LichSuKho</summary>
[ApiController]
[Route("api/v1")]
[Authorize(Roles = VaiTros.QuanLy)] // Toàn bộ phần nguyên liệu & kho chỉ dành cho quản lý
public class KhoController(AppDbContext db, KhoService kho) : ControllerBase
{
    /// <summary>GET /nguyen-lieu — kèm dự báo số ngày còn dùng. ?sapHet=true để lấy danh sách cần nhập gấp.</summary>
    [HttpGet("nguyen-lieu")]
    public Task<List<NguyenLieuDto>> DanhSach([FromQuery] bool? sapHet, [FromQuery] bool? dangSuDung) => kho.DanhSach(sapHet, dangSuDung);

    [HttpGet("nguyen-lieu/{id:int}")]
    public Task<NguyenLieuDto> ChiTiet(int id) => kho.Mot(id);

    /// <summary>Tạo nguyên liệu. Nếu soLuongTon &gt; 0 sẽ ghi 1 dòng LichSuKho loại NhapHang.</summary>
    [HttpPost("nguyen-lieu")]
    public async Task<IActionResult> Tao(NguyenLieuRequest req)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var nl = new NguyenLieu
        {
            TenNguyenLieu = req.TenNguyenLieu, DonViTinh = req.DonViTinh, SoLuongTon = req.SoLuongTon,
            MucCanhBaoToiThieu = req.MucCanhBaoToiThieu, DangSuDung = req.DangSuDung,
        };
        db.NguyenLieus.Add(nl);
        await db.SaveChangesAsync();
        if (req.SoLuongTon > 0)
        {
            db.LichSuKhos.Add(new LichSuKho
            {
                MaNguyenLieu = nl.MaNguyenLieu, MaNguoiDung = User.MaNguoiDung(), LoaiGiaoDich = LoaiGiaoDichs.NhapHang,
                SoLuongThayDoi = req.SoLuongTon, ThoiGian = DateTime.Now, GhiChu = "Tồn kho ban đầu",
            });
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return StatusCode(201, await kho.Mot(nl.MaNguyenLieu));
    }

    /// <summary>Sửa thông tin. Không sửa trực tiếp SoLuongTon — dùng nhap-kho / xuat-huy để có lịch sử.</summary>
    [HttpPatch("nguyen-lieu/{id:int}")]
    public async Task<NguyenLieuDto> Sua(int id, NguyenLieuCapNhatRequest req)
    {
        var nl = await db.NguyenLieus.FindAsync(id) ?? throw ApiException.NotFound("Nguyên liệu");
        nl.TenNguyenLieu = req.TenNguyenLieu ?? nl.TenNguyenLieu;
        nl.DonViTinh = req.DonViTinh ?? nl.DonViTinh;
        nl.MucCanhBaoToiThieu = req.MucCanhBaoToiThieu ?? nl.MucCanhBaoToiThieu;
        nl.DangSuDung = req.DangSuDung ?? nl.DangSuDung;
        await db.SaveChangesAsync();
        return await kho.Mot(id);
    }

    /// <summary>Xóa mềm: DangSuDung = 0</summary>
    [HttpDelete("nguyen-lieu/{id:int}")]
    public async Task<IActionResult> Xoa(int id)
    {
        var nl = await db.NguyenLieus.FindAsync(id) ?? throw ApiException.NotFound("Nguyên liệu");
        nl.DangSuDung = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>POST /nguyen-lieu/{id}/nhap-kho — cộng tồn + ghi LichSuKho 'NhapHang'</summary>
    [HttpPost("nguyen-lieu/{id:int}/nhap-kho")]
    public Task<NguyenLieuDto> NhapKho(int id, GiaoDichKhoRequest req) =>
        kho.GiaoDich(id, LoaiGiaoDichs.NhapHang, req.SoLuong!.Value, req.GhiChu, User.MaNguoiDung());

    /// <summary>POST /nguyen-lieu/{id}/xuat-huy — trừ tồn (hỏng, đổ vỡ, hết hạn) + ghi LichSuKho 'XuatHuy'</summary>
    [HttpPost("nguyen-lieu/{id:int}/xuat-huy")]
    public Task<NguyenLieuDto> XuatHuy(int id, GiaoDichKhoRequest req) =>
        kho.GiaoDich(id, LoaiGiaoDichs.XuatHuy, req.SoLuong!.Value, req.GhiChu, User.MaNguoiDung());

    /// <summary>GET /lich-su-kho — nhật ký nhập/xuất/tiêu hao</summary>
    [HttpGet("lich-su-kho")]
    public async Task<Paged<LichSuKhoDto>> LichSu([FromQuery] LichSuKhoQuery q)
    {
        var query = db.LichSuKhos.Include(l => l.NguyenLieu).AsQueryable();
        if (q.MaNguyenLieu is { } nl) query = query.Where(l => l.MaNguyenLieu == nl);
        if (q.LoaiGiaoDich is not null) query = query.Where(l => l.LoaiGiaoDich == q.LoaiGiaoDich);
        if (q.TuNgay is { } tu) query = query.Where(l => l.ThoiGian >= tu);
        if (q.DenNgay is { } den) query = query.Where(l => l.ThoiGian <= den);
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(l => l.ThoiGian).ThenByDescending(l => l.MaLichSu)
            .Skip(q.Skip).Take(q.Limit).ToListAsync();
        return new Paged<LichSuKhoDto>(rows.Select(l => l.ToDto()).ToList(), PhanTrang.Of(q.Page, q.Limit, total));
    }
}
