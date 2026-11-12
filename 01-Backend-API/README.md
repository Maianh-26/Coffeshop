# API Hệ thống đặt món thông minh – Quán cà phê

**C# · ASP.NET Core 8 · SQL Server (QuanCaPheDB) · Kiến trúc API-first**

```
CoffeeShop.Api/                   ← Thư mục gốc của project (mở thư mục này trong VS Code)
├── Program.cs                    ← Cấu hình: SQL Server, JWT, Swagger UI đọc openapi.yaml
├── appsettings.json              ← Connection string, khóa JWT
├── CoffeeShop.Api.http           ← Mọi request mẫu (JSON vào) — bấm Send để chạy
├── openapi/openapi.yaml          ← HỢP ĐỒNG API (viết TRƯỚC, nguồn chuẩn duy nhất)
├── database/
│   ├── QuanCaPheDB.sql           ← Script tạo CSDL (đã sửa 1 lỗi UNIQUE, xem mục 6)
│   ├── BoSoBan_ThuTuHienThi.sql  ← Chạy 1 lần nếu DB cũ còn cột SoBan / ThuTuHienThi
│   └── FixCongThucUnique.sql     ← Chỉ dùng nếu DB đã tạo từ bản script gốc
├── Data/                         ← Entity ánh xạ 1-1 với các bảng + DbContext + dữ liệu mẫu
├── Dtos/                         ← JSON đầu vào / đầu ra (khớp openapi.yaml)
├── Services/                     ← Nghiệp vụ: đặt món, thanh toán (gọi stored procedure), kho, gợi ý
├── Controllers/                  ← Endpoint
└── Common/                       ← JWT, BCrypt, xử lý lỗi, mapper
```

---

## 1. Hướng dẫn chạy – từng bước

### Bước 1. Cài phần mềm (bỏ qua nếu đã có)
- **.NET 8 SDK**: https://dotnet.microsoft.com/download/dotnet/8.0 → kiểm tra: `dotnet --version`
- **SQL Server Express** + **SSMS** (SQL Server Management Studio)
- (Khuyên dùng) **Visual Studio 2022** hoặc **VS Code** + extension *C# Dev Kit* và *REST Client*

### Bước 2. Tạo database `QuanCaPheDB`
**Cách A – SSMS:** mở `database/QuanCaPheDB.sql` → bấm **Execute (F5)**.

**Cách B – dòng lệnh** (mở PowerShell tại thư mục dự án):
```powershell
sqlcmd -S .\SQLEXPRESS -E -C -I -f 65001 -i database\QuanCaPheDB.sql
```
> ⚠️ Script **xóa và tạo lại** `QuanCaPheDB` nếu đã tồn tại. Có dữ liệu thật thì sao lưu trước.
> Nếu DB của bạn đã tạo từ **bản script gốc** và không muốn xóa → chỉ chạy:
> `sqlcmd -S .\SQLEXPRESS -E -C -I -i database\FixCongThucUnique.sql`

### Bước 3. Kiểm tra chuỗi kết nối
Mở `CoffeeShop.Api/appsettings.json`:
```json
"ConnectionStrings": {
  "QuanCaPheDB": "Server=.\\SQLEXPRESS;Database=QuanCaPheDB;Trusted_Connection=True;TrustServerCertificate=True"
}
```
- Dùng SQL Server bản đầy đủ (không phải Express) → đổi `.\\SQLEXPRESS` thành `.` hoặc `localhost`.
- Đăng nhập bằng tài khoản `sa` → thay `Trusted_Connection=True` bằng `User Id=sa;Password=matkhau`.

### Bước 4. Chạy API
```powershell
cd CoffeeShop.Api
dotnet run
```
Hoặc mở `CoffeeShop.Api/CoffeeShop.Api.csproj` bằng Visual Studio → nhấn **F5**.

Lần chạy đầu, nếu bảng `NguoiDung` trống, API tự thêm **dữ liệu mẫu**: 2 tài khoản, 3 kích thước, 3 danh mục,
6 món (kèm giá từng size + công thức), 3 topping, 9 nguyên liệu, 2 khuyến mãi.
(Tắt bằng `"SeedSampleData": false` trong appsettings.json.)

| Tài khoản | Mật khẩu | Vai trò |
|---|---|---|
| `quanly` | `QuanLy@123` | QuanLy |
| `nhanvien` | `NhanVien@123` | NhanVien |

### Bước 5. Mở tài liệu & test
- **Swagger UI**: http://localhost:3000/docs (hiển thị trực tiếp file `openapi.yaml`)
- Kiểm tra DB: http://localhost:3000/api/v1/health → `"database": "connected"`

