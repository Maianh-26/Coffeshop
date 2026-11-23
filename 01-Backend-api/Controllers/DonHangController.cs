using CoffeeShop.Api.Common;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;
using CoffeeShop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Controllers;

/// <summary>Bảng DonHang + ChiTietDonHang + ChiTietDonHang_Topping</summary>
[ApiController]
[Route("api/v1/don-hang")]
public class DonHangController(DonHangService service) : ControllerBase
{
    /// <summary>
    /// POST /don-hang — đặt món.
    /// Khách tự order trên tablet: không cần token → MaNguoiDung = NULL.
    /// Nhân viên order hộ tại quầy: gửi token → MaNguoiDung = nhân viên.
    /// </summary>
    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> DatMon(DatMonRequest req) =>
        StatusCode(201, (await service.DatMon(req, User.MaNguoiDung())).ToDto());

    /// <summary>GET /don-hang — danh sách đơn (lọc trạng thái, thời gian, tìm mã đơn / số nhận món, sắp xếp)</summary>
    [HttpGet, Authorize(Roles = VaiTros.TatCa)]
    public async Task<Paged<DonHangDto>> DanhSach([FromQuery] DonHangQuery q)
    {
        // Lọc trên bảng DonHang trơn (chưa Include) — các điều kiện đều so cột trực tiếp để SQL dùng được index
        var query = service.DonHangs();
        if (q.TrangThai is not null) query = query.Where(d => d.TrangThaiThanhToan == q.TrangThai);
        if (q.TrangThaiPhucVu is not null)
            query = query.Where(d => d.TrangThaiPhucVu == q.TrangThaiPhucVu && d.TrangThaiThanhToan != TrangThais.Cancelled);
        if (q.TuNgay is { } tu) query = query.Where(d => d.ThoiGianTao >= tu);
        if (q.DenNgay is { } den) query = query.Where(d => d.ThoiGianTao <= den);
        if (!string.IsNullOrEmpty(q.TimKiem)) query = query.Where(TimKiemDonHang.DieuKien(q.TimKiem));

        var total = await query.CountAsync();
        // Sắp xếp TRƯỚC Skip/Take; cùng tổng tiền thì đơn mới hơn (mã lớn hơn) trước để thứ tự giữa các trang ổn định
        var sorted = q.SapXep switch
        {
            "maDonHang" => query.OrderBy(d => d.MaDonHang),
            "-maDonHang" => query.OrderByDescending(d => d.MaDonHang),
            "tongTien" => query.OrderBy(d => d.TongTien).ThenByDescending(d => d.MaDonHang),
            "-tongTien" => query.OrderByDescending(d => d.TongTien).ThenByDescending(d => d.MaDonHang),
            _ => query.OrderByDescending(d => d.ThoiGianTao).ThenByDescending(d => d.MaDonHang),
        };
        // Bước 1: chỉ lấy MÃ đơn của trang → lọc + sắp xếp + phân trang chạy đúng MỘT lần, đọc trên index.
        // (Include + AsSplitQuery trên cả truy vấn sẽ lặp lại lọc/sắp xếp/OFFSET cho mỗi bảng con.)
        var ids = await sorted.Select(d => d.MaDonHang).Skip(q.Skip).Take(q.Limit).ToListAsync();
        // Bước 2: nạp đủ món / topping cho đúng các mã đó (tìm theo khóa chính), giữ nguyên thứ tự bước 1
        var rows = ids.Count == 0
            ? []
            : await service.Query().AsNoTracking().Where(d => ids.Contains(d.MaDonHang)).ToListAsync();
        var byId = rows.ToDictionary(d => d.MaDonHang);
        var data = ids.Where(byId.ContainsKey).Select(id => byId[id].ToDto()).ToList();
        return new Paged<DonHangDto>(data, PhanTrang.Of(q.Page, q.Limit, total));
    }

    [HttpGet("{id:int}"), Authorize(Roles = VaiTros.TatCa)]
    public async Task<DonHangDto> ChiTiet(int id) => (await service.Tim(id)).ToDto();

    /// <summary>PUT /don-hang/{id} — sửa món của đơn chưa thanh toán, chưa phục vụ (body giống POST /don-hang)</summary>
    [HttpPut("{id:int}"), Authorize(Roles = VaiTros.TatCa)]
    public async Task<DonHangDto> SuaMon(int id, DatMonRequest req) => (await service.SuaMon(id, req)).ToDto();

    /// <summary>POST /don-hang/{id}/thanh-toan — gọi sp_ThanhToanVaTruKho (PAID + giờ/người thu tiền + trừ kho + ghi LichSuKho)</summary>
    [HttpPost("{id:int}/thanh-toan"), Authorize(Roles = VaiTros.TatCa)]
    public Task<ThanhToanResponse> ThanhToan(int id, ThanhToanRequest req) => service.ThanhToan(id, req, User.MaNguoiDung());

    /// <summary>POST /don-hang/{id}/huy — quản lý hủy đơn (đơn đã PAID sẽ được đánh dấu hoàn tiền)</summary>
    [HttpPost("{id:int}/huy"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<DonHangDto> Huy(int id, HuyDonRequest req) => (await service.Huy(id, req.LyDoHuy)).ToDto();

    /// <summary>PUT /don-hang/{id}/so-nhan-mon — gán / đổi / bỏ số trên bảng gỗ đưa khách</summary>
    [HttpPut("{id:int}/so-nhan-mon"), Authorize(Roles = VaiTros.TatCa)]
    public async Task<DonHangDto> SoNhanMon(int id, SoNhanMonRequest req) =>
        (await service.GanSoNhanMon(id, req.SoNhanMon)).ToDto();

    /// <summary>PUT /don-hang/{id}/trang-thai-phuc-vu — WAITING → PREPARING → SERVED</summary>
    [HttpPut("{id:int}/trang-thai-phuc-vu"), Authorize(Roles = VaiTros.TatCa)]
    public async Task<DonHangDto> TrangThaiPhucVu(int id, TrangThaiPhucVuRequest req) =>
        (await service.DoiTrangThaiPhucVu(id, req.TrangThaiPhucVu)).ToDto();
}
