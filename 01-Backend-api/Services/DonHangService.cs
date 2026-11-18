using CoffeeShop.Api.Common;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Services;

public class DonHangService(AppDbContext db)
{
    public IQueryable<DonHang> Query() => db.DonHangs
        .Include(d => d.NguoiDung)
        .Include(d => d.ChiTiets).ThenInclude(c => c.Toppings)
        .AsSplitQuery();

    public async Task<DonHang> Tim(int id) =>
        await Query().FirstOrDefaultAsync(d => d.MaDonHang == id) ?? throw ApiException.NotFound("Đơn hàng");

    // ------------------------------------------------------------------ ĐẶT MÓN
    /// <summary>
    /// Tạo đơn PENDING. Hệ thống tự:
    ///  - kiểm tra món/size/topping còn bán,
    ///  - áp khuyến mãi của món (kể cả khung giờ vàng) → lưu vào DonGiaBan,
    ///  - kiểm tra đủ nguyên liệu theo CongThucMon (chưa trừ kho — trừ khi thanh toán bằng sp_ThanhToanVaTruKho),
    ///  - snapshot tên món + size, tên & giá topping.
    /// </summary>
    public async Task<DonHang> DatMon(DatMonRequest req, int? maNhanVien)
    {
        var now = DateTime.Now;
        var maCtms = req.ChiTiet.Select(c => c.MaChiTietMon!.Value).Distinct().ToList();
        var maToppings = req.ChiTiet.SelectMany(c => c.Toppings ?? new()).Distinct().ToList();

        var ctms = await db.ChiTietMons
            .Include(c => c.Mon).ThenInclude(m => m!.KhuyenMai)
            .Include(c => c.KichThuoc)
            .Include(c => c.CongThucs)
            .Where(c => maCtms.Contains(c.MaChiTietMon))
            .AsSplitQuery()
            .ToDictionaryAsync(c => c.MaChiTietMon);
        var toppings = await db.Toppings.Include(t => t.CongThucs)
            .Where(t => maToppings.Contains(t.MaTopping)).ToDictionaryAsync(t => t.MaTopping);

        var canDung = new Dictionary<int, decimal>(); // MaNguyenLieu → lượng cần
        void Cong(IEnumerable<CongThucMon> ct, int soLuong)
        {
            foreach (var c in ct) canDung[c.MaNguyenLieu] = canDung.GetValueOrDefault(c.MaNguyenLieu) + c.LuongSuDung * soLuong;
        }

        var donHang = new DonHang
        {
            MaNguoiDung = maNhanVien,
            PhuongThucThanhToan = req.PhuongThucThanhToan ?? "CASH",
            TrangThaiThanhToan = TrangThais.Pending,
            ThoiGianTao = now,
        };

        foreach (var dong in req.ChiTiet)
        {
            if (!ctms.TryGetValue(dong.MaChiTietMon!.Value, out var ctm))
                throw ApiException.NotFound($"Chi tiết món #{dong.MaChiTietMon}");
            var mon = ctm.Mon!;
            if (mon.NgungBan) throw ApiException.Unprocessable("MON_NGUNG_BAN", $"Món \"{mon.TenMon}\" đã ngừng kinh doanh");
            if (!mon.ConHang) throw ApiException.Unprocessable("MON_HET_HANG", $"Món \"{mon.TenMon}\" tạm hết hàng");
            if (!ctm.DangApDung) throw ApiException.Unprocessable("SIZE_KHONG_AP_DUNG", $"Size {ctm.MaKichThuoc} của \"{mon.TenMon}\" không còn bán");

            var dsTopping = dong.Toppings ?? new();
            if (dsTopping.Distinct().Count() != dsTopping.Count)
                throw ApiException.BadRequest("Mỗi topping chỉ được chọn tối đa 1 phần/ly");

            var soLuong = dong.SoLuong!.Value;
            var chiTiet = new ChiTietDonHang
            {
                MaChiTietMon = ctm.MaChiTietMon,
                TenMonLuu = $"{mon.TenMon} - {ctm.KichThuoc?.TenKichThuoc ?? ctm.MaKichThuoc}",
                DonGiaBan = Mapping.GiaSauGiam(ctm.GiaBan, mon.KhuyenMai, now),
                SoLuong = soLuong,
                GhiChu = dong.GhiChu,
            };
            Cong(ctm.CongThucs, soLuong);

            foreach (var maTp in dsTopping)
            {
                if (!toppings.TryGetValue(maTp, out var tp)) throw ApiException.NotFound($"Topping #{maTp}");
                if (tp.NgungBan || !tp.ConHang) throw ApiException.Unprocessable("TOPPING_HET_HANG", $"Topping \"{tp.TenTopping}\" hiện không phục vụ");
                chiTiet.Toppings.Add(new ChiTietDonHangTopping { MaTopping = tp.MaTopping, TenToppingLuu = tp.TenTopping, DonGiaTopping = tp.GiaThem });
                Cong(tp.CongThucs, soLuong);
            }
            donHang.ChiTiets.Add(chiTiet);
        }

        // Kiểm tra tồn kho (thông minh: chặn đơn không thể pha chế ngay từ lúc đặt)
        var maNls = canDung.Keys.ToList();
        var nls = await db.NguyenLieus.Where(n => maNls.Contains(n.MaNguyenLieu)).ToDictionaryAsync(n => n.MaNguyenLieu);
        var thieu = canDung
            .Where(kv => nls[kv.Key].SoLuongTon < kv.Value)
            .Select(kv => new { tenNguyenLieu = nls[kv.Key].TenNguyenLieu, canDung = kv.Value, conTon = nls[kv.Key].SoLuongTon, donViTinh = nls[kv.Key].DonViTinh })
            .ToList();
        if (thieu.Count > 0) throw ApiException.Unprocessable("HET_NGUYEN_LIEU", "Không đủ nguyên liệu để pha chế đơn này", thieu);

        donHang.TongTien = donHang.ChiTiets.Sum(c => (c.DonGiaBan + c.Toppings.Sum(t => t.DonGiaTopping)) * c.SoLuong);
        db.DonHangs.Add(donHang);
        await db.SaveChangesAsync();
        return await Tim(donHang.MaDonHang);
    }

