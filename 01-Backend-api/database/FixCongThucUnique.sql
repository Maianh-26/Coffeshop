-- ============================================================================
-- SỬA LỖI RÀNG BUỘC UNIQUE BẢNG CongThucMon (chạy cho DB QuanCaPheDB ĐÃ TẠO TỪ BẢN SCRIPT GỐC)
-- Không xóa dữ liệu. Chạy 1 lần:
--   sqlcmd -S .\SQLEXPRESS -E -C -I -i database\FixCongThucUnique.sql
-- ============================================================================
-- Lý do: UNIQUE (MaTopping, MaNguyenLieu) coi NULL = NULL, nên 2 món khác nhau (MaTopping NULL)
-- không thể cùng dùng 1 nguyên liệu. Filtered unique index chỉ kiểm tra khi cột KHÁC NULL.
SET QUOTED_IDENTIFIER ON;
GO
USE QuanCaPheDB;
GO
IF OBJECT_ID('UQ_CongThuc_ChiTietMon', 'UQ') IS NOT NULL ALTER TABLE CongThucMon DROP CONSTRAINT UQ_CongThuc_ChiTietMon;
IF OBJECT_ID('UQ_CongThuc_Topping', 'UQ') IS NOT NULL ALTER TABLE CongThucMon DROP CONSTRAINT UQ_CongThuc_Topping;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_CongThuc_ChiTietMon' AND object_id = OBJECT_ID('CongThucMon'))
    CREATE UNIQUE INDEX UQ_CongThuc_ChiTietMon ON CongThucMon(MaChiTietMon, MaNguyenLieu) WHERE MaChiTietMon IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_CongThuc_Topping' AND object_id = OBJECT_ID('CongThucMon'))
    CREATE UNIQUE INDEX UQ_CongThuc_Topping ON CongThucMon(MaTopping, MaNguyenLieu) WHERE MaTopping IS NOT NULL;
GO
PRINT N'Đã sửa ràng buộc UNIQUE của CongThucMon';
