using CoffeeShop.Api.Common;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;
using CoffeeShop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
        var query = service.Query().AsNoTracking();
        if (q.TrangThai is not null) query = query.Where(d => d.TrangThaiThanhToan == q.TrangThai);
        if (q.TrangThaiPhucVu is not null)
            query = query.Where(d => d.TrangThaiPhucVu == q.TrangThaiPhucVu && d.TrangThaiThanhToan != TrangThais.Cancelled);
        if (q.TuNgay is { } tu) query = query.Where(d => d.ThoiGianTao >= tu);
        if (q.DenNgay is { } den) query = query.Where(d => d.ThoiGianTao <= den);
        if (!string.IsNullOrEmpty(q.TimKiem))
        {
            // Mã đơn BẮT ĐẦU BẰNG chuỗi số, HOẶC số nhận món BẰNG ĐÚNG số đó (chuỗi vượt int → chỉ so mã đơn)
            var tk = q.TimKiem;
            int? so = int.TryParse(tk, out var n) ? n : null;
            query = query.Where(d => d.MaDonHang.ToString().StartsWith(tk) || (so != null && d.SoNhanMon == so));
        }
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
        var rows = await sorted.Skip(q.Skip).Take(q.Limit).ToListAsync();
        return new Paged<DonHangDto>(rows.Select(d => d.ToDto()).ToList(), PhanTrang.Of(q.Page, q.Limit, total));
    }

    [HttpGet("{id:int}"), Authorize(Roles = VaiTros.TatCa)]
    public async Task<DonHangDto> ChiTiet(int id) => (await service.Tim(id)).ToDto();

    /// <summary>PUT /don-hang/{id} — sửa món của đơn chưa thanh toán, chưa phục vụ (body giống POST /don-hang)</summary>
    [HttpPut("{id:int}"), Authorize(Roles = VaiTros.TatCa)]
    public async Task<DonHangDto> SuaMon(int id, DatMonRequest req) => (await service.SuaMon(id, req)).ToDto();

    /// <summary>POST /don-hang/{id}/thanh-toan — gọi sp_ThanhToanVaTruKho (PAID + giờ/người thu tiền + trừ kho + ghi LichSuKho)</summary>
    [HttpPost("{id:int}/thanh-toan"), Authorize(Roles = VaiTros.TatCa)]
    public Task<ThanhToanResponse> ThanhToan(int id, ThanhToanRequest req) => service.ThanhToan(id, req, User.MaNguoiDung());

    /// <summary>
    /// POST /don-hang/{id}/xac-nhan-tien-mat — nút "Xác nhận đã thanh toán tiền mặt" trên web admin.
    /// Giống /thanh-toan với CASH; tablet đang chờ sẽ hiện "Đã thanh toán bằng tiền mặt".
    /// </summary>
    [HttpPost("{id:int}/xac-nhan-tien-mat"), Authorize(Roles = VaiTros.TatCa)]
    public Task<ThanhToanResponse> XacNhanTienMat(
        int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] XacNhanTienMatRequest? req) =>
        service.ThanhToan(id,
            new ThanhToanRequest { PhuongThucThanhToan = "CASH", TienKhachDua = req?.TienKhachDua },
            User.MaNguoiDung());

    /// <summary>
    /// GET /don-hang/{id}/trang-thai — công khai cho tablet hỏi định kỳ "đã thanh toán chưa, bằng cách nào".
    /// Chỉ trả thông tin rút gọn (không lộ chi tiết món / nhân viên).
    /// </summary>
    [HttpGet("{id:int}/trang-thai"), AllowAnonymous]
    public async Task<TrangThaiDonDto> TrangThai(int id) => TrangThaiDonDto.Of(await service.Tim(id));

    /// <summary>PUT /don-hang/{id}/phuong-thuc-thanh-toan — công khai: khách đổi QR ↔ tiền mặt khi đơn còn PENDING</summary>
    [HttpPut("{id:int}/phuong-thuc-thanh-toan"), AllowAnonymous]
    public async Task<TrangThaiDonDto> DoiPhuongThuc(int id, DoiPhuongThucRequest req) =>
        TrangThaiDonDto.Of(await service.DoiPhuongThuc(id, req.PhuongThucThanhToan));

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
