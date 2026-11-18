-- ============================================================================
-- ĐỒNG BỘ WEB ADMIN — patch cho QuanCaPheDB ĐÃ TẠO (không xóa dữ liệu, chạy lại nhiều lần không lỗi)
-- Căn cứ: 03-Web-Admin/docs/hop-dong-api-bo-sung.md (v1.0) và docs/ke-hoach-dong-bo-web-admin.md
--   Đợt 0: khung script + tạo lại sp_ThanhToanVaTruKho với QUOTED_IDENTIFIER ON (G3)
--   Đợt 1: A1 — DonHang.SoNhanMon, DonHang.TrangThaiPhucVu (+ CHECK, unique index lọc "đơn đang giữ số")
--   Đợt 2: A6 — DonHang.ThoiGianThanhToan, DonHang.MaNguoiThu; sp_ThanhToanVaTruKho nhận @MaNguoiThu
--   (Cột mới đặt TRƯỚC phần tạo lại sp vì sp tham chiếu tới chúng)
--   Đợt 3: B1 — NguoiDung.DaXoa (xóa mềm) + chuyển tài khoản web đã "xóa tạm" (TrangThai = 0) thành đã xóa
--          B2 — NguoiDung.Email cho phép NULL, UNIQUE → unique index lọc
-- Chạy (BẮT BUỘC có -I vì index lọc / QUOTED_IDENTIFIER):
--   sqlcmd -S . -E -C -I -f 65001 -i database\DongBoWebAdmin_20261118.sql
-- Đồng bộ tương ứng: database/QuanCaPheDB.sql, Data/Entities.cs, Data/AppDbContext.cs
-- ============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
USE QuanCaPheDB;
GO

-- ============================================================================ ĐỢT 1 — A1
-- 1. Cột mới
IF COL_LENGTH('DonHang', 'SoNhanMon') IS NULL
    ALTER TABLE DonHang ADD SoNhanMon INT NULL; -- Số trên bảng gỗ đưa khách (1–50), NULL = chưa nhập
IF COL_LENGTH('DonHang', 'TrangThaiPhucVu') IS NULL
    ALTER TABLE DonHang ADD TrangThaiPhucVu VARCHAR(20) NOT NULL
        CONSTRAINT DF_DonHang_TrangThaiPhucVu DEFAULT 'WAITING'; -- 'WAITING' | 'PREPARING' | 'SERVED'
GO

-- 2. Dữ liệu cũ: đơn trước hôm nay đã thu / đã hủy coi như đã phục vụ
UPDATE DonHang SET TrangThaiPhucVu = 'SERVED'
WHERE TrangThaiThanhToan IN ('PAID', 'CANCELLED') AND TrangThaiPhucVu = 'WAITING'
  AND ThoiGianTao < CAST(GETDATE() AS DATE);
PRINT CONCAT(N'  Đơn cũ chuyển SERVED: ', @@ROWCOUNT);
GO

-- 3. Ràng buộc CHECK
IF OBJECT_ID('CK_DonHang_SoNhanMon', 'C') IS NULL
    ALTER TABLE DonHang ADD CONSTRAINT CK_DonHang_SoNhanMon CHECK (SoNhanMon IS NULL OR SoNhanMon BETWEEN 1 AND 50);
IF OBJECT_ID('CK_DonHang_TrangThaiPhucVu', 'C') IS NULL
    ALTER TABLE DonHang ADD CONSTRAINT CK_DonHang_TrangThaiPhucVu CHECK (TrangThaiPhucVu IN ('WAITING', 'PREPARING', 'SERVED'));
GO

