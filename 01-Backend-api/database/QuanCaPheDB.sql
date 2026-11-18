-- ============================================================================
-- HỆ QUẢN TRỊ CSDL: Microsoft SQL Server
-- CƠ SỞ DỮ LIỆU: QuanCaPheDB (Cập nhật: Bảng TenMon & KhuyenMai nối vào TenMon)
-- ============================================================================

USE master;
GO

-- 1. XÓA VÀ TẠO MỚI CƠ SỞ DỮ LIỆU SẠCH SẼ
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'QuanCaPheDB')
BEGIN
    ALTER DATABASE QuanCaPheDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE QuanCaPheDB;
END
GO

CREATE DATABASE QuanCaPheDB;
GO

USE QuanCaPheDB;
GO

-- ----------------------------------------------------------------------------
-- KHỐI 1: NGƯỜI DÙNG & PHÂN QUYỀN (1 BẢNG DUY NHẤT)
-- ----------------------------------------------------------------------------
CREATE TABLE NguoiDung (
    MaNguoiDung INT IDENTITY(1,1) PRIMARY KEY,
    TenDangNhap VARCHAR(50) NOT NULL UNIQUE,
    MatKhau VARCHAR(255) NOT NULL, -- Mật khẩu băm (BCrypt/Argon2)[cite: 6]
    HoTen NVARCHAR(100) NOT NULL,
    Email VARCHAR(100) NOT NULL UNIQUE, -- Khôi phục mật khẩu[cite: 6]
    VaiTro NVARCHAR(50) NOT NULL DEFAULT N'NhanVien', -- 'QuanLy' (Chủ quán) hoặc 'NhanVien' (Thu ngân/Pha chế)
    TrangThai BIT NOT NULL DEFAULT 1 -- 1: Hoạt động, 0: Khóa tài khoản
);

-- ----------------------------------------------------------------------------
-- KHỐI 2: DANH MỤC, TÊN MÓN, KÍCH CỠ, TOPPING & KHUYẾN MÃI
-- ----------------------------------------------------------------------------
CREATE TABLE DanhMuc (
    MaDanhMuc INT IDENTITY(1,1) PRIMARY KEY,
    TenDanhMuc NVARCHAR(100) NOT NULL
);

-- Bảng Khuyến mãi (tạo trước để TenMon tham chiếu)
CREATE TABLE KhuyenMai (
    MaKhuyenMai INT IDENTITY(1,1) PRIMARY KEY,
    Code VARCHAR(20) NOT NULL UNIQUE,
    PhanTramGiam DECIMAL(5,2) NOT NULL CHECK (PhanTramGiam > 0 AND PhanTramGiam <= 100),
    NgayBatDau DATETIME NOT NULL,
    NgayKetThuc DATETIME NOT NULL,
    GioBatDau TIME NULL, -- Khung giờ vàng (NULL = cả ngày)[cite: 6]
    GioKetThuc TIME NULL
);

-- Bảng TenMon (đổi từ ThucDon), KhuyenMai nối vào đây
CREATE TABLE TenMon (
    MaTenMon INT IDENTITY(1,1) PRIMARY KEY,
    MaDanhMuc INT NOT NULL,
    MaKhuyenMai INT NULL, -- Khóa ngoại gắn khuyến mãi theo từng món
    TenMon NVARCHAR(150) NOT NULL,
    HinhAnh NVARCHAR(255) NULL,
    ConHang BIT NOT NULL DEFAULT 1,  -- 1: Đang bán, 0: Tạm hết hàng trong ngày[cite: 6]
    NgungBan BIT NOT NULL DEFAULT 0, -- 1: Ngừng kinh doanh món vĩnh viễn[cite: 6]
    CONSTRAINT FK_TenMon_DanhMuc FOREIGN KEY (MaDanhMuc) REFERENCES DanhMuc(MaDanhMuc),
    CONSTRAINT FK_TenMon_KhuyenMai FOREIGN KEY (MaKhuyenMai) REFERENCES KhuyenMai(MaKhuyenMai)
);

