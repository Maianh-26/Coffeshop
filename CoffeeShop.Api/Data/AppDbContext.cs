using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Data;

/// <summary>
/// Database-first: schema do database/QuanCaPheDB.sql tạo ra.
/// DbContext chỉ khai báo ánh xạ, KHÔNG tự tạo/sửa bảng.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<NguoiDung> NguoiDungs => Set<NguoiDung>();
    public DbSet<DanhMuc> DanhMucs => Set<DanhMuc>();
    public DbSet<KhuyenMai> KhuyenMais => Set<KhuyenMai>();
    public DbSet<Mon> Mons => Set<Mon>();
    public DbSet<KichThuoc> KichThuocs => Set<KichThuoc>();
    public DbSet<ChiTietMon> ChiTietMons => Set<ChiTietMon>();
    public DbSet<Topping> Toppings => Set<Topping>();
    public DbSet<DonHang> DonHangs => Set<DonHang>();
    public DbSet<ChiTietDonHang> ChiTietDonHangs => Set<ChiTietDonHang>();
    public DbSet<ChiTietDonHangTopping> ChiTietDonHangToppings => Set<ChiTietDonHangTopping>();
    public DbSet<NguyenLieu> NguyenLieus => Set<NguyenLieu>();
    public DbSet<CongThucMon> CongThucMons => Set<CongThucMon>();
    public DbSet<LichSuKho> LichSuKhos => Set<LichSuKho>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<NguoiDung>(e =>
        {
            e.ToTable("NguoiDung").HasKey(x => x.MaNguoiDung);
            e.Property(x => x.TenDangNhap).HasMaxLength(50).IsUnicode(false);
            e.Property(x => x.MatKhau).HasMaxLength(255).IsUnicode(false);
            e.Property(x => x.Email).HasMaxLength(100).IsUnicode(false);
        });

        b.Entity<DanhMuc>(e => e.ToTable("DanhMuc").HasKey(x => x.MaDanhMuc));

        b.Entity<KhuyenMai>(e =>
        {
            e.ToTable("KhuyenMai").HasKey(x => x.MaKhuyenMai);
            e.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.PhanTramGiam).HasColumnType("decimal(5,2)");
            e.Property(x => x.NgayBatDau).HasColumnType("datetime");
            e.Property(x => x.NgayKetThuc).HasColumnType("datetime");
        });

        b.Entity<Mon>(e =>
        {
            e.ToTable("TenMon").HasKey(x => x.MaTenMon);
            e.HasOne(x => x.DanhMuc).WithMany(d => d.Mons).HasForeignKey(x => x.MaDanhMuc);
            e.HasOne(x => x.KhuyenMai).WithMany().HasForeignKey(x => x.MaKhuyenMai);
        });

        b.Entity<KichThuoc>(e =>
        {
            e.ToTable("KichThuoc").HasKey(x => x.MaKichThuoc);
            e.Property(x => x.MaKichThuoc).HasMaxLength(10).IsUnicode(false);
        });

        b.Entity<ChiTietMon>(e =>
        {
            e.ToTable("ChiTietMon").HasKey(x => x.MaChiTietMon);
            e.Property(x => x.MaKichThuoc).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.GiaBan).HasColumnType("decimal(18,0)");
            e.HasOne(x => x.Mon).WithMany(m => m.ChiTietMons).HasForeignKey(x => x.MaTenMon);
            e.HasOne(x => x.KichThuoc).WithMany().HasForeignKey(x => x.MaKichThuoc);
            e.HasMany(x => x.CongThucs).WithOne().HasForeignKey(c => c.MaChiTietMon);
        });

        b.Entity<Topping>(e =>
        {
            e.ToTable("Topping").HasKey(x => x.MaTopping);
            e.Property(x => x.GiaThem).HasColumnType("decimal(18,0)");
            e.HasMany(x => x.CongThucs).WithOne().HasForeignKey(c => c.MaTopping);
        });

        b.Entity<DonHang>(e =>
        {
            e.ToTable("DonHang").HasKey(x => x.MaDonHang);
            e.Property(x => x.TongTien).HasColumnType("decimal(18,0)");
            e.Property(x => x.PhuongThucThanhToan).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.TrangThaiThanhToan).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.ThoiGianTao).HasColumnType("datetime");
            e.Property(x => x.GioHoanTien).HasColumnType("datetime");
            e.HasOne(x => x.NguoiDung).WithMany().HasForeignKey(x => x.MaNguoiDung);
            e.HasMany(x => x.ChiTiets).WithOne().HasForeignKey(c => c.MaDonHang);
        });

        b.Entity<ChiTietDonHang>(e =>
        {
            e.ToTable("ChiTietDonHang").HasKey(x => x.MaChiTietDon);
            e.Property(x => x.DonGiaBan).HasColumnType("decimal(18,0)");
            e.HasMany(x => x.Toppings).WithOne().HasForeignKey(t => t.MaChiTietDon);
        });

        b.Entity<ChiTietDonHangTopping>(e =>
        {
            e.ToTable("ChiTietDonHang_Topping").HasKey(x => x.MaChiTietTopping);
            e.Property(x => x.DonGiaTopping).HasColumnType("decimal(18,0)");
        });

        b.Entity<NguyenLieu>(e =>
        {
            e.ToTable("NguyenLieu").HasKey(x => x.MaNguyenLieu);
            e.Property(x => x.SoLuongTon).HasColumnType("decimal(12,2)");
            e.Property(x => x.MucCanhBaoToiThieu).HasColumnType("decimal(12,2)");
        });

        b.Entity<CongThucMon>(e =>
        {
            e.ToTable("CongThucMon").HasKey(x => x.MaCongThuc);
            e.Property(x => x.LuongSuDung).HasColumnType("decimal(10,2)");
            e.HasOne(x => x.NguyenLieu).WithMany().HasForeignKey(x => x.MaNguyenLieu);
        });

        b.Entity<LichSuKho>(e =>
        {
            e.ToTable("LichSuKho").HasKey(x => x.MaLichSu);
            e.Property(x => x.SoLuongThayDoi).HasColumnType("decimal(12,2)");
            e.Property(x => x.ThoiGian).HasColumnType("datetime");
            e.HasOne(x => x.NguyenLieu).WithMany().HasForeignKey(x => x.MaNguyenLieu);
        });
    }
}