-- 4. Index. "Đơn đang giữ số" = có số + chưa phục vụ xong + chưa hủy → mỗi số chỉ một đơn giữ.
--    Hủy hoặc SERVED thì đơn rơi khỏi điều kiện lọc → số tự giải phóng (vẫn lưu trên đơn).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DonHang_SoNhanMon_DangGiu' AND object_id = OBJECT_ID('DonHang'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_DonHang_SoNhanMon_DangGiu ON DonHang (SoNhanMon)
    WHERE SoNhanMon IS NOT NULL AND TrangThaiPhucVu IN ('WAITING', 'PREPARING') AND TrangThaiThanhToan <> 'CANCELLED';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DonHang_TrangThaiPhucVu' AND object_id = OBJECT_ID('DonHang'))
    CREATE NONCLUSTERED INDEX IX_DonHang_TrangThaiPhucVu ON DonHang (TrangThaiPhucVu, ThoiGianTao DESC);
GO

-- ============================================================================ ĐỢT 2 — A6
-- 1. Giờ thu tiền + người thu tiền (FK thứ hai tới NguoiDung)
IF COL_LENGTH('DonHang', 'ThoiGianThanhToan') IS NULL
    ALTER TABLE DonHang ADD ThoiGianThanhToan DATETIME NULL; -- Lúc chuyển PAID; NULL khi chưa thu
IF COL_LENGTH('DonHang', 'MaNguoiThu') IS NULL
    ALTER TABLE DonHang ADD MaNguoiThu INT NULL
        CONSTRAINT FK_DonHang_NguoiThu REFERENCES NguoiDung(MaNguoiDung); -- NULL: đơn cũ / thu tự động
GO

-- 2. Dữ liệu cũ: đơn đã thu (kể cả đã hoàn tiền) chưa có giờ thu → lấy giờ tạo đơn
UPDATE DonHang SET ThoiGianThanhToan = ThoiGianTao
WHERE ThoiGianThanhToan IS NULL AND (TrangThaiThanhToan = 'PAID' OR DaHoanTien = 1);
PRINT CONCAT(N'  Đơn cũ gán ThoiGianThanhToan = ThoiGianTao: ', @@ROWCOUNT);
GO

-- 3. Index cho báo cáo lọc theo giờ thu tiền
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DonHang_ThoiGianThanhToan' AND object_id = OBJECT_ID('DonHang'))
    CREATE NONCLUSTERED INDEX IX_DonHang_ThoiGianThanhToan ON DonHang (ThoiGianThanhToan)
    WHERE ThoiGianThanhToan IS NOT NULL;
GO

-- ============================================================================ ĐỢT 0 — G3 (+ Đợt 2: @MaNguoiThu)
-- DonHang giờ có index lọc: mọi câu lệnh ghi vào DonHang phải chạy với QUOTED_IDENTIFIER ON.
-- Stored procedure ghi nhớ thiết lập LÚC TẠO, nên tạo lại sp ngay dưới SET QUOTED_IDENTIFIER ON ở đầu file
-- (nếu sp từng được tạo không có -I, thanh toán sẽ lỗi 1934). Nội dung giống hệt QuanCaPheDB.sql.
-- Đợt 2: thêm @MaNguoiThu (mặc định NULL → lời gọi cũ chỉ truyền @MaDonHang vẫn chạy),
-- ghi ThoiGianThanhToan + MaNguoiThu trong cùng transaction với trừ kho.
CREATE OR ALTER PROCEDURE sp_ThanhToanVaTruKho
    @MaDonHang INT,
    @MaNguoiThu INT = NULL
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

        -- 2. Cập nhật trạng thái đơn hàng sang PAID + ghi giờ thu tiền, người thu tiền
        UPDATE DonHang
        SET TrangThaiThanhToan = 'PAID',
            ThoiGianThanhToan = GETDATE(),
            MaNguoiThu = @MaNguoiThu
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

-- Kiểm tra: sp phải có QUOTED_IDENTIFIER ON
IF OBJECTPROPERTY(OBJECT_ID('sp_ThanhToanVaTruKho'), 'ExecIsQuotedIdentOn') = 1
    PRINT N'  sp_ThanhToanVaTruKho: QUOTED_IDENTIFIER ON (OK)';