CREATE TABLE KichThuoc (
    MaKichThuoc VARCHAR(10) PRIMARY KEY, -- 'S', 'M', 'L'
    TenKichThuoc NVARCHAR(50) NOT NULL   -- Nhỏ, Vừa, Lớn
);

CREATE TABLE ChiTietMon (
    MaChiTietMon INT IDENTITY(1,1) PRIMARY KEY,
    MaTenMon INT NOT NULL, -- Khóa ngoại tham chiếu sang TenMon
    MaKichThuoc VARCHAR(10) NOT NULL,
    GiaBan DECIMAL(18, 0) NOT NULL CHECK (GiaBan >= 0),
    DangApDung BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_TenMon_KichThuoc UNIQUE (MaTenMon, MaKichThuoc),
    CONSTRAINT FK_ChiTietMon_TenMon FOREIGN KEY (MaTenMon) REFERENCES TenMon(MaTenMon),
    CONSTRAINT FK_ChiTietMon_KichThuoc FOREIGN KEY (MaKichThuoc) REFERENCES KichThuoc(MaKichThuoc)
);

CREATE TABLE Topping (
    MaTopping INT IDENTITY(1,1) PRIMARY KEY,
    TenTopping NVARCHAR(100) NOT NULL,
    GiaThem DECIMAL(18, 0) NOT NULL DEFAULT 0 CHECK (GiaThem >= 0),
    ConHang BIT NOT NULL DEFAULT 1,
    NgungBan BIT NOT NULL DEFAULT 0
);

-- ----------------------------------------------------------------------------
-- KHỐI 3: HÓA ĐƠN & ĐẶT MÓN (BỎ KHÓA NGOẠI KHUYENMAI)
-- ----------------------------------------------------------------------------
CREATE TABLE DonHang (
    MaDonHang INT IDENTITY(1,1) PRIMARY KEY,
    MaNguoiDung INT NULL, -- NULL nếu khách tự order trên Tablet[cite: 1, 6]
    TongTien DECIMAL(18, 0) NOT NULL DEFAULT 0,
    PhuongThucThanhToan VARCHAR(20) NOT NULL DEFAULT 'CASH', -- 'CASH', 'QR_TRANSFER'[cite: 1]
    TrangThaiThanhToan VARCHAR(20) NOT NULL DEFAULT 'PENDING', -- 'PENDING', 'PAID', 'CANCELLED'[cite: 1]
    ThoiGianTao DATETIME NOT NULL DEFAULT GETDATE(),
    LyDoHuy NVARCHAR(255) NULL,   -- Quản lý hủy đơn[cite: 6]
    DaHoanTien BIT NOT NULL DEFAULT 0, --[cite: 6]
    GioHoanTien DATETIME NULL,    --[cite: 6]
    CONSTRAINT FK_DonHang_NguoiDung FOREIGN KEY (MaNguoiDung) REFERENCES NguoiDung(MaNguoiDung)
);

CREATE TABLE ChiTietDonHang (
    MaChiTietDon INT IDENTITY(1,1) PRIMARY KEY,
    MaDonHang INT NOT NULL,
    MaChiTietMon INT NOT NULL,
    TenMonLuu NVARCHAR(150) NOT NULL, -- Cố định Tên món + Size lúc đặt[cite: 6]
    DonGiaBan DECIMAL(18, 0) NOT NULL,  -- Lưu cố định đơn giá lúc đặt (đã trừ khuyến mãi nếu có)[cite: 6]
    SoLuong INT NOT NULL CHECK (SoLuong > 0),
    GhiChu NVARCHAR(255) NULL,
    CONSTRAINT FK_ChiTietDon_DonHang FOREIGN KEY (MaDonHang) REFERENCES DonHang(MaDonHang),
    CONSTRAINT FK_ChiTietDon_ChiTietMon FOREIGN KEY (MaChiTietMon) REFERENCES ChiTietMon(MaChiTietMon)
);