**Test trong Swagger:** `POST /auth/dang-nhap` → *Try it out* → Execute → copy `token`
→ bấm nút **Authorize** 🔒 góc trên → dán token → giờ gọi được các API cần quyền.

**Test bằng file .http:** mở `CoffeeShop.Api/CoffeeShop.Api.http` → bấm *Send Request* trên từng request
(dán token vào `@tokenQL`, `@tokenNV`).

### Bước 6. Kịch bản demo (bảo vệ đồ án)
1. `GET /mon?coTheBan=true` – menu, giá từng size, **giá sau khuyến mãi tự tính**
2. `POST /don-hang` (không token) – khách tự đặt món, có topping
3. `POST /don-hang` với `soLuong` lớn món Tiramisu → **422 HET_NGUYEN_LIEU** (hệ thống chặn đơn không pha được)
4. `POST /don-hang/{id}/thanh-toan` – gọi **sp_ThanhToanVaTruKho** → xem `canhBaoKho`
5. `GET /lich-su-kho` – thấy các dòng `TieuHaoTheoDon` do stored procedure ghi
6. `GET /nguyen-lieu?sapHet=true` – nguyên liệu cần nhập + **dự báo số ngày còn dùng**
7. `GET /bao-cao/doanh-thu`, `/bao-cao/gio-cao-diem` – báo cáo cho chủ quán

---

## 2. Quy trình API-first (cách làm khi thêm/sửa tính năng)

```
① Sửa openapi/openapi.yaml  →  ② Review hợp đồng (Swagger /docs)  →  ③ Frontend dùng hợp đồng để làm song song
         ↓
④ Viết DTO (Dtos/) đúng schema  →  ⑤ Viết Service + Controller  →  ⑥ Test bằng .http / Swagger đúng như hợp đồng
```
- Hợp đồng là nguồn chuẩn: Swagger UI **không sinh từ code** mà đọc thẳng `openapi.yaml`.
- Tên trường JSON = tên cột DB ở dạng camelCase (`MaTenMon` → `maTenMon`) để dễ đối chiếu.
- Gửi trường lạ trong body → **400** (giống `additionalProperties: false`).
- Mọi lỗi cùng 1 định dạng:
```json
{ "error": { "code": "HET_NGUYEN_LIEU", "message": "Không đủ nguyên liệu để pha chế đơn này",
             "details": [ { "tenNguyenLieu": "Bánh tiramisu", "canDung": 5, "conTon": 3, "donViTinh": "mieng" } ] } }
```

---

## 3. Danh sách endpoint (prefix `/api/v1`)

🌐 = công khai (tablet khách) · 👤 = NhanVien + QuanLy · 👑 = chỉ QuanLy

| Nhóm | Method & đường dẫn | Quyền | Bảng DB |
|---|---|---|---|
| Hệ thống | `GET /health` | 🌐 | |
| Xác thực | `POST /auth/dang-nhap` · `GET /auth/toi` · `POST /auth/doi-mat-khau` | 🌐/👤 | NguoiDung |
| Người dùng | `GET, POST /nguoi-dung` · `GET, PATCH /nguoi-dung/{id}` | 👑 | NguoiDung |
| Danh mục | `GET /danh-muc` · `GET /danh-muc/{id}` | 🌐 | DanhMuc |
| | `POST /danh-muc` · `PUT, DELETE /danh-muc/{id}` | 👑 | |
| Kích thước | `GET /kich-thuoc` 🌐 · `POST /kich-thuoc` · `PUT, DELETE /kich-thuoc/{ma}` 👑 | | KichThuoc |
| Món | `GET /mon` · `GET /mon/{id}` | 🌐 | TenMon, ChiTietMon |
| | `POST /mon` · `PATCH /mon/{id}` · `DELETE /mon/{id}` · `PUT /mon/{id}/kich-thuoc` | 👑 | |
| | `PATCH /mon/{id}/con-hang` | 👤 | |
| Công thức | `GET /chi-tiet-mon/{id}/cong-thuc` 👤 · `PUT …` 👑 | | CongThucMon |
| Topping | `GET /topping` · `GET /topping/{id}` | 🌐 | Topping |
| | `POST /topping` · `PUT, DELETE /topping/{id}` · `PUT /topping/{id}/cong-thuc` | 👑 | |
| | `PATCH /topping/{id}/con-hang` · `GET /topping/{id}/cong-thuc` | 👤 | |
| Khuyến mãi | `GET /khuyen-mai` · `GET /khuyen-mai/{id}` 🌐 · `POST`, `PUT`, `DELETE` 👑 | | KhuyenMai, TenMon |
| Đơn hàng | `POST /don-hang` | 🌐 | DonHang, ChiTietDonHang, ChiTietDonHang_Topping |
| | `GET /don-hang` · `GET /don-hang/{id}` · `POST /don-hang/{id}/thanh-toan` | 👤 | + **sp_ThanhToanVaTruKho** |
| | `POST /don-hang/{id}/huy` | 👑 | |
| Kho | `GET /nguyen-lieu` · `GET /nguyen-lieu/{id}` · `POST …/nhap-kho` · `POST …/xuat-huy` · `GET /lich-su-kho` | 👑 | NguyenLieu, LichSuKho |
| | `POST /nguyen-lieu` · `PATCH, DELETE /nguyen-lieu/{id}` | 👑 | |
| Báo cáo | `GET /bao-cao/doanh-thu` · `/bao-cao/mon-ban-chay` · `/bao-cao/gio-cao-diem` | 👑 | DonHang |

