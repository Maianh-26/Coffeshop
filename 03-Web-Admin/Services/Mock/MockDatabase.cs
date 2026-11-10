using CoffeeShop.Admin.Models;
using CoffeeShop.Admin.Utilities;

namespace CoffeeShop.Admin.Services.Mock;

/// <summary>
/// Dữ liệu giả lập trong bộ nhớ, dùng khi không chạy backend (Api:UseMock = true).
/// Cấu trúc bảng mô phỏng CSDL QuanCaPheDB của backend; <see cref="MockApiHandler"/> dựng DTO từ đây.
/// Dữ liệu sống trong suốt phiên trình duyệt; tải lại trang (F5) sẽ về dữ liệu mẫu ban đầu.
/// </summary>
public class MockDatabase
{
    // ------------------------------------------------------------------ Bảng
    public List<NguoiDung> NguoiDungs { get; } = [];
    public List<DanhMuc> DanhMucs { get; } = [];
    public List<KichThuoc> KichThuocs { get; } = [];
    public List<MonRow> Mons { get; } = [];
    public List<ToppingRow> Toppings { get; } = [];
    public List<CongThucRow> CongThucs { get; } = [];
    public List<KhuyenMaiRow> KhuyenMais { get; } = [];
    public List<NguyenLieuRow> NguyenLieus { get; } = [];
    public List<LichSuKho> LichSuKhos { get; } = [];
    public List<DonHang> DonHangs { get; } = [];

    /// <summary>Lần cuối mock tự thêm đơn "khách đặt từ tablet".</summary>
    public DateTime LastTabletOrder { get; set; } = DateTime.Now;

    /// <summary>
    /// Nút "Mô phỏng gửi đơn từ Tablet" (chỉ hiện ở chế độ giả lập) bật cờ này:
    /// lần tải danh sách đơn kế tiếp sẽ có thêm một đơn tablet mới.
    /// </summary>
    public bool TabletOrderRequested { get; set; }

    /// <summary>Mật khẩu giả lập: tên đăng nhập → mật khẩu (giống tài khoản mẫu của backend).</summary>
    public Dictionary<string, string> MatKhaus { get; } = new(StringComparer.OrdinalIgnoreCase);

    public class MonRow
    {
        public int MaTenMon { get; set; }
        public int MaDanhMuc { get; set; }
        public string TenMon { get; set; } = "";
        public string? HinhAnh { get; set; }
        public bool ConHang { get; set; } = true;
        public bool NgungBan { get; set; }
        public int? MaKhuyenMai { get; set; }
        public List<ChiTietMonRow> Sizes { get; set; } = [];
    }

    public class ChiTietMonRow
    {
        public int MaChiTietMon { get; set; }
        public string MaKichThuoc { get; set; } = "";
        public decimal GiaBan { get; set; }
        public bool DangApDung { get; set; } = true;
    }

    public class ToppingRow
    {
        public int MaTopping { get; set; }
        public string TenTopping { get; set; } = "";
        public decimal GiaThem { get; set; }
        public bool ConHang { get; set; } = true;
        public bool NgungBan { get; set; }
    }

    /// <summary>Định mức: thuộc MỘT món-size (MaChiTietMon) HOẶC MỘT topping (MaTopping).</summary>
    public class CongThucRow
    {
        public int MaCongThuc { get; set; }
        public int? MaChiTietMon { get; set; }
        public int? MaTopping { get; set; }
        public int MaNguyenLieu { get; set; }
        public decimal LuongSuDung { get; set; }
    }

    public class KhuyenMaiRow
    {
        public int MaKhuyenMai { get; set; }
        public string Code { get; set; } = "";
        public decimal PhanTramGiam { get; set; }
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }
        public TimeSpan? GioBatDau { get; set; }
        public TimeSpan? GioKetThuc { get; set; }

