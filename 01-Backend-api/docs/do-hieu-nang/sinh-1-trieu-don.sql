-- Sinh 1.000.000 đơn giả vào QuanCaPheDB_Bench (KHÔNG dùng cho DB thật)
SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
USE QuanCaPheDB_Bench;
IF DB_NAME() <> 'QuanCaPheDB_Bench' THROW 50000, 'Sai DB', 1;

DECLARE @t0 datetime2 = SYSDATETIME();
DECLARE @start datetime = '2025-11-23T07:00:00';
DECLARE @user int = (SELECT MIN(MaNguoiDung) FROM NguoiDung);

DROP TABLE IF EXISTS #n;
SELECT TOP (1000000) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int) AS i
INTO #n FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c;

-- 2740 đơn/ngày, 07:00 → ~21:30, cách nhau ~19 giây
DROP TABLE IF EXISTS #o;
SELECT i,
       DATEADD(second, ((i-1) % 2740) * 19 + (CHECKSUM(i*7) & 2147483647) % 19, DATEADD(day, (i-1) / 2740, @start)) AS tao,
       (CHECKSUM(CAST(i AS bigint)*7919) & 2147483647) % 100 AS r
INTO #o FROM #n;

INSERT INTO DonHang WITH (TABLOCK)
    (MaNguoiDung, TongTien, PhuongThucThanhToan, TrangThaiThanhToan, ThoiGianTao, LyDoHuy, DaHoanTien,
     GioHoanTien, SoNhanMon, TrangThaiPhucVu, ThoiGianThanhToan, MaNguoiThu)
SELECT IIF(r % 2 = 0, NULL, @user), 0,
       IIF(r % 3 = 0, 'QR_TRANSFER', 'CASH'),
       s.tt, tao,
       IIF(s.tt = 'CANCELLED', N'Khách đổi ý', NULL), 0, NULL, NULL,
       IIF(s.tt = 'PENDING', 'WAITING', 'SERVED'),
       IIF(s.tt = 'PAID', DATEADD(minute, r % 10 + 1, tao), NULL),
       IIF(s.tt = 'PAID', @user, NULL)
FROM #o
CROSS APPLY (SELECT CASE WHEN r < 92 THEN 'PAID'
                         WHEN r < 97 THEN 'CANCELLED'
                         WHEN tao >= DATEADD(day, -2, '2026-11-23') THEN 'PENDING'
                         ELSE 'PAID' END AS tt) s
ORDER BY i;
PRINT CONCAT('DonHang xong: ', DATEDIFF(second, @t0, SYSDATETIME()), 's');

DROP TABLE IF EXISTS #menu;
SELECT ROW_NUMBER() OVER (ORDER BY cm.MaChiTietMon) - 1 AS rn, cm.MaChiTietMon,
       CAST(m.TenMon + N' (' + cm.MaKichThuoc + N')' AS nvarchar(150)) AS ten, cm.GiaBan
INTO #menu FROM ChiTietMon cm JOIN TenMon m ON m.MaTenMon = cm.MaTenMon;
DECLARE @nm int = (SELECT COUNT(*) FROM #menu);

DECLARE @base int = (SELECT MAX(MaDonHang) FROM DonHang) - 1000000;
IF @base < 0 THROW 50001, N'Chưa có 1 triệu đơn', 1;

-- 1–4 món mỗi đơn (trung bình 2,5)
INSERT INTO ChiTietDonHang WITH (TABLOCK) (MaDonHang, MaChiTietMon, TenMonLuu, DonGiaBan, SoLuong, GhiChu)
SELECT d.MaDonHang, mn.MaChiTietMon, mn.ten, mn.GiaBan,
       IIF((CHECKSUM(d.MaDonHang*13 + v.k) & 2147483647) % 5 = 0, 2, 1),
       IIF((CHECKSUM(d.MaDonHang*3 + v.k) & 2147483647) % 8 = 0, N'Ít đá', NULL)
FROM DonHang d
CROSS JOIN (VALUES (1),(2),(3),(4)) v(k)
JOIN #menu mn ON mn.rn = (CHECKSUM(d.MaDonHang*31 + v.k*101) & 2147483647) % @nm
WHERE d.MaDonHang > @base
  AND v.k <= 1 + (CHECKSUM(d.MaDonHang*31) & 2147483647) % 4
ORDER BY d.MaDonHang, v.k;
PRINT CONCAT('ChiTietDonHang xong: ', DATEDIFF(second, @t0, SYSDATETIME()), 's');

DROP TABLE IF EXISTS #tp;
SELECT ROW_NUMBER() OVER (ORDER BY MaTopping) - 1 AS rn, MaTopping, TenTopping, GiaThem INTO #tp FROM Topping;
DECLARE @nt int = (SELECT COUNT(*) FROM #tp);
DECLARE @baseCt int = (SELECT MIN(MaChiTietDon) FROM ChiTietDonHang WHERE MaDonHang > @base);

-- ~40% ly có 1 topping
INSERT INTO ChiTietDonHang_Topping WITH (TABLOCK) (MaChiTietDon, MaTopping, TenToppingLuu, DonGiaTopping)
SELECT ct.MaChiTietDon, tp.MaTopping, tp.TenTopping, tp.GiaThem
FROM ChiTietDonHang ct
JOIN #tp tp ON tp.rn = (CHECKSUM(ct.MaChiTietDon*7) & 2147483647) % @nt
WHERE ct.MaChiTietDon >= @baseCt AND (CHECKSUM(ct.MaChiTietDon*17) & 2147483647) % 10 < 4
ORDER BY ct.MaChiTietDon;
PRINT CONCAT('Topping xong: ', DATEDIFF(second, @t0, SYSDATETIME()), 's');

-- Tổng tiền = Σ (đơn giá + topping) × số lượng
UPDATE d SET TongTien = s.tong
FROM DonHang d
JOIN (SELECT ct.MaDonHang, SUM((ct.DonGiaBan + ISNULL(t.tien, 0)) * ct.SoLuong) AS tong
      FROM ChiTietDonHang ct
      LEFT JOIN (SELECT MaChiTietDon, SUM(DonGiaTopping) AS tien FROM ChiTietDonHang_Topping GROUP BY MaChiTietDon) t
             ON t.MaChiTietDon = ct.MaChiTietDon
      WHERE ct.MaDonHang > @base
      GROUP BY ct.MaDonHang) s ON s.MaDonHang = d.MaDonHang;
PRINT CONCAT('TongTien xong: ', DATEDIFF(second, @t0, SYSDATETIME()), 's');

UPDATE STATISTICS DonHang WITH FULLSCAN;
UPDATE STATISTICS ChiTietDonHang WITH FULLSCAN;
UPDATE STATISTICS ChiTietDonHang_Topping WITH FULLSCAN;
CHECKPOINT;
PRINT CONCAT('Hoàn tất: ', DATEDIFF(second, @t0, SYSDATETIME()), 's');
