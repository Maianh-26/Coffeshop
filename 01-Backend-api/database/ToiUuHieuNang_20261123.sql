-- ============================================================================
-- TỐI ƯU HIỆU NĂNG — patch index cho QuanCaPheDB ĐÃ TẠO (không đổi dữ liệu, chạy lại nhiều lần không lỗi)
-- Căn cứ: đo trên bản sao 1.000.000 đơn / 2,5 triệu dòng món (23/11/2026).
--   Không có các index này, mỗi lần tải danh sách đơn / xem 1 đơn phải QUÉT CẢ BẢNG.
-- Chạy (BẮT BUỘC có -I vì index lọc / QUOTED_IDENTIFIER):
--   sqlcmd -S . -E -C -I -f 65001 -i database\ToiUuHieuNang_20261123.sql
-- Đồng bộ tương ứng: database/QuanCaPheDB.sql (DB tạo mới đã có sẵn các index này)
-- ============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
USE QuanCaPheDB;
GO

-- 1. Món của đơn: SQL Server KHÔNG tự tạo index cho khóa ngoại.
--    Dùng khi nạp món cho trang danh sách / chi tiết đơn / báo cáo món bán chạy.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChiTietDonHang_MaDonHang' AND object_id = OBJECT_ID('ChiTietDonHang'))
    CREATE NONCLUSTERED INDEX IX_ChiTietDonHang_MaDonHang ON ChiTietDonHang (MaDonHang);
GO

-- 2. Danh sách đơn mặc định (mới nhất trước) + lọc theo khoảng thời gian tạo
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DonHang_ThoiGianTao' AND object_id = OBJECT_ID('DonHang'))
    CREATE NONCLUSTERED INDEX IX_DonHang_ThoiGianTao ON DonHang (ThoiGianTao DESC, MaDonHang DESC);
GO

-- 3. Sắp xếp theo tổng tiền. Hợp đồng: cùng tổng tiền thì mã lớn hơn trước ở CẢ 2 chiều, nên cần 2 index:
--    sapXep=-tongTien (TongTien DESC, MaDonHang DESC) = đọc ngược index (a);
--    sapXep=tongTien  (TongTien ASC,  MaDonHang DESC) = đọc xuôi index (b).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DonHang_TongTien' AND object_id = OBJECT_ID('DonHang'))
    CREATE NONCLUSTERED INDEX IX_DonHang_TongTien ON DonHang (TongTien, MaDonHang);                -- (a)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DonHang_TongTien_MaGiam' AND object_id = OBJECT_ID('DonHang'))
    CREATE NONCLUSTERED INDEX IX_DonHang_TongTien_MaGiam ON DonHang (TongTien, MaDonHang DESC);    -- (b)
GO

-- 4. Lọc trạng thái thanh toán + thời gian tạo (vd. "Chờ thanh toán trong 7 ngày").
--    Có MaDonHang DESC ở cuối để khớp đúng thứ tự mặc định (ThoiGianTao DESC, MaDonHang DESC) → không phải sắp xếp lại.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DonHang_TrangThai_ThoiGianTao' AND object_id = OBJECT_ID('DonHang'))
    CREATE NONCLUSTERED INDEX IX_DonHang_TrangThai_ThoiGianTao ON DonHang (TrangThaiThanhToan, ThoiGianTao DESC, MaDonHang DESC);
ELSE IF NOT EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                    WHERE i.name = 'IX_DonHang_TrangThai_ThoiGianTao' AND i.object_id = OBJECT_ID('DonHang')
                      AND ic.key_ordinal = 3 AND COL_NAME(ic.object_id, ic.column_id) = 'MaDonHang')
    CREATE NONCLUSTERED INDEX IX_DonHang_TrangThai_ThoiGianTao ON DonHang (TrangThaiThanhToan, ThoiGianTao DESC, MaDonHang DESC)
        WITH (DROP_EXISTING = ON);   -- bản thử trước đó chỉ có 2 cột
GO

-- 5. Tìm theo số nhận món (ô tìm kiếm). Index lọc "đơn đang giữ số" sẵn có chỉ phủ đơn chưa phục vụ xong.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DonHang_SoNhanMon' AND object_id = OBJECT_ID('DonHang'))
    CREATE NONCLUSTERED INDEX IX_DonHang_SoNhanMon ON DonHang (SoNhanMon) WHERE SoNhanMon IS NOT NULL;
GO

-- 6. Báo cáo theo giờ thu tiền: thêm cột INCLUDE để báo cáo đọc thẳng từ index, không quay về bảng
--    DB chưa chạy DongBoWebAdmin_20261118.sql thì chưa có index này → tạo mới; có rồi mà chưa có INCLUDE → tạo lại.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DonHang_ThoiGianThanhToan' AND object_id = OBJECT_ID('DonHang'))
    CREATE NONCLUSTERED INDEX IX_DonHang_ThoiGianThanhToan ON DonHang (ThoiGianThanhToan)
        INCLUDE (TrangThaiThanhToan, PhuongThucThanhToan, TongTien)
        WHERE ThoiGianThanhToan IS NOT NULL;
ELSE IF NOT EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                    WHERE i.name = 'IX_DonHang_ThoiGianThanhToan' AND i.object_id = OBJECT_ID('DonHang') AND ic.is_included_column = 1)
    CREATE NONCLUSTERED INDEX IX_DonHang_ThoiGianThanhToan ON DonHang (ThoiGianThanhToan)
        INCLUDE (TrangThaiThanhToan, PhuongThucThanhToan, TongTien)
        WHERE ThoiGianThanhToan IS NOT NULL
        WITH (DROP_EXISTING = ON);
GO

-- 7. Báo cáo: đếm đơn hoàn tiền theo giờ hoàn (index lọc rất nhỏ — chỉ chứa đơn đã hoàn tiền)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DonHang_GioHoanTien' AND object_id = OBJECT_ID('DonHang'))
    CREATE NONCLUSTERED INDEX IX_DonHang_GioHoanTien ON DonHang (GioHoanTien) WHERE DaHoanTien = 1 AND GioHoanTien IS NOT NULL;
GO

PRINT N'Đã áp dụng ToiUuHieuNang_20261123.sql';
GO
