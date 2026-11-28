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
        .Include(d => d.NguoiThu)
        .Include(d => d.ChiTiets).ThenInclude(c => c.Toppings)
        .AsSplitQuery();

    /// <summary>Bảng DonHang trơn, không Include — để lọc / đếm / sắp xếp trước khi nạp chi tiết.</summary>
    public IQueryable<DonHang> DonHangs() => db.DonHangs.AsNoTracking();

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
        var now = GioViet.Now;
        var chiTiets = await DungChiTiet(req, now);
        var donHang = new DonHang
        {
            MaNguoiDung = maNhanVien,
            PhuongThucThanhToan = req.PhuongThucThanhToan ?? "CASH",
            TrangThaiThanhToan = TrangThais.Pending,
            ThoiGianTao = now,
            ChiTiets = chiTiets,
            TongTien = TinhTongTien(chiTiets),
        };
        db.DonHangs.Add(donHang);
        await db.SaveChangesAsync();
        return await Tim(donHang.MaDonHang);
    }

    private static decimal TinhTongTien(IEnumerable<ChiTietDonHang> chiTiets) =>
        chiTiets.Sum(c => (c.DonGiaBan + c.Toppings.Sum(t => t.DonGiaTopping)) * c.SoLuong);

    /// <summary>
    /// Dựng danh sách chi tiết đơn từ request (dùng chung cho đặt món và sửa món). Không ghi DB.
    ///  - kiểm tra món/size/topping còn bán → 404 / 422 (MON_NGUNG_BAN, MON_HET_HANG, SIZE_KHONG_AP_DUNG, TOPPING_HET_HANG),
    ///  - áp khuyến mãi của món tại <paramref name="thoiDiemGia"/> (đặt món: bây giờ; sửa món: giờ tạo đơn),
    ///  - kiểm tra đủ nguyên liệu theo CongThucMon → 422 HET_NGUYEN_LIEU,
    ///  - snapshot tên món + size, tên & giá topping.
    /// </summary>
    private async Task<List<ChiTietDonHang>> DungChiTiet(DatMonRequest req, DateTime thoiDiemGia)
    {
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

        var chiTiets = new List<ChiTietDonHang>();
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
                DonGiaBan = Mapping.GiaSauGiam(ctm.GiaBan, mon.KhuyenMai, thoiDiemGia),
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
            chiTiets.Add(chiTiet);
        }

        // Kiểm tra tồn kho (thông minh: chặn đơn không thể pha chế ngay từ lúc đặt)
        var maNls = canDung.Keys.ToList();
        var nls = await db.NguyenLieus.Where(n => maNls.Contains(n.MaNguyenLieu)).ToDictionaryAsync(n => n.MaNguyenLieu);
        var thieu = canDung
            .Where(kv => nls[kv.Key].SoLuongTon < kv.Value)
            .Select(kv => new { tenNguyenLieu = nls[kv.Key].TenNguyenLieu, canDung = kv.Value, conTon = nls[kv.Key].SoLuongTon, donViTinh = nls[kv.Key].DonViTinh })
            .ToList();
        if (thieu.Count > 0) throw ApiException.Unprocessable("HET_NGUYEN_LIEU", "Không đủ nguyên liệu để pha chế đơn này", thieu);
        return chiTiets;
    }

    // ------------------------------------------------------------------ SỬA MÓN
    private const string KhongSuaDuoc = "Chỉ sửa được đơn chưa thanh toán, chưa hủy và chưa phục vụ";

    /// <summary>
    /// PUT /don-hang/{id}: thay TOÀN BỘ món của đơn PENDING chưa SERVED, tính lại TongTien.
    /// Kiểm tra giống đặt món; giá khuyến mãi theo giờ tạo đơn; không trừ kho;
    /// giữ nguyên người tạo, giờ tạo, số nhận món, trạng thái phục vụ.
    /// </summary>
    public async Task<DonHang> SuaMon(int id, DatMonRequest req)
    {
        var don = await Tim(id);
        if (don.TrangThaiThanhToan != TrangThais.Pending || don.TrangThaiPhucVu == TrangThaiPhucVus.Served)
            throw ApiException.Conflict(KhongSuaDuoc);

        // Kiểm tra & dựng món mới TRƯỚC khi ghi: lỗi 404/422 thì đơn cũ giữ nguyên
        var chiTiets = await DungChiTiet(req, don.ThoiGianTao);
        var tongTien = TinhTongTien(chiTiets);
        var phuongThuc = req.PhuongThucThanhToan ?? don.PhuongThucThanhToan;
        db.ChangeTracker.Clear();

        await using var tx = await db.Database.BeginTransactionAsync();
        // Câu ghi ĐẦU TIÊN là cập nhật có điều kiện trên dòng DonHang: vừa kiểm tra lại trạng thái, vừa giữ khóa dòng
        // tới khi commit → sp_ThanhToanVaTruKho chạy cùng lúc phải chờ, rồi trừ kho theo món MỚI (hoặc ta nhận 409 nếu thanh toán xong trước).
        var soDong = await db.DonHangs
            .Where(d => d.MaDonHang == id && d.TrangThaiThanhToan == TrangThais.Pending && d.TrangThaiPhucVu != TrangThaiPhucVus.Served)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.TongTien, tongTien).SetProperty(d => d.PhuongThucThanhToan, phuongThuc));
        if (soDong == 0) throw ApiException.Conflict(KhongSuaDuoc); // tx chưa commit → tự rollback khi dispose

        // Không có ON DELETE CASCADE: xóa topping của các dòng → xóa dòng → thêm dòng mới
        await db.ChiTietDonHangToppings
            .Where(t => db.ChiTietDonHangs.Any(c => c.MaChiTietDon == t.MaChiTietDon && c.MaDonHang == id))
            .ExecuteDeleteAsync();
        await db.ChiTietDonHangs.Where(c => c.MaDonHang == id).ExecuteDeleteAsync();
        foreach (var c in chiTiets) c.MaDonHang = id;
        db.ChiTietDonHangs.AddRange(chiTiets);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        db.ChangeTracker.Clear();
        return await Tim(id);
    }

    // ------------------------------------------------------------------ THANH TOÁN
    /// <summary>
    /// Cập nhật phương thức rồi gọi stored procedure sp_ThanhToanVaTruKho
    /// (đổi PAID + ghi giờ thu tiền, người thu tiền + trừ kho + ghi LichSuKho, cùng một transaction).
    /// </summary>
    /// <param name="maNguoiThu">Người đang đăng nhập bấm thu tiền (lấy từ token, không nhận từ client).</param>
    public async Task<ThanhToanResponse> ThanhToan(int id, ThanhToanRequest req, int? maNguoiThu)
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
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sp_ThanhToanVaTruKho @MaDonHang = {id}, @MaNguoiThu = {maNguoiThu}");
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

    /// <summary>Khách đổi QR ↔ tiền mặt trên tablet (không tạo đơn trùng). Chỉ áp dụng cho đơn còn PENDING.</summary>
    public async Task<DonHang> DoiPhuongThuc(int id, string phuongThuc)
    {
        var don = await db.DonHangs.FindAsync(id) ?? throw ApiException.NotFound("Đơn hàng");
        if (don.TrangThaiThanhToan != TrangThais.Pending)
            throw ApiException.Conflict($"Đơn đang ở trạng thái {don.TrangThaiThanhToan}, không đổi được phương thức thanh toán");
        if (don.PhuongThucThanhToan != phuongThuc)
        {
            don.PhuongThucThanhToan = phuongThuc;
            await db.SaveChangesAsync();
        }
        return don;
    }

    // ------------------------------------------------------------------ PHỤC VỤ TẠI QUẦY
    /// <summary>Đơn đang giữ số = có số + chưa phục vụ xong + chưa hủy (trùng điều kiện index UX_DonHang_SoNhanMon_DangGiu).</summary>
    private IQueryable<DonHang> DonDangGiuSo(int soNhanMon) => db.DonHangs.AsNoTracking().Where(d =>
        d.SoNhanMon == soNhanMon
        && (d.TrangThaiPhucVu == TrangThaiPhucVus.Waiting || d.TrangThaiPhucVu == TrangThaiPhucVus.Preparing)
        && d.TrangThaiThanhToan != TrangThais.Cancelled);

    private static ApiException SoDangDung(int so, int maDonGiu) => new(409, "SO_NHAN_MON_DANG_DUNG",
        $"Số {so} đang được dùng cho đơn #{maDonGiu} (chưa phục vụ xong). Hãy chọn số khác.",
        new { soNhanMon = so, maDonHangDangGiu = maDonGiu });

    /// <summary>
    /// Gán / đổi / bỏ (null) số nhận món. Chỉ khi đơn chưa phục vụ xong và chưa hủy.
    /// Kiểm tra trùng trước, và vẫn bắt lỗi unique index cho trường hợp hai máy gán cùng số cùng lúc.
    /// </summary>
    public async Task<DonHang> GanSoNhanMon(int id, int? soNhanMon)
    {
        var don = await Tim(id);
        if (don.TrangThaiThanhToan == TrangThais.Cancelled || don.TrangThaiPhucVu == TrangThaiPhucVus.Served)
            throw ApiException.Conflict("Đơn đã phục vụ hoặc đã hủy, không đổi được số nhận món");
        if (don.SoNhanMon == soNhanMon) return don; // gửi lại đúng số đang có → không tính là trùng

        if (soNhanMon is { } so)
        {
            var maDonGiu = await DonDangGiuSo(so).Where(d => d.MaDonHang != id).Select(d => (int?)d.MaDonHang).FirstOrDefaultAsync();
            if (maDonGiu is { } ma) throw SoDangDung(so, ma);
        }

        // Cập nhật có điều kiện: máy khác vừa chuyển SERVED / hủy đơn giữa lúc kiểm tra và lúc ghi → 0 dòng → 409
        int soDong;
        try
        {
            soDong = await db.DonHangs
                .Where(d => d.MaDonHang == id && d.TrangThaiPhucVu != TrangThaiPhucVus.Served && d.TrangThaiThanhToan != TrangThais.Cancelled)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.SoNhanMon, soNhanMon));
        }
        catch (SqlException ex) when (soNhanMon is not null && DbErrors.LaTrungKhoa(ex))
        {
            // Hai máy gán cùng số cùng lúc: unique index lọc chặn máy thứ hai
            var soTrung = soNhanMon.Value;
            var ma = await DonDangGiuSo(soTrung).Where(d => d.MaDonHang != id).Select(d => (int?)d.MaDonHang).FirstOrDefaultAsync();
            throw ma is { } m ? SoDangDung(soTrung, m) : ApiException.Conflict($"Số {soTrung} đang được dùng cho đơn khác. Hãy chọn số khác.");
        }
        if (soDong == 0) throw ApiException.Conflict("Đơn đã phục vụ hoặc đã hủy, không đổi được số nhận món");

        db.ChangeTracker.Clear();
        return await Tim(id);
    }

    private static string TenPhucVu(string trangThai) => trangThai switch
    {
        TrangThaiPhucVus.Waiting => "Chờ chuẩn bị",
        TrangThaiPhucVus.Preparing => "Đang chuẩn bị",
        TrangThaiPhucVus.Served => "Đã phục vụ",
        _ => trangThai,
    };

    /// <summary>Chỉ cho bước kế tiếp: WAITING → PREPARING → SERVED (không bỏ bước, không lùi). Không phụ thuộc thanh toán.</summary>
    public async Task<DonHang> DoiTrangThaiPhucVu(int id, string trangThaiMoi)
    {
        var don = await Tim(id);
        if (don.TrangThaiThanhToan == TrangThais.Cancelled) throw ApiException.Conflict("Đơn đã bị hủy");

        var hienTai = don.TrangThaiPhucVu;
        var buocKe = hienTai switch
        {
            TrangThaiPhucVus.Waiting => TrangThaiPhucVus.Preparing,
            TrangThaiPhucVus.Preparing => TrangThaiPhucVus.Served,
            _ => null,
        };
        if (trangThaiMoi != buocKe)
            throw ApiException.Conflict(hienTai == TrangThaiPhucVus.Waiting && trangThaiMoi == TrangThaiPhucVus.Served
                ? "Đơn chưa bắt đầu chuẩn bị, không thể xác nhận đã phục vụ"
                : $"Đơn đang ở trạng thái {TenPhucVu(hienTai)}, không chuyển sang {TenPhucVu(trangThaiMoi)} được");

        // Cập nhật có điều kiện: hai máy bấm cùng lúc thì chỉ một máy thành công
        var soDong = await db.DonHangs
            .Where(d => d.MaDonHang == id && d.TrangThaiPhucVu == hienTai && d.TrangThaiThanhToan != TrangThais.Cancelled)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.TrangThaiPhucVu, trangThaiMoi));
        if (soDong == 0) throw ApiException.Conflict("Đơn vừa được cập nhật ở máy khác, hãy tải lại");

        db.ChangeTracker.Clear();
        return await Tim(id);
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
            don.GioHoanTien = GioViet.Now;
        }
        don.TrangThaiThanhToan = TrangThais.Cancelled;
        don.LyDoHuy = lyDo;
        await db.SaveChangesAsync();
        return don;
    }
}
