-- ============================================================================
-- ĐỒNG BỘ WEB ADMIN — patch cho QuanCaPheDB ĐÃ TẠO (không xóa dữ liệu, chạy lại nhiều lần không lỗi)
-- Căn cứ: 03-Web-Admin/docs/hop-dong-api-bo-sung.md (v1.0) và docs/ke-hoach-dong-bo-web-admin.md
--   Đợt 0: khung script + tạo lại sp_ThanhToanVaTruKho với QUOTED_IDENTIFIER ON (G3)
--   Đợt 1: A1 — DonHang.SoNhanMon, DonHang.TrangThaiPhucVu (+ CHECK, unique index lọc "đơn đang giữ số")
--   Đợt 2: A6 — DonHang.ThoiGianThanhToan, DonHang.MaNguoiThu; sp_ThanhToanVaTruKho nhận @MaNguoiThu
--   (Cột mới đặt TRƯỚC phần tạo lại sp vì sp tham chiếu tới chúng)
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

PRINT N'Đã áp dụng patch DongBoWebAdmin_20261118 (Đợt 0 + Đợt 1 + Đợt 2)';