JSON vào/ra chi tiết của **từng** endpoint: xem Swagger `/docs` (có ví dụ) hoặc `CoffeeShop.Api.http`.

---

## 4. JSON mẫu của luồng chính

### Đăng nhập — `POST /auth/dang-nhap`
```json
// Vào
{ "tenDangNhap": "quanly", "matKhau": "QuanLy@123" }
// Ra 200
{ "token": "eyJhbGciOi...", "hetHan": "2026-11-08T12:00:00+07:00",
  "nguoiDung": { "maNguoiDung": 1, "tenDangNhap": "quanly", "hoTen": "Chủ quán",
                 "email": "quanly@coffee.vn", "vaiTro": "QuanLy", "trangThai": true } }
```

### Menu — `GET /mon?coTheBan=true`
```json
{ "data": [ {
    "maTenMon": 3, "maDanhMuc": 1, "tenDanhMuc": "Cà phê", "tenMon": "Bạc xỉu", "hinhAnh": null,
    "conHang": true, "ngungBan": false, "coTheBan": true,
    "khuyenMai": { "maKhuyenMai": 2, "code": "KHAITRUONG10", "phanTramGiam": 10, "dangApDung": true,
                   "gioBatDau": null, "gioKetThuc": null },
    "kichThuoc": [
      { "maChiTietMon": 6, "maKichThuoc": "M", "tenKichThuoc": "Vừa", "giaBan": 32000, "giaSauGiam": 28800,
        "dangApDung": true, "duNguyenLieu": true },
      { "maChiTietMon": 7, "maKichThuoc": "L", "tenKichThuoc": "Lớn", "giaBan": 38000, "giaSauGiam": 34200,
        "dangApDung": true, "duNguyenLieu": true } ] } ],
  "meta": { "page": 1, "limit": 20, "total": 6, "totalPages": 1 } }
```

### Đặt món — `POST /don-hang`
```json
// Vào (không cần token nếu khách tự order trên tablet)
{ "phuongThucThanhToan": "CASH",
  "chiTiet": [ { "maChiTietMon": 7, "soLuong": 2, "ghiChu": "Ít đá", "toppings": [3] },
               { "maChiTietMon": 2, "soLuong": 1 } ] }
// Ra 201
{ "maDonHang": 1, "maNguoiDung": null, "tenNhanVien": null, "tongTien": 117400,
  "phuongThucThanhToan": "CASH", "trangThaiThanhToan": "PENDING", "thoiGianTao": "2026-11-08T00:03:51",
  "lyDoHuy": null, "daHoanTien": false, "gioHoanTien": null,
  "chiTiet": [
    { "maChiTietDon": 1, "maChiTietMon": 7, "tenMonLuu": "Bạc xỉu - Lớn", "donGiaBan": 34200, "soLuong": 2,
      "ghiChu": "Ít đá", "toppings": [ { "maTopping": 3, "tenToppingLuu": "Thêm shot espresso", "donGiaTopping": 10000 } ],
      "thanhTien": 88400 },
    { "maChiTietDon": 2, "maChiTietMon": 2, "tenMonLuu": "Cà phê sữa đá - Vừa", "donGiaBan": 29000, "soLuong": 1,
      "ghiChu": null, "toppings": [], "thanhTien": 29000 } ] }
```

### Thanh toán — `POST /don-hang/1/thanh-toan`
```json
// Vào
{ "phuongThucThanhToan": "CASH", "tienKhachDua": 200000 }
// Ra 200
{ "donHang": { "maDonHang": 1, "trangThaiThanhToan": "PAID", "tongTien": 117400, "...": "..." },
  "tienThua": 82600,
  "canhBaoKho": [ { "maNguyenLieu": 9, "tenNguyenLieu": "Bánh tiramisu", "soLuongTon": 3,
                    "mucCanhBaoToiThieu": 5, "donViTinh": "mieng" } ] }
```

