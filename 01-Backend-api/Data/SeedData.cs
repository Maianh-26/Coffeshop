using CoffeeShop.Api.Common;

namespace CoffeeShop.Api.Data;

/// <summary>
/// Dữ liệu mẫu — CHỈ chạy khi bảng NguoiDung đang trống (DB vừa tạo từ QuanCaPheDB.sql).
/// Tắt bằng "SeedSampleData": false trong appsettings.json.
/// </summary>
public static class SeedData
{
    public static void Run(AppDbContext db)
    {
        if (db.NguoiDungs.Any()) return;

        db.NguoiDungs.AddRange(
            new NguoiDung { TenDangNhap = "quanly", MatKhau = Passwords.Hash("QuanLy@123"), HoTen = "Chủ quán", Email = "quanly@coffee.vn", VaiTro = VaiTros.QuanLy },
            new NguoiDung { TenDangNhap = "nhanvien", MatKhau = Passwords.Hash("NhanVien@123"), HoTen = "Thu ngân A", Email = "nhanvien@coffee.vn", VaiTro = VaiTros.NhanVien });

        if (!db.KichThuocs.Any())
            db.KichThuocs.AddRange(
                new KichThuoc { MaKichThuoc = "S", TenKichThuoc = "Nhỏ" },
                new KichThuoc { MaKichThuoc = "M", TenKichThuoc = "Vừa" },
                new KichThuoc { MaKichThuoc = "L", TenKichThuoc = "Lớn" });

        var caPhe = new DanhMuc { TenDanhMuc = "Cà phê" };
        var tra = new DanhMuc { TenDanhMuc = "Trà" };
        var banh = new DanhMuc { TenDanhMuc = "Bánh ngọt" };
        db.DanhMucs.AddRange(caPhe, tra, banh);

        NguyenLieu NL(string ten, string dvt, decimal ton, decimal canhBao) =>
            new() { TenNguyenLieu = ten, DonViTinh = dvt, SoLuongTon = ton, MucCanhBaoToiThieu = canhBao };
        var hat = NL("Cà phê hạt", "gram", 5000, 500);
        var suaDac = NL("Sữa đặc", "ml", 3000, 300);
        var suaTuoi = NL("Sữa tươi", "ml", 8000, 1000);
        var da = NL("Đá viên", "gram", 50000, 5000);
        var traDen = NL("Trà đen", "gram", 2000, 200);
        var dao = NL("Đào ngâm", "gram", 3000, 300);
        var tranChau = NL("Trân châu", "gram", 2000, 300);
        var kem = NL("Kem cheese", "ml", 1500, 200);
        var tiramisu = NL("Bánh tiramisu", "mieng", 3, 5); // dưới mức cảnh báo → demo "sắp hết"
        db.NguyenLieus.AddRange(hat, suaDac, suaTuoi, da, traDen, dao, tranChau, kem, tiramisu);

        // Khung giờ vàng 14:00-16:00 giảm 20% cho trà
        var gioVang = new KhuyenMai
        {
            Code = "GIOVANG20", PhanTramGiam = 20, NgayBatDau = DateTime.Today.AddDays(-1), NgayKetThuc = DateTime.Today.AddMonths(3),
            GioBatDau = new TimeSpan(14, 0, 0), GioKetThuc = new TimeSpan(16, 0, 0),
        };
        // Giảm 10% cả ngày cho bạc xỉu
        var khaiTruong = new KhuyenMai
        {
            Code = "KHAITRUONG10", PhanTramGiam = 10, NgayBatDau = DateTime.Today.AddDays(-1), NgayKetThuc = DateTime.Today.AddMonths(1),
        };
        db.KhuyenMais.AddRange(gioVang, khaiTruong);

        // Món + giá từng size + định mức từng size (size lớn dùng nhiều nguyên liệu hơn)
        ChiTietMon Size(string ma, decimal gia, double heSo, params (NguyenLieu nl, decimal luong)[] ct) => new()
        {
            MaKichThuoc = ma, GiaBan = gia,
            CongThucs = ct.Select(c => new CongThucMon { NguyenLieu = c.nl, LuongSuDung = Math.Round(c.luong * (decimal)heSo, 2) }).ToList(),
        };
        Mon Mon(DanhMuc dm, string ten, KhuyenMai? km, params ChiTietMon[] sizes) =>
            new() { DanhMuc = dm, TenMon = ten, KhuyenMai = km, ChiTietMons = sizes.ToList() };

        (NguyenLieu, decimal)[] cfsd = { (hat, 20), (suaDac, 30), (da, 150) };
        (NguyenLieu, decimal)[] bacXiu = { (hat, 10), (suaTuoi, 80), (suaDac, 20), (da, 150) };
        (NguyenLieu, decimal)[] traDao = { (traDen, 8), (dao, 60), (da, 150) };
        (NguyenLieu, decimal)[] traSua = { (traDen, 8), (suaTuoi, 100), (da, 120) };

        db.Mons.AddRange(
            Mon(caPhe, "Cà phê sữa đá", null, Size("S", 25000, 0.8, cfsd), Size("M", 29000, 1, cfsd), Size("L", 35000, 1.3, cfsd)),
            Mon(caPhe, "Cà phê đen", null, Size("M", 25000, 1, (hat, 20)), Size("L", 30000, 1.3, (hat, 20))),
            Mon(caPhe, "Bạc xỉu", khaiTruong, Size("M", 32000, 1, bacXiu), Size("L", 38000, 1.3, bacXiu)),
            Mon(tra, "Trà đào cam sả", gioVang, Size("M", 45000, 1, traDao), Size("L", 52000, 1.3, traDao)),
            Mon(tra, "Trà sữa", gioVang, Size("M", 39000, 1, traSua), Size("L", 45000, 1.3, traSua)),
            Mon(banh, "Tiramisu", null, Size("M", 45000, 1, (tiramisu, 1))));

        db.Toppings.AddRange(
            new Topping { TenTopping = "Trân châu đen", GiaThem = 5000, CongThucs = { new CongThucMon { NguyenLieu = tranChau, LuongSuDung = 30 } } },
            new Topping { TenTopping = "Kem cheese", GiaThem = 10000, CongThucs = { new CongThucMon { NguyenLieu = kem, LuongSuDung = 30 } } },
            new Topping { TenTopping = "Thêm shot espresso", GiaThem = 10000, CongThucs = { new CongThucMon { NguyenLieu = hat, LuongSuDung = 18 } } });

        db.SaveChanges();
    }
}