        /// <summary>Giống backend: trong khoảng ngày và (nếu có) trong khung giờ vàng.</summary>
        public bool DangApDung(DateTime now) =>
            now >= NgayBatDau && now <= NgayKetThuc
            && (GioBatDau is null || GioKetThuc is null
                || (now.TimeOfDay >= GioBatDau && now.TimeOfDay <= GioKetThuc));
    }

    public class NguyenLieuRow
    {
        public int MaNguyenLieu { get; set; }
        public string TenNguyenLieu { get; set; } = "";
        public string DonViTinh { get; set; } = "";
        public decimal SoLuongTon { get; set; }
        public decimal MucCanhBaoToiThieu { get; set; }
        public bool DangSuDung { get; set; } = true;
    }

    // ------------------------------------------------------------------ Quên mật khẩu (chỉ giả lập)
    /// <summary>Mã xác minh quên mật khẩu đang chờ: email → thông tin mã.</summary>
    public Dictionary<string, ResetCode> ResetCodes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Nhật ký xin mã theo email (kể cả email không có tài khoản): dùng cho giới hạn gửi lại / số lần mỗi giờ.
    /// Không bị xóa khi mã hết hạn hay bị hủy → không lách được giới hạn.
    /// </summary>
    public Dictionary<string, List<DateTime>> ResetRequestLog { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Token đặt lại mật khẩu (dùng một lần): token → (tên đăng nhập, hết hạn).</summary>
    public Dictionary<string, (string Username, DateTime ExpiresAt)> ResetTokens { get; } = [];

    public class ResetCode
    {
        public string Username { get; init; } = "";
        public string Code { get; init; } = "";
        public DateTime ExpiresAt { get; init; }
        public DateTime SentAt { get; init; }
        public int FailedAttempts { get; set; }
    }

    // ------------------------------------------------------------------ Khóa tự tăng
    public static int NextId<T>(IEnumerable<T> rows, Func<T, int> id) => rows.Select(id).DefaultIfEmpty(0).Max() + 1;

    public MockDatabase()
    {
        SeedAccounts();
        SeedMenu();
        SeedOrders();
    }

    private void SeedAccounts()
    {
        NguoiDungs.AddRange(
        [
            new() { MaNguoiDung = 1, TenDangNhap = "quanly", HoTen = "Chủ quán", Email = "quanly@coffee.vn", VaiTro = Roles.Admin },
            new() { MaNguoiDung = 2, TenDangNhap = "nhanvien", HoTen = "Thu ngân A", Email = "nhanvien@coffee.vn", VaiTro = Roles.Staff },
            new() { MaNguoiDung = 3, TenDangNhap = "minh.nv", HoTen = "Lê Minh", Email = "minh.nv@coffee.vn", VaiTro = Roles.Staff },
        ]);

        MatKhaus["quanly"] = "QuanLy@123";
        MatKhaus["nhanvien"] = "NhanVien@123";
        MatKhaus["minh.nv"] = "NhanVien@123";
    }

    private void SeedMenu()
    {
        DateTime today = DateTime.Today;

        DanhMucs.AddRange(
        [
            new() { MaDanhMuc = 1, TenDanhMuc = "Cà phê" },
            new() { MaDanhMuc = 2, TenDanhMuc = "Trà" },
            new() { MaDanhMuc = 3, TenDanhMuc = "Bánh ngọt" },
        ]);

        KichThuocs.AddRange(
        [
            new() { MaKichThuoc = "S", TenKichThuoc = "Nhỏ" },
            new() { MaKichThuoc = "M", TenKichThuoc = "Vừa" },
            new() { MaKichThuoc = "L", TenKichThuoc = "Lớn" },
        ]);

        KhuyenMais.AddRange(
        [
            new() { MaKhuyenMai = 1, Code = "GIOVANG20", PhanTramGiam = 20, NgayBatDau = today.AddDays(-1), NgayKetThuc = today.AddMonths(3), GioBatDau = new TimeSpan(14, 0, 0), GioKetThuc = new TimeSpan(16, 0, 0) },
            new() { MaKhuyenMai = 2, Code = "KHAITRUONG10", PhanTramGiam = 10, NgayBatDau = today.AddDays(-1), NgayKetThuc = today.AddMonths(1) },
        ]);

        NguyenLieus.AddRange(
        [
            new() { MaNguyenLieu = 1, TenNguyenLieu = "Cà phê hạt", DonViTinh = "gram", SoLuongTon = 5000, MucCanhBaoToiThieu = 500 },
            new() { MaNguyenLieu = 2, TenNguyenLieu = "Sữa đặc", DonViTinh = "ml", SoLuongTon = 3000, MucCanhBaoToiThieu = 300 },
            new() { MaNguyenLieu = 3, TenNguyenLieu = "Sữa tươi", DonViTinh = "ml", SoLuongTon = 4000, MucCanhBaoToiThieu = 500 },
            new() { MaNguyenLieu = 4, TenNguyenLieu = "Đá viên", DonViTinh = "gram", SoLuongTon = 50000, MucCanhBaoToiThieu = 5000 },
            new() { MaNguyenLieu = 5, TenNguyenLieu = "Trà đen", DonViTinh = "gram", SoLuongTon = 2000, MucCanhBaoToiThieu = 200 },
            new() { MaNguyenLieu = 6, TenNguyenLieu = "Đào ngâm", DonViTinh = "gram", SoLuongTon = 3000, MucCanhBaoToiThieu = 300 },
            new() { MaNguyenLieu = 7, TenNguyenLieu = "Trân châu", DonViTinh = "gram", SoLuongTon = 2000, MucCanhBaoToiThieu = 200 },
            new() { MaNguyenLieu = 8, TenNguyenLieu = "Kem cheese", DonViTinh = "gram", SoLuongTon = 1000, MucCanhBaoToiThieu = 100 },
            new() { MaNguyenLieu = 9, TenNguyenLieu = "Bánh tiramisu", DonViTinh = "mieng", SoLuongTon = 3, MucCanhBaoToiThieu = 5 },
        ]);

        int ct = 1;
        MonRow Mon(int id, int danhMuc, string ten, int? km, params (string Size, decimal Gia)[] sizes) => new()
        {
            MaTenMon = id,
            MaDanhMuc = danhMuc,
            TenMon = ten,
            MaKhuyenMai = km,
            Sizes = sizes.Select(s => new ChiTietMonRow { MaChiTietMon = ct++, MaKichThuoc = s.Size, GiaBan = s.Gia }).ToList()
        };

        Mons.AddRange(
        [
            Mon(1, 1, "Cà phê sữa đá", null, ("M", 29000), ("L", 35000)),
            Mon(2, 1, "Cà phê đen", 1, ("M", 25000), ("L", 30000)),
            Mon(3, 1, "Bạc xỉu", 2, ("M", 32000), ("L", 38000)),
            Mon(4, 2, "Trà đào cam sả", 1, ("M", 39000), ("L", 45000)),
            Mon(5, 2, "Trà sữa trân châu", null, ("S", 30000), ("M", 35000), ("L", 42000)),
            Mon(6, 3, "Bánh tiramisu", null, ("M", 45000)),
        ]);

        Toppings.AddRange(
        [
            new() { MaTopping = 1, TenTopping = "Trân châu đen", GiaThem = 5000 },
            new() { MaTopping = 2, TenTopping = "Kem cheese", GiaThem = 10000 },
            new() { MaTopping = 3, TenTopping = "Thêm shot espresso", GiaThem = 10000 },
        ]);

        int cth = 1;
        void Recipe(int? chiTietMon, int? topping, params (int NguyenLieu, decimal Luong)[] lines)
        {
            foreach (var (nl, luong) in lines)
            {
                CongThucs.Add(new CongThucRow
                {
                    MaCongThuc = cth++, MaChiTietMon = chiTietMon, MaTopping = topping, MaNguyenLieu = nl, LuongSuDung = luong
                });
            }
        }

        Recipe(null, 1, (7, 30));
        Recipe(null, 2, (8, 25));
        Recipe(null, 3, (1, 18));
        Recipe(1, null, (1, 16), (2, 24), (4, 120));   // Cà phê sữa đá M
        Recipe(2, null, (1, 20), (2, 30), (4, 160));   // Cà phê sữa đá L
        Recipe(3, null, (1, 18), (4, 120));            // Cà phê đen M
        Recipe(5, null, (1, 10), (2, 20), (3, 80), (4, 100)); // Bạc xỉu M
        Recipe(7, null, (5, 8), (6, 50), (4, 150));    // Trà đào M
        Recipe(12, null, (9, 1));                       // Bánh tiramisu
    }

    private void SeedOrders()
    {
        var now = DateTime.Now;

        // Đơn cũ đã thanh toán trong 7 ngày → có dữ liệu cho báo cáo
        int id = 1;
        for (int day = 6; day >= 1; day--)
        {
            for (int i = 0; i < 2 + day % 3; i++)
            {
                var created = now.Date.AddDays(-day).AddHours(8 + (i * 3 + day) % 12).AddMinutes(i * 7);
                var order = NewOrder(id++, i % 2 == 0 ? null : 2, created,
                    i % 3 == 0 ? PhuongThucThanhToans.QrTransfer : PhuongThucThanhToans.Cash,
                    (1 + (i + day) % 5, 1 + i % 2, i % 2 == 0 ? [1] : []));
                order.TrangThaiThanhToan = TrangThaiDon.Paid;
                DonHangs.Add(order);
            }
        }

        // Đơn hôm nay
        var paid = NewOrder(id++, 2, now.AddHours(-1), PhuongThucThanhToans.Cash, (1, 2, []));
        paid.TrangThaiThanhToan = TrangThaiDon.Paid;
        DonHangs.Add(paid);

        DonHangs.Add(NewOrder(id++, null, now.AddMinutes(-20), PhuongThucThanhToans.QrTransfer, (4, 2, [1])));
        DonHangs.Add(NewOrder(id, null, now.AddMinutes(-5), PhuongThucThanhToans.Cash, (5, 1, [1, 2]), (1, 1, [])));
    }

    /// <summary>
    /// Tạo đơn mẫu (bỏ qua kiểm tra tồn kho). Mỗi dòng: (mã món, số lượng, mã topping); size = size đầu tiên của món.
    /// </summary>
    private DonHang NewOrder(int id, int? maNhanVien, DateTime created, string method,
        params (int Mon, int SoLuong, int[] Toppings)[] lines)
    {
        var order = new DonHang
        {
            MaDonHang = id,
            MaNguoiDung = maNhanVien,
            TenNhanVien = NguoiDungs.FirstOrDefault(u => u.MaNguoiDung == maNhanVien)?.HoTen,
            PhuongThucThanhToan = method,
            TrangThaiThanhToan = TrangThaiDon.Pending,
            ThoiGianTao = created,
            ChiTiet = []
        };

        int lineId = DonHangs.SelectMany(d => d.ChiTiet ?? []).Select(c => c.MaChiTietDon).DefaultIfEmpty(0).Max() + 1;

        foreach (var (monId, soLuong, toppingIds) in lines)
        {
            var mon = Mons.First(m => m.MaTenMon == monId);
            var size = mon.Sizes[0];
            var toppings = toppingIds
                .Select(t => Toppings.First(x => x.MaTopping == t))
                .Select(t => new ToppingDaChon { MaTopping = t.MaTopping, TenToppingLuu = t.TenTopping, DonGiaTopping = t.GiaThem })
                .ToList();

            order.ChiTiet.Add(new ChiTietDon
            {
                MaChiTietDon = lineId++,
                MaChiTietMon = size.MaChiTietMon,
                TenMonLuu = $"{mon.TenMon} - {KichThuocs.First(k => k.MaKichThuoc == size.MaKichThuoc).TenKichThuoc}",
                DonGiaBan = size.GiaBan,
                SoLuong = soLuong,
                Toppings = toppings,
                ThanhTien = (size.GiaBan + toppings.Sum(t => t.DonGiaTopping)) * soLuong
            });
        }

        order.TongTien = order.ChiTiet.Sum(c => c.ThanhTien);
        return order;
    }
}
