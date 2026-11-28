-- ============================================================================
-- GIỜ VIỆT NAM CHO MỌI MỐC THỜI GIAN (28/11/2026)
-- Chạy cho CSDL đang có (DB tạo mới từ QuanCaPheDB.sql đã có sẵn). Chạy lại nhiều lần không hỏng.
--   sqlcmd -S . -E -C -I -f 65001 -d QuanCaPheDB -i database\GioVietNam_20261128.sql
--
-- Vì sao: GETDATE() trả giờ theo múi giờ của máy chủ SQL. Máy chủ chạy UTC (cloud) → giờ thu tiền, lịch sử kho
-- lệch 7 giờ so với giờ tạo đơn do API ghi (API đã dùng Common/GioViet). Thay bằng DATEADD(HOUR, 7, SYSUTCDATETIME()).
--   1. Default của DonHang.ThoiGianTao, LichSuKho.ThoiGian → giờ Việt Nam (đặt tên DF_... để lần sau sửa được).
--   2. Tạo lại sp_ThanhToanVaTruKho: ThoiGianThanhToan và LichSuKho.ThoiGian dùng @Now giờ Việt Nam.
-- Lưu ý: chạy script này SAU DongBoWebAdmin_20261118.sql (script cũ đó còn tạo thủ tục với GETDATE()).
-- ============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Chặn chạy nhầm CSDL (quên -d → thường là master)
IF OBJECT_ID('dbo.DonHang') IS NULL OR OBJECT_ID('dbo.LichSuKho') IS NULL
BEGIN
    RAISERROR(N'Không thấy bảng DonHang / LichSuKho — chạy script với -d <tên CSDL quán>.', 16, 1);
    SET NOEXEC ON;   -- bỏ qua mọi lệnh phía sau
END
GO

-- 1. Default: bỏ default cũ (tên do SQL Server tự sinh) rồi tạo lại với tên cố định
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_NAME(dc.parent_object_id)) + N' DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE (OBJECT_NAME(dc.parent_object_id) = 'DonHang' AND c.name = 'ThoiGianTao')
   OR (OBJECT_NAME(dc.parent_object_id) = 'LichSuKho' AND c.name = 'ThoiGian');
EXEC sp_executesql @sql;

ALTER TABLE DonHang ADD CONSTRAINT DF_DonHang_ThoiGianTao DEFAULT (DATEADD(HOUR, 7, SYSUTCDATETIME())) FOR ThoiGianTao;
ALTER TABLE LichSuKho ADD CONSTRAINT DF_LichSuKho_ThoiGian DEFAULT (DATEADD(HOUR, 7, SYSUTCDATETIME())) FOR ThoiGian;
PRINT N'  Default DonHang.ThoiGianTao, LichSuKho.ThoiGian: giờ Việt Nam';
GO

-- 2. Thủ tục thu tiền + trừ kho (giống QuanCaPheDB.sql)
CREATE OR ALTER PROCEDURE sp_ThanhToanVaTruKho
    @MaDonHang INT,
    @MaNguoiThu INT = NULL -- Người bấm thu tiền (API truyền người đang đăng nhập)
AS
BEGIN
    SET NOCOUNT ON;
    -- Giờ Việt Nam (UTC+7), không phụ thuộc múi giờ của máy chủ SQL
    DECLARE @Now DATETIME = DATEADD(HOUR, 7, SYSUTCDATETIME());
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
            ThoiGianThanhToan = @Now,
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
            @Now,
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

IF OBJECTPROPERTY(OBJECT_ID('sp_ThanhToanVaTruKho'), 'ExecIsQuotedIdentOn') = 1
    PRINT N'  sp_ThanhToanVaTruKho: giờ Việt Nam, QUOTED_IDENTIFIER ON (OK)';
ELSE
    RAISERROR(N'sp_ThanhToanVaTruKho được tạo với QUOTED_IDENTIFIER OFF — chạy lại script bằng sqlcmd -I', 16, 1);
GO