### Tồn kho — `GET /nguyen-lieu?sapHet=true`
```json
[ { "maNguyenLieu": 1, "tenNguyenLieu": "Cà phê hạt", "donViTinh": "gram", "soLuongTon": 900,
    "mucCanhBaoToiThieu": 500, "dangSuDung": true, "sapHet": true,
    "tieuHaoTrungBinhNgay": 650.5, "soNgayConLai": 1.4 } ]
```

---

## 5. Quy tắc nghiệp vụ "thông minh"

| Tính năng | Cách hoạt động |
|---|---|
| **Giá khuyến mãi tự động** | Món gắn `MaKhuyenMai`; nếu hôm nay nằm trong `NgayBatDau–NgayKetThuc` **và** giờ hiện tại nằm trong `GioBatDau–GioKetThuc` (nếu có) → `donGiaBan = giaBan × (100 − phanTramGiam)%`, lưu cố định vào `ChiTietDonHang`. |
| **Món tự ẩn khi hết nguyên liệu** | `coTheBan = ConHang && !NgungBan && có size DangApDung đủ nguyên liệu theo CongThucMon`. |
| **Chặn đơn thiếu nguyên liệu** | Khi đặt: cộng định mức (món-size + topping) × số lượng, so với `SoLuongTon` → thiếu trả 422 kèm chi tiết. |
| **Thanh toán tự trừ kho** | API gọi `EXEC sp_ThanhToanVaTruKho` → PAID + trừ kho + ghi `LichSuKho (TieuHaoTheoDon)`; trả về cảnh báo nguyên liệu dưới mức tối thiểu. |
| **Dự báo kho** | Tiêu hao TB/ngày = tổng `TieuHaoTheoDon` 7 ngày / 7; `soNgayConLai = soLuongTon / TB`. Sắp hết khi ≤ mức cảnh báo hoặc còn ≤ 2 ngày. |
| **Xóa mềm** | Xóa món/topping → `NgungBan = 1`; xóa nguyên liệu → `DangSuDung = 0` (giữ lịch sử đơn). |
| **Hủy đơn** | Chỉ QuanLy. Đơn PAID bị hủy → `DaHoanTien = 1`, `GioHoanTien = now` (nguyên liệu đã pha không cộng lại kho). |

---

## 6. Lỗi đã sửa trong script CSDL gốc

Bảng `CongThucMon` bản gốc có:
```sql
CONSTRAINT UQ_CongThuc_ChiTietMon UNIQUE (MaChiTietMon, MaNguyenLieu),
CONSTRAINT UQ_CongThuc_Topping    UNIQUE (MaTopping, MaNguyenLieu)
```
SQL Server coi `NULL = NULL` trong ràng buộc UNIQUE. Mọi dòng công thức của **món** có `MaTopping = NULL`,
nên hai món khác nhau **không thể cùng dùng một nguyên liệu** (ví dụ "Cà phê sữa đá" và "Bạc xỉu" cùng dùng
"Cà phê hạt" → lỗi `Violation of UNIQUE KEY constraint 'UQ_CongThuc_Topping'`).

Đã thay bằng **filtered unique index** — giữ nguyên ý nghĩa nghiệp vụ, chỉ kiểm tra khi cột khác NULL:
```sql
CREATE UNIQUE INDEX UQ_CongThuc_ChiTietMon ON CongThucMon(MaChiTietMon, MaNguyenLieu) WHERE MaChiTietMon IS NOT NULL;
CREATE UNIQUE INDEX UQ_CongThuc_Topping    ON CongThucMon(MaTopping, MaNguyenLieu)    WHERE MaTopping IS NOT NULL;
```

---

## 7. Lỗi thường gặp

| Hiện tượng | Cách xử lý |
|---|---|
| `A network-related ... error` khi chạy | SQL Server chưa chạy hoặc sai tên server trong connection string (Bước 3). |
| `Cannot open database "QuanCaPheDB"` | Chưa chạy `database/QuanCaPheDB.sql` (Bước 2). |
| `Violation of UNIQUE KEY constraint 'UQ_CongThuc_Topping'` | DB tạo từ bản script gốc → chạy `database/FixCongThucUnique.sql`. |
| `CREATE INDEX failed ... QUOTED_IDENTIFIER` | Chạy sqlcmd thiếu cờ `-I`. |
| 401 khi gọi API | Chưa bấm Authorize / token hết hạn (12 giờ) → đăng nhập lại. |
| 403 | Đúng token nhưng sai vai trò (ví dụ NhanVien gọi API của QuanLy). |
| Cổng 3000 bị chiếm | Đổi `applicationUrl` trong `Properties/launchSettings.json` và `servers.url` trong `openapi.yaml`. |