    // ------------------------------------------------------------------ THANH TOÁN
    /// <summary>Cập nhật phương thức rồi gọi stored procedure sp_ThanhToanVaTruKho (đổi PAID + trừ kho + ghi LichSuKho).</summary>
    public async Task<ThanhToanResponse> ThanhToan(int id, ThanhToanRequest req)
    {
        var don = await Tim(id);
        if (don.TrangThaiThanhToan != TrangThais.Pending)
            throw ApiException.Conflict($"Đơn đang ở trạng thái {don.TrangThaiThanhToan}, không thể thanh toán");
        if (req.TienKhachDua is { } dua && dua < don.TongTien)
            throw ApiException.BadRequest($"Tiền khách đưa ({dua:N0}) nhỏ hơn tổng tiền ({don.TongTien:N0})");

        don.PhuongThucThanhToan = req.PhuongThucThanhToan;
        await db.SaveChangesAsync();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sp_ThanhToanVaTruKho @MaDonHang = {id}");
        }
        catch (SqlException ex)
        {
            throw ApiException.Conflict(ex.Message);
        }

        db.ChangeTracker.Clear();
        var daThanhToan = await Tim(id);
        var canhBao = await db.NguyenLieus
            .Where(n => n.DangSuDung && n.SoLuongTon <= n.MucCanhBaoToiThieu)
            .OrderBy(n => n.TenNguyenLieu)
            .Select(n => new CanhBaoKhoDto(n.MaNguyenLieu, n.TenNguyenLieu, n.SoLuongTon, n.MucCanhBaoToiThieu, n.DonViTinh))
            .ToListAsync();
        var tienThua = req.TienKhachDua is { } t ? t - daThanhToan.TongTien : 0;
        return new ThanhToanResponse(daThanhToan.ToDto(), tienThua, canhBao);
    }

    // ------------------------------------------------------------------ HỦY / HOÀN TIỀN
    /// <summary>
    /// Quản lý hủy đơn (ghi LyDoHuy).
    /// - Đơn PENDING: chưa trừ kho nên chỉ đổi trạng thái.
    /// - Đơn PAID: đánh dấu DaHoanTien + GioHoanTien. Nguyên liệu đã pha chế coi như đã tiêu hao (không cộng lại kho).
    /// </summary>
    public async Task<DonHang> Huy(int id, string lyDo)
    {
        var don = await Tim(id);
        if (don.TrangThaiThanhToan == TrangThais.Cancelled) throw ApiException.Conflict("Đơn đã bị hủy trước đó");
        if (don.TrangThaiThanhToan == TrangThais.Paid)
        {
            don.DaHoanTien = true;
            don.GioHoanTien = DateTime.Now;
        }
        don.TrangThaiThanhToan = TrangThais.Cancelled;
        don.LyDoHuy = lyDo;
        await db.SaveChangesAsync();
        return don;
    }
}
