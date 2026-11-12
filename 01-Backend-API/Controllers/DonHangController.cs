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

    /// <summary>GET /don-hang — danh sách đơn (lọc trạng thái, thời gian)</summary>
    [HttpGet, Authorize(Roles = VaiTros.TatCa)]
    public async Task<Paged<DonHangDto>> DanhSach([FromQuery] DonHangQuery q)
    {
        var query = service.Query();
        if (q.TrangThai is not null) query = query.Where(d => d.TrangThaiThanhToan == q.TrangThai);
        if (q.TuNgay is { } tu) query = query.Where(d => d.ThoiGianTao >= tu);
        if (q.DenNgay is { } den) query = query.Where(d => d.ThoiGianTao <= den);
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(d => d.ThoiGianTao).ThenByDescending(d => d.MaDonHang)
            .Skip(q.Skip).Take(q.Limit).ToListAsync();
        return new Paged<DonHangDto>(rows.Select(d => d.ToDto()).ToList(), PhanTrang.Of(q.Page, q.Limit, total));
    }

    [HttpGet("{id:int}"), Authorize(Roles = VaiTros.TatCa)]
    public async Task<DonHangDto> ChiTiet(int id) => (await service.Tim(id)).ToDto();

    /// <summary>POST /don-hang/{id}/thanh-toan — gọi sp_ThanhToanVaTruKho (PAID + trừ kho + ghi LichSuKho)</summary>
    [HttpPost("{id:int}/thanh-toan"), Authorize(Roles = VaiTros.TatCa)]
    public Task<ThanhToanResponse> ThanhToan(int id, ThanhToanRequest req) => service.ThanhToan(id, req);

    /// <summary>POST /don-hang/{id}/huy — quản lý hủy đơn (đơn đã PAID sẽ được đánh dấu hoàn tiền)</summary>
    [HttpPost("{id:int}/huy"), Authorize(Roles = VaiTros.QuanLy)]
    public async Task<DonHangDto> Huy(int id, HuyDonRequest req) => (await service.Huy(id, req.LyDoHuy)).ToDto();
}