CREATE TABLE ChiTietDonHang_Topping (
    MaChiTietTopping INT IDENTITY(1,1) PRIMARY KEY,
    MaChiTietDon INT NOT NULL,
    MaTopping INT NOT NULL,
    TenToppingLuu NVARCHAR(100) NOT NULL, -- Cố định tên Topping lúc đặt[cite: 6]
    DonGiaTopping DECIMAL(18, 0) NOT NULL, -- Cố định giá Topping lúc đặt[cite: 6]
    CONSTRAINT UQ_ChiTietDon_Topping UNIQUE (MaChiTietDon, MaTopping), -- Mỗi topping tối đa 1 phần / ly[cite: 6]
    CONSTRAINT FK_CTDH_Topping_ChiTietDon FOREIGN KEY (MaChiTietDon) REFERENCES ChiTietDonHang(MaChiTietDon),
    CONSTRAINT FK_CTDH_Topping_Topping FOREIGN KEY (MaTopping) REFERENCES Topping(MaTopping)
);

-- ----------------------------------------------------------------------------
-- KHỐI 4: KHO NGUYÊN LIỆU & ĐỊNH MỨC TIÊU HAO
-- ----------------------------------------------------------------------------
CREATE TABLE NguyenLieu (
    MaNguyenLieu INT IDENTITY(1,1) PRIMARY KEY,
    TenNguyenLieu NVARCHAR(100) NOT NULL,
    DonViTinh NVARCHAR(20) NOT NULL, -- gram, ml, mieng, hop,...[cite: 6]
    SoLuongTon DECIMAL(12, 2) NOT NULL DEFAULT 0,
    MucCanhBaoToiThieu DECIMAL(12, 2) NOT NULL DEFAULT 10,
    DangSuDung BIT NOT NULL DEFAULT 1 --[cite: 6]
);

CREATE TABLE CongThucMon (
    MaCongThuc INT IDENTITY(1,1) PRIMARY KEY, --[cite: 6]
    MaChiTietMon INT NULL, -- Gắn với Món - Size cụ thể (bao gồm cả topping mặc định)
    MaTopping INT NULL,    -- Gắn với Topping gọi thêm[cite: 6]
    MaNguyenLieu INT NOT NULL, --[cite: 6]
    LuongSuDung DECIMAL(10, 2) NOT NULL CHECK (LuongSuDung > 0), --[cite: 6]
    CONSTRAINT FK_CongThuc_ChiTietMon FOREIGN KEY (MaChiTietMon) REFERENCES ChiTietMon(MaChiTietMon),
    CONSTRAINT FK_CongThuc_Topping FOREIGN KEY (MaTopping) REFERENCES Topping(MaTopping),
    CONSTRAINT FK_CongThuc_NguyenLieu FOREIGN KEY (MaNguyenLieu) REFERENCES NguyenLieu(MaNguyenLieu),
    CONSTRAINT CK_CongThuc_DoiTuong CHECK (
        (MaChiTietMon IS NOT NULL AND MaTopping IS NULL) OR 
        (MaChiTietMon IS NULL AND MaTopping IS NOT NULL)
    ),
    CONSTRAINT UQ_CongThuc_ChiTietMon UNIQUE (MaChiTietMon, MaNguyenLieu),
    CONSTRAINT UQ_CongThuc_Topping UNIQUE (MaTopping, MaNguyenLieu)
);

CREATE TABLE LichSuKho (
    MaLichSu INT IDENTITY(1,1) PRIMARY KEY,
    MaNguyenLieu INT NOT NULL,
    MaDonHang INT NULL,     -- NULL nếu nhập kho độc lập hoặc kiểm kê[cite: 6]
    MaNguoiDung INT NULL,   -- NULL nếu hệ thống tự sinh log trừ kho theo đơn[cite: 6]
    LoaiGiaoDich NVARCHAR(50) NOT NULL, -- 'NhapHang', 'XuatHuy', 'TieuHaoTheoDon'[cite: 6]
    SoLuongThayDoi DECIMAL(12, 2) NOT NULL, -- (+) khi nhập, (-) khi tiêu hao[cite: 6]
    ThoiGian DATETIME NOT NULL DEFAULT GETDATE(),
    GhiChu NVARCHAR(255) NULL,
    CONSTRAINT FK_LichSuKho_NguyenLieu FOREIGN KEY (MaNguyenLieu) REFERENCES NguyenLieu(MaNguyenLieu),
    CONSTRAINT FK_LichSuKho_DonHang FOREIGN KEY (MaDonHang) REFERENCES DonHang(MaDonHang),
    CONSTRAINT FK_LichSuKho_NguoiDung FOREIGN KEY (MaNguoiDung) REFERENCES NguoiDung(MaNguoiDung)
);
GO

