-- Chạy 1 lần trên DB QuanCaPhe đã tồn tại để khớp với code mới
-- (bỏ DonHang.SoBan, DanhMuc.ThuTuHienThi, KichThuoc.ThuTuHienThi).
-- DB tạo mới từ QuanCaPheDB.sql thì KHÔNG cần chạy file này.

-- Hàm tự xóa DEFAULT constraint rồi xóa cột
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_NAME(dc.parent_object_id)) +
               N' DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';' + CHAR(10)
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE (OBJECT_NAME(dc.parent_object_id) IN (N'DanhMuc', N'KichThuoc') AND c.name = N'ThuTuHienThi')
   OR (OBJECT_NAME(dc.parent_object_id) = N'DonHang' AND c.name = N'SoBan');
EXEC sp_executesql @sql;
GO

IF COL_LENGTH('DonHang',    'SoBan')        IS NOT NULL ALTER TABLE DonHang   DROP COLUMN SoBan;
IF COL_LENGTH('DanhMuc',    'ThuTuHienThi') IS NOT NULL ALTER TABLE DanhMuc   DROP COLUMN ThuTuHienThi;
IF COL_LENGTH('KichThuoc',  'ThuTuHienThi') IS NOT NULL ALTER TABLE KichThuoc DROP COLUMN ThuTuHienThi;
GO
