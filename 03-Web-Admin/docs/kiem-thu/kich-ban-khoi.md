# Dữ liệu mẫu và kịch bản khói

> Dùng sau **mỗi module** của `docs/ke-hoach-cai-tien-chuc-nang.md` (tiêu chí 1.7) để chắc chắn phần đã sửa không làm hỏng phần khác.
> Mọi thao tác chạy trên CSDL riêng **`QuanCaPheDB_KiemThu`** — không đụng `QuanCaPheDB` thật (hai script đều từ chối tên đó).

## 1. Chuẩn bị (khoảng 2 phút)

Chạy trong PowerShell, từ thư mục `03-Web-Admin/tests/du-lieu-mau`:

1. Tạo lại CSDL kiểm thử từ script gốc của backend (xóa sạch dữ liệu kiểm thử cũ):

   ```powershell
   .\tao-db-kiem-thu.ps1
   ```

2. Chạy backend trỏ vào CSDL kiểm thử, cổng **3100** (khác cổng 3000 của backend thường dùng). Backend tự tạo tài khoản mẫu, thực đơn, kho, khuyến mãi khi CSDL trống:

   ```powershell
   $env:ConnectionStrings__QuanCaPheDB = "Server=.;Database=QuanCaPheDB_KiemThu;Trusted_Connection=True;TrustServerCertificate=True"; dotnet run --project ..\..\..\01-Backend-api --urls http://localhost:3100 --no-launch-profile
   ```

3. Ở cửa sổ khác, sinh 30 đơn mẫu qua API — **chạy trong cùng ngày, ngay trước khi kiểm thử** (các con số "Hôm nay" bên dưới tính theo ngày sinh dữ liệu). Script dừng nếu CSDL kiểm thử đã có đơn hoặc backend không ghi vào CSDL kiểm thử:

   ```powershell
   .\tao-don-mau.ps1 -Api http://localhost:3100/api/v1/
   ```

   | Nhóm | Số đơn |
   |---|---|
   | Chờ thu · Chờ phục vụ (3 đơn có số nhận món, 2 đơn khách chọn chuyển khoản) | 6 |
   | Chờ thu · Đang chuẩn bị | 4 |
   | Đã thu · Chờ phục vụ | 4 |
   | Đã thu · Đang chuẩn bị (chuyển khoản) | 2 |
   | Đã thu · Đã phục vụ (tiền mặt / chuyển khoản xen kẽ) | 6 |
   | Hủy, không thu | 3 |
   | Hủy, có hoàn tiền | 2 |
   | **Đơn treo**: chờ thu, giờ tạo lùi về hôm qua (mã 1, 2, 3) | 3 |

4. Chạy web admin trỏ vào backend kiểm thử: tạm sửa `wwwroot/appsettings.json` → `"BaseUrl": "http://localhost:3100/"` (**không commit** thay đổi này), rồi `dotnet run --project CoffeeShop.Admin.csproj` trong `03-Web-Admin` và mở http://localhost:5101.

Kiểm tra nhanh số đơn theo trạng thái:

```powershell
sqlcmd -S . -E -C -d QuanCaPheDB_KiemThu -W -Q "SELECT TrangThaiThanhToan, TrangThaiPhucVu, DaHoanTien, COUNT(*) AS SoDon FROM DonHang GROUP BY TrangThaiThanhToan, TrangThaiPhucVu, DaHoanTien"
```

## 2. Kịch bản khói (khoảng 10 phút)

Đánh dấu từng bước; bước nào sai thì dừng, ghi vào "Sổ việc phát sinh" của kế hoạch.

- [ ] **Test tự động:** `dotnet test CoffeeShop.Admin.sln` — tất cả qua. `dotnet build` web và backend — 0 lỗi, 0 cảnh báo.
- [ ] **Đăng nhập:** `quanly` → đủ 6 mục menu; đăng xuất, đăng nhập `nhanvien` → chỉ có Đơn hàng, mở thẳng `/products` → "Không có quyền truy cập".
- [ ] **Đơn hàng (Quản lý):** lọc mặc định Hôm nay hiện 27 đơn (30 đơn mẫu trừ 3 đơn treo; trước bước "Luồng một đơn"); banner vàng báo **3 đơn chưa thu** từ trước hôm nay, bấm "Xem các đơn này" → đúng đơn 1, 2, 3.
- [ ] **Luồng một đơn:** Tạo đơn tại quầy (1 món có topping + ghi chú) → gán số nhận món → Đang chuẩn bị → Thu tiền mặt → Đã phục vụ → xem trước hóa đơn: tiêu đề "HÓA ĐƠN THANH TOÁN", đúng món, topping, ghi chú, tổng tiền.
- [ ] **Phiếu tạm tính:** mở một đơn chờ thu → nút "In phiếu tạm tính", tiêu đề phiếu "PHIẾU TẠM TÍNH".
- [ ] **Hủy đơn:** hủy một đơn chờ thu kèm lý do → trạng thái "Đã hủy", thanh toán "Không thu".
- [ ] **Thực đơn:** thêm một món có công thức → món hiện trong hộp tạo đơn tại quầy với đúng giá (kể cả khuyến mãi nếu món thuộc khuyến mãi đang chạy).
- [ ] **Kho:** nguyên liệu của món ở bước "Luồng một đơn" giảm đúng định mức; lịch sử kho có dòng tương ứng.
- [ ] **Tổng quan:** "Hôm nay" — doanh thu = tổng các đơn đã thu hôm nay (so với SQL dưới); "Xuất Excel" tải được file, mở được bằng Excel, 4 sheet.
- [ ] **Màn hình:** mở trang Đơn hàng ở 375px, 768px, máy tính — không vỡ bố cục; console trình duyệt không có lỗi đỏ.

Doanh thu hôm nay theo SQL (so với thẻ "Doanh thu thuần" trên Tổng quan):

```powershell
sqlcmd -S . -E -C -d QuanCaPheDB_KiemThu -W -Q "SELECT COUNT(*) AS SoDon, SUM(TongTien) AS DoanhThu FROM DonHang WHERE TrangThaiThanhToan = 'PAID' AND ThoiGianThanhToan >= CAST(DATEADD(HOUR, 7, SYSUTCDATETIME()) AS date)"
```

> Việc 3.4 (đối soát tồn kho với lịch sử kho) và 6.4 (đối soát báo cáo) của kế hoạch sẽ thêm câu SQL đối soát đầy đủ vào mục này.

## 3. Dọn dẹp

- Trả `wwwroot/appsettings.json` về `http://localhost:3000/` (hoặc `git checkout -- wwwroot/appsettings.json`).
- Tắt backend kiểm thử (Ctrl+C). CSDL `QuanCaPheDB_KiemThu` để lại cũng được; lần sau `tao-db-kiem-thu.ps1` tạo lại từ đầu.