-- ----------------------------------------------------------------------------
-- STORED PROCEDURE TỰ ĐỘNG TRỪ KHO KHI ĐƠN HÀNG THANH TOÁN (PAID)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_ThanhToanVaTruKho
    @MaDonHang INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    BEGIN TRY
        -- 1. Kiểm tra đơn hàng có tồn tại và đang ở trạng thái chờ hay không
        IF NOT EXISTS (SELECT 1 FROM DonHang WHERE MaDonHang = @MaDonHang AND TrangThaiThanhToan = 'PENDING')
        BEGIN
            RAISERROR(N'Đơn hàng không tồn tại hoặc đã được thanh toán/hủy trước đó.', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END

        -- 2. Cập nhật trạng thái đơn hàng sang PAID
        UPDATE DonHang 
        SET TrangThaiThanhToan = 'PAID'
        WHERE MaDonHang = @MaDonHang;

        -- 3. Bảng tạm gom định mức nguyên liệu cần trừ
        DECLARE @NguyenLieuTieuHao TABLE (
            MaNguyenLieu INT,
            TongLuongTieuHao DECIMAL(12, 2)
        );

        -- Gom định mức món gốc (kèm topping mặc định)
        INSERT INTO @NguyenLieuTieuHao (MaNguyenLieu, TongLuongTieuHao)
        SELECT 
            ctm.MaNguyenLieu,
            SUM(ctm.LuongSuDung * ctd.SoLuong)
        FROM ChiTietDonHang ctd
        INNER JOIN CongThucMon ctm ON ctd.MaChiTietMon = ctm.MaChiTietMon
        WHERE ctd.MaDonHang = @MaDonHang
        GROUP BY ctm.MaNguyenLieu;

        -- Gom định mức Topping gọi thêm
        INSERT INTO @NguyenLieuTieuHao (MaNguyenLieu, TongLuongTieuHao)
        SELECT 
            ctm.MaNguyenLieu,
            SUM(ctm.LuongSuDung * ctd.SoLuong)
        FROM ChiTietDonHang ctd
        INNER JOIN ChiTietDonHang_Topping ctt ON ctd.MaChiTietDon = ctt.MaChiTietDon
        INNER JOIN CongThucMon ctm ON ctt.MaTopping = ctm.MaTopping
        WHERE ctd.MaDonHang = @MaDonHang
        GROUP BY ctm.MaNguyenLieu;

        -- 4. Ghi nhận nhật ký trừ kho vào LichSuKho
        INSERT INTO LichSuKho (MaNguyenLieu, MaDonHang, MaNguoiDung, LoaiGiaoDich, SoLuongThayDoi, ThoiGian, GhiChu)
        SELECT 
            MaNguyenLieu,
            @MaDonHang,
            NULL,
            N'TieuHaoTheoDon',
            -SUM(TongLuongTieuHao),
            GETDATE(),
            CONCAT(N'Tự động trừ kho theo đơn hàng #', @MaDonHang)
        FROM @NguyenLieuTieuHao
        GROUP BY MaNguyenLieu;

        -- 5. Trừ số lượng tồn thực tế trong bảng NguyenLieu
        UPDATE nl
        SET nl.SoLuongTon = nl.SoLuongTon - th.TongTru
        FROM NguyenLieu nl
        INNER JOIN (
            SELECT MaNguyenLieu, SUM(TongLuongTieuHao) AS TongTru
            FROM @NguyenLieuTieuHao
            GROUP BY MaNguyenLieu
        ) th ON nl.MaNguyenLieu = th.MaNguyenLieu;

        COMMIT TRANSACTION;
        PRINT N'Thanh toán thành công và đã tự động trừ kho!';
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
    END CATCH
END;
GO