ELSE
    RAISERROR(N'sp_ThanhToanVaTruKho được tạo với QUOTED_IDENTIFIER OFF — chạy lại script bằng sqlcmd -I', 16, 1);
GO

-- ============================================================================ ĐỢT 3 — B1, B2
-- 1. Cột DaXoa
IF COL_LENGTH('NguoiDung', 'DaXoa') IS NULL
    ALTER TABLE NguoiDung ADD DaXoa BIT NOT NULL CONSTRAINT DF_NguoiDung_DaXoa DEFAULT 0; -- 1 = đã xóa (xóa mềm)
GO

-- 2. Bỏ UNIQUE constraint tự sinh trên Email (tên khác nhau giữa các máy → tra theo cột)
DECLARE @uq SYSNAME = (
    SELECT kc.name FROM sys.key_constraints kc
    JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE kc.type = 'UQ' AND kc.parent_object_id = OBJECT_ID('NguoiDung') AND c.name = 'Email');
IF @uq IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(400) = N'ALTER TABLE NguoiDung DROP CONSTRAINT ' + QUOTENAME(@uq);
    EXEC (@sql);
    PRINT CONCAT(N'  Đã bỏ UNIQUE constraint ', @uq, N' trên NguoiDung.Email');
END
GO

-- 3. Email cho phép NULL (không bắt buộc khi tạo tài khoản)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('NguoiDung') AND name = 'Email' AND is_nullable = 0)
    ALTER TABLE NguoiDung ALTER COLUMN Email VARCHAR(100) NULL;
GO

-- 4. Dữ liệu cũ — CHỈ MỘT LẦN: Web Admin không còn chức năng khóa, nút "Xóa" trước đây gửi PATCH trangThai=false
--    → mọi tài khoản đang TrangThai = 0 là tài khoản đã bị xóa từ web: chuyển thành đã xóa
--      và giải phóng tên đăng nhập / email (giống hệt DELETE nguoi-dung/{id}).
--    Đánh dấu "đã chuyển" bằng extended property trên bảng, ghi CÙNG transaction với UPDATE:
--    chạy lại patch về sau (kể cả sau một lần chạy lỗi giữa chừng) không đụng tới tài khoản bị khóa qua API.
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties
               WHERE major_id = OBJECT_ID('NguoiDung') AND minor_id = 0 AND name = N'DongBo_Dot3_DaChuyenTaiKhoanKhoa')
BEGIN
    -- XACT_ABORT + TRY/CATCH: UPDATE lỗi thì hủy cả transaction, KHÔNG ghi đánh dấu "đã chuyển"
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        EXEC (N'
            UPDATE NguoiDung
            SET DaXoa = 1,
                Email = NULL,
                TenDangNhap = LEFT(TenDangNhap, 50 - LEN(CONCAT(''#xoa'', MaNguoiDung))) + CONCAT(''#xoa'', MaNguoiDung)
            WHERE TrangThai = 0 AND DaXoa = 0;
            PRINT CONCAT(N''  Tài khoản đã khóa chuyển thành đã xóa: '', @@ROWCOUNT);');
        EXEC sys.sp_addextendedproperty @name = N'DongBo_Dot3_DaChuyenTaiKhoanKhoa', @value = N'Đã chuyển TrangThai=0 → DaXoa=1 (patch 20261118)',
            @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'NguoiDung';
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
    SET XACT_ABORT OFF;
END
ELSE
    PRINT N'  Bỏ qua chuyển tài khoản đã khóa (đã chuyển ở lần chạy trước)';
GO

-- 5. Email duy nhất khi có giá trị (nhiều tài khoản không có email vẫn hợp lệ)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_NguoiDung_Email' AND object_id = OBJECT_ID('NguoiDung'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_NguoiDung_Email ON NguoiDung (Email) WHERE Email IS NOT NULL;
GO

PRINT N'Đã áp dụng patch DongBoWebAdmin_20261118 (Đợt 0 + Đợt 1 + Đợt 2 + Đợt 3)';
