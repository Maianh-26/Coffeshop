# Hợp đồng API bổ sung: đồng bộ backend với Web Admin

| | |
|---|---|
| **Gửi** | Nhóm Backend (`01-Backend-api`, `CoffeeShop.Api`) |
| **Từ** | Nhóm Web Admin (`03-Web-Admin`) |
| **Ngày** | 18/11/2026 |
| **Phiên bản** | 1.0 |
| **Đối chiếu với** | Backend trên `main` (commit 12/11/2026, `openapi.yaml` 1.109 dòng) và Web Admin commit `8255317` |
| **Thay thế / gom lại** | `de-xuat-api-don-hang.md` (v2.1), `de-xuat-api-xoa-tai-khoan.md`, `de-xuat-api-bo-email-tai-khoan.md`. Ba file cũ vẫn giữ để tham khảo code mẫu chi tiết; **file này là danh sách đầy đủ và là bản chốt** |

---

## Mục lục

1. [Tóm tắt: backend cần làm gì](#1-tóm-tắt-backend-cần-làm-gì)
2. [Quy ước chung (giữ nguyên)](#2-quy-ước-chung-giữ-nguyên)
3. [Nhóm A: Đơn hàng](#3-nhóm-a-đơn-hàng)
4. [Nhóm B: Tài khoản](#4-nhóm-b-tài-khoản)
5. [Nhóm C: Dữ liệu cho báo cáo và kho](#5-nhóm-c-dữ-liệu-cho-báo-cáo-và-kho)
6. [Nhóm D: Tùy chọn (chờ chủ quán chốt)](#6-nhóm-d-tùy-chọn-chờ-chủ-quán-chốt)
7. [Những thứ KHÔNG cần làm nữa](#7-những-thứ-không-cần-làm-nữa)
8. [Tổng hợp thay đổi SQL](#8-tổng-hợp-thay-đổi-sql)
9. [Tổng hợp thay đổi DTO](#9-tổng-hợp-thay-đổi-dto)
10. [Bổ sung `openapi.yaml`](#10-bổ-sung-openapiyaml)
11. [Tương thích ngược và thứ tự triển khai](#11-tương-thích-ngược-và-thứ-tự-triển-khai)
12. [Phía Web Admin sẽ làm gì khi backend xong](#12-phía-web-admin-sẽ-làm-gì-khi-backend-xong)
13. [Checklist bàn giao](#13-checklist-bàn-giao)

---

## 1. Tóm tắt: backend cần làm gì

| # | Hạng mục | Loại | Ưu tiên | Web Admin hiện tại |
|---|---|---|---|---|
| A1 | Thêm `soNhanMon`, `trangThaiPhucVu` vào đơn hàng | DB + DTO | **Cao** | Đã làm giao diện, ẩn khi backend chưa có |
| A2 | `PUT don-hang/{id}/so-nhan-mon` | API mới | **Cao** | Đã gọi, đang 404 |
| A3 | `PUT don-hang/{id}/trang-thai-phuc-vu` | API mới | **Cao** | Đã gọi, đang 404 |
| A4 | `PUT don-hang/{id}`: sửa món của đơn chưa thu tiền | API mới | Trung bình | Đã gọi, đang 404 |
| A5 | `GET don-hang`: thêm `trangThaiPhucVu`, `sapXep`, `timKiem` | Sửa API | Trung bình | Đã gửi `trangThaiPhucVu`, `sapXep`; backend đang bỏ qua |
| A6 | Ghi **giờ thu tiền** và **người thu tiền** khi thanh toán | DB + DTO | **Cao** | Chưa có, báo cáo đang tính theo giờ tạo đơn |
| B1 | `DELETE nguoi-dung/{id}` (xóa mềm, giải phóng tên đăng nhập) | API mới | **Cao** | Dùng tạm `PATCH trangThai=false` |
| B2 | `email` không bắt buộc khi tạo tài khoản | Sửa API + DB | Trung bình | Gửi tạm `<tenDangNhap>@coffeeshop.local` |
| C1 | Báo cáo doanh thu tính theo **giờ thu tiền** | Sửa API | **Cao** | Phụ thuộc A6 |
| C2 | `mon-ban-chay` trả thêm doanh thu topping; thêm `bao-cao/topping-ban-chay` | Sửa API + API mới | Trung bình | Chưa có |
| C3 | `lich-su-kho` trả thêm `tenNguoiDung` | DTO | Thấp | **Web không hiển thị** (xem ghi chú bên dưới) |
| D1 | Giá nhập khi nhập kho + báo cáo chi phí | DB + API | Tùy chọn | Chưa có |
| D2 | Tải ảnh món lên | API mới | Tùy chọn | Chỉ nhập được URL |
| D3 | Đơn chuyển khoản trên tablet tự sang `PAID` | Tích hợp | Tùy chọn | Mở từ v2.1 |

> **Nguyên tắc:** chỉ **thêm** trường và API. Không đổi tên, không xóa trường của response hiện có, để tablet Flutter không bị ảnh hưởng. Riêng các **request** body có `UnmappedMemberHandling.Disallow`, nên chỉ thêm trường **không bắt buộc**.

---

## 2. Quy ước chung (giữ nguyên)

- Tiền tố `api/v1/`, JSON camelCase theo tên cột DB, id là `int`.
- Header `Authorization: Bearer <token>`. Quyền ghi theo bảng ở từng API: `TatCa` = `QuanLy` + `NhanVien`.
- Lỗi luôn có dạng `{ "error": { "code", "message", "details" } }`. **`message` bằng tiếng Việt**, vì Web Admin hiển thị nguyên văn cho nhân viên.
- Danh sách có phân trang trả `{ data, meta: { page, limit, total, totalPages } }`, `limit` ≤ 100.
- Ngày giờ là giờ địa phương, không có múi giờ (`2026-11-18T08:30:00`). Bộ lọc `tuNgay` so sánh `>=`, `denNgay` so sánh `<=`. Web gửi `denNgay` = `23:59:59.997` khi lọc trọn ngày.

---

## 3. Nhóm A: Đơn hàng

### A1. Trường mới trên đơn hàng

| Trường JSON | Kiểu | Ý nghĩa |
|---|---|---|
| `soNhanMon` | `int?` (1–50) | Số trên bảng gỗ nhân viên đưa khách. `null` = chưa nhập |
| `trangThaiPhucVu` | `string` | `WAITING` (mặc định) → `PREPARING` → `SERVED` |
| `thoiGianThanhToan` | `DateTime?` | Lúc chuyển `PAID` (xem A6). `null` khi chưa thu |
| `maNguoiThu` | `int?` | Người bấm thu tiền (xem A6) |
| `tenNguoiThu` | `string?` | Họ tên người thu tiền |

**Hai loại trạng thái độc lập:**

| Thanh toán `trangThaiThanhToan` (đã có) | Phục vụ `trangThaiPhucVu` (mới) |
|---|---|
| `PENDING` / `PAID` / `CANCELLED` | `WAITING` / `PREPARING` / `SERVED` |

- "Đã hủy" **chỉ** dùng `trangThaiThanhToan = CANCELLED`. Không thêm `CANCELLED` vào `trangThaiPhucVu`.
- Không bắt buộc thu tiền trước khi phục vụ (quán có kiểu phục vụ trước, thu sau).

**"Đơn đang giữ số"** (dùng cho khóa số nhận món):

```
SoNhanMon IS NOT NULL
AND TrangThaiPhucVu IN ('WAITING', 'PREPARING')
AND TrangThaiThanhToan <> 'CANCELLED'
```

Hủy đơn hoặc chuyển `SERVED` thì số **tự giải phóng** (đơn rơi khỏi điều kiện). Số vẫn lưu trên đơn để tra cứu, **không** đặt về `NULL`.

**Ma trận được phép:**

| Thao tác | WAITING | PREPARING | SERVED | CANCELLED |
|---|:-:|:-:|:-:|:-:|
| Gán / đổi / bỏ số nhận món | ✅ | ✅ | ❌ 409 | ❌ 409 |
| Sang `PREPARING` | ✅ | ❌ 409 | ❌ 409 | ❌ 409 |
| Sang `SERVED` | ❌ 409 | ✅ | ❌ 409 | ❌ 409 |
| Sửa món (`PUT don-hang/{id}`) | ✅ nếu `PENDING` | ✅ nếu `PENDING` | ❌ 409 | ❌ 409 |
| Thanh toán (đã có) | ✅ nếu `PENDING` | ✅ nếu `PENDING` | ✅ nếu `PENDING` | ❌ |
| Hủy (đã có, chỉ QuanLy) | ✅ | ✅ | ✅ | ❌ |

### A2. `PUT api/v1/don-hang/{id}/so-nhan-mon`

- **Quyền:** `TatCa`
- **Request:** `{ "soNhanMon": 18 }`. Gửi `{ "soNhanMon": null }` để bỏ số.
- **Response 200:** `DonHangDto` đầy đủ (có `chiTiet`).

| HTTP | `code` | Khi nào | `message` gợi ý |
|---|---|---|---|
| 400 | `VALIDATION_ERROR` | Ngoài 1–50, body sai | (tự sinh) |
| 404 | `NOT_FOUND` | Không có đơn | `Đơn hàng không tồn tại` |
| 409 | **`SO_NHAN_MON_DANG_DUNG`** | Số đang được đơn khác giữ | `Số 18 đang được dùng cho đơn #12 (chưa phục vụ xong). Hãy chọn số khác.` |
| 409 | `CONFLICT` | Đơn đã `SERVED` / `CANCELLED` | `Đơn đã phục vụ hoặc đã hủy, không đổi được số nhận món` |

- `SO_NHAN_MON_DANG_DUNG` trả thêm `details: { "soNhanMon": 18, "maDonHangDangGiu": 12 }`.
- Gửi lại đúng số đơn đang có → 200 (không tính là trùng với chính nó).
- Hai máy gán cùng số cùng lúc: unique index lọc (mục 8) chặn máy thứ hai. Bắt `DbUpdateException` có `SqlException.Number` **2601/2627** → 409 `SO_NHAN_MON_DANG_DUNG`.

### A3. `PUT api/v1/don-hang/{id}/trang-thai-phuc-vu`

- **Quyền:** `TatCa`
- **Request:** `{ "trangThaiPhucVu": "PREPARING" }`. Chỉ nhận `PREPARING` hoặc `SERVED`.
- **Response 200:** `DonHangDto`.
- **Quy tắc:** chỉ cho **đúng bước kế tiếp** (`WAITING → PREPARING → SERVED`), không bỏ bước, không lùi.

| HTTP | `code` | Khi nào | `message` gợi ý |
|---|---|---|---|
| 400 | `VALIDATION_ERROR` | Giá trị khác `PREPARING`/`SERVED` | |
| 404 | `NOT_FOUND` | Không có đơn | `Đơn hàng không tồn tại` |
| 409 | `CONFLICT` | Đơn đã hủy | `Đơn đã bị hủy` |
| 409 | `CONFLICT` | `WAITING` → `SERVED` | `Đơn chưa bắt đầu chuẩn bị, không thể xác nhận đã phục vụ` |
| 409 | `CONFLICT` | Lùi hoặc giữ nguyên | `Đơn đang ở trạng thái Đã phục vụ, không chuyển sang Đang chuẩn bị được` |

### A4. `PUT api/v1/don-hang/{id}`: sửa món của đơn

- **Quyền:** `TatCa`
- **Request:** **giống hệt** `POST api/v1/don-hang` (`DatMonRequest`). `chiTiet` **thay toàn bộ** danh sách cũ; `phuongThucThanhToan = null` → giữ nguyên.
- **Response 200:** `DonHangDto`, `tongTien` đã tính lại.
- **Xử lý:**
  1. Chỉ khi `PENDING` **và** `trangThaiPhucVu <> SERVED`, ngược lại 409 `CONFLICT` "Chỉ sửa được đơn chưa thanh toán, chưa hủy và chưa phục vụ".
  2. Kiểm tra món / size / topping / tồn kho **giống `DatMon`**, cùng mã lỗi (404, 422 `MON_NGUNG_BAN`, `MON_HET_HANG`, `SIZE_KHONG_AP_DUNG`, `TOPPING_HET_HANG`, `HET_NGUYEN_LIEU`). Nên tách phần dựng chi tiết của `DatMon` thành hàm dùng chung.
  3. Giá khuyến mãi áp theo **`ThoiGianTao` của đơn**, không theo giờ sửa.
  4. Trong **một transaction**: xóa `ChiTietDonHang_Topping` → xóa `ChiTietDonHang` → thêm dòng mới → tính lại `TongTien`. (Không có `ON DELETE CASCADE`.)
  5. **Không** trừ kho (kho chỉ trừ khi thanh toán).
  6. Giữ nguyên `MaNguoiDung`, `ThoiGianTao`, `SoNhanMon`, `TrangThaiPhucVu`.

### A5. `GET api/v1/don-hang`: thêm 3 tham số

| Tham số | Giá trị | Xử lý |
|---|---|---|
| `trangThaiPhucVu` | `WAITING` / `PREPARING` / `SERVED` | Lọc theo trạng thái phục vụ **và loại đơn `CANCELLED`** |
| `sapXep` | `maDonHang`, `-maDonHang`, `tongTien`, `-tongTien` (bỏ trống = mặc định) | Sắp xếp **trước** `Skip/Take`. Bằng nhau thì `MaDonHang` giảm dần. Giá trị khác → 400 |
| `timKiem` | Chuỗi chữ số, tối đa 10 ký tự | Khớp **mã đơn bắt đầu bằng** chuỗi đó **hoặc** `soNhanMon` **bằng đúng** số đó |

Mặc định (không có `sapXep`) giữ như hiện nay: `ThoiGianTao` giảm dần, rồi `MaDonHang` giảm dần.

Gợi ý `timKiem` (SQL Server dịch được qua EF):

```csharp
if (!string.IsNullOrEmpty(q.TimKiem))
{
    int? so = int.TryParse(q.TimKiem, out var n) ? n : null;
    query = query.Where(d => d.MaDonHang.ToString().StartsWith(q.TimKiem) || d.SoNhanMon == so);
}
```

```csharp
// DonHangQuery
[AllowedValues("WAITING", "PREPARING", "SERVED", null)] public string? TrangThaiPhucVu { get; set; }
[AllowedValues("maDonHang", "-maDonHang", "tongTien", "-tongTien", null)] public string? SapXep { get; set; }
[RegularExpression("^[0-9]{1,10}$")] public string? TimKiem { get; set; }
```

### A6. Ghi giờ thu tiền và người thu tiền

**Vấn đề hiện tại:** `DonHang` chỉ có `ThoiGianTao`. Đơn tạo lúc 23:50, thu tiền lúc 00:10 hôm sau đang bị tính vào doanh thu hôm trước, và không biết nhân viên nào đã thu tiền.

**Thay đổi:**
- Thêm cột `ThoiGianThanhToan DATETIME NULL`, `MaNguoiThu INT NULL` (FK `NguoiDung`).
- `POST don-hang/{id}/thanh-toan`: ghi `ThoiGianThanhToan = GETDATE()`, `MaNguoiThu = User.MaNguoiDung()`. Nên ghi luôn trong `sp_ThanhToanVaTruKho` (thêm tham số `@MaNguoiThu`) để cùng transaction.
- `DonHangDto` trả thêm `thoiGianThanhToan`, `maNguoiThu`, `tenNguoiThu`.
- **Dữ liệu cũ:** đơn `PAID` đã có → `ThoiGianThanhToan = ThoiGianTao` (chạy một lần trong script).
- Hủy đơn đã `PAID` (hoàn tiền) **không** xóa hai trường này.

> **Đính chính (18/11/2026, quyết định của chủ quán):** web **không hiển thị tên người** ở bất kỳ đâu: không người tạo đơn, không người thu tiền (chi tiết đơn, hóa đơn K80), không người thực hiện kho. Lý do giống quyết định ngày 14/11 (`tien-do-quan-ly-don-hang.md` mục 9): quán gia đình, mọi người đứng chung quầy, đối soát tiền theo két tiền mặt / sao kê chuyển khoản. Web chỉ dùng **`thoiGianThanhToan`**. Backend đã làm `maNguoiThu`, `tenNguoiThu` (C3: `tenNguoiDung`): **giữ nguyên, không cần gỡ** (trường thêm, cho phép null, giống `tenNhanVien` hiện có), dữ liệu lưu bên trong để tra khi cần.

### Ví dụ `DonHangDto` sau khi bổ sung

```json
{
  "maDonHang": 21,
  "maNguoiDung": null,
  "tenNhanVien": null,
  "tongTien": 74000,
  "phuongThucThanhToan": "CASH",
  "trangThaiThanhToan": "PAID",
  "thoiGianTao": "2026-11-18T08:23:00",
  "lyDoHuy": null,
  "daHoanTien": false,
  "gioHoanTien": null,
  "soNhanMon": 18,
  "trangThaiPhucVu": "PREPARING",
  "thoiGianThanhToan": "2026-11-18T08:25:41",
  "maNguoiThu": 2,
  "tenNguoiThu": "Nguyễn Văn Nhân Viên",
  "chiTiet": [ ... ]
}
```

---

## 4. Nhóm B: Tài khoản

### B1. `DELETE api/v1/nguoi-dung/{id}`

- **Quyền:** `QuanLy`
- **Response:** 204 No Content.

| HTTP | `code` | Khi nào | `message` gợi ý |
|---|---|---|---|
| 404 | `NOT_FOUND` | Không có tài khoản (hoặc đã xóa) | `Người dùng không tồn tại` |
| 409 | `CONFLICT` | Xóa chính mình | `Không thể xóa tài khoản của chính mình` |
| 409 | `CONFLICT` | Xóa quản lý cuối cùng còn hoạt động | `Phải còn ít nhất một tài khoản Quản lý` |

**Cách xóa (xóa mềm, vì `DonHang.MaNguoiDung`, `LichSuKho.MaNguoiDung`, `DonHang.MaNguoiThu` tham chiếu tới):**
- Thêm cột `DaXoa BIT NOT NULL DEFAULT 0`.
- Khi xóa: `DaXoa = 1`, `TrangThai = 0`, đổi `TenDangNhap` thành `<cũ>#xoa<id>` và `Email` thành `NULL` (hoặc `<cũ>#xoa<id>`) để **tạo lại được tài khoản cùng tên đăng nhập**. Đây là việc cách làm tạm của web không xử lý được (đang bị 409 "Tên đăng nhập đã tồn tại").
- `GET nguoi-dung` và `GET nguoi-dung/{id}`: **không** trả tài khoản `DaXoa = 1`.
- `auth/dang-nhap`: tài khoản `DaXoa = 1` đăng nhập → 401 như sai mật khẩu (khác tài khoản bị khóa, hiện đang trả 403 "Tài khoản đã bị khóa").
- `auth/toi` và mọi API có token: token của tài khoản `DaXoa = 1` → 401, giống chỗ đang kiểm tra `TrangThai` trong `AuthController`.
- Tên hiển thị trên đơn / lịch sử kho cũ vẫn lấy từ `HoTen` (giữ nguyên `HoTen`).

**Phân biệt với khóa:** `PATCH nguoi-dung/{id} { "trangThai": false }` vẫn là **khóa** (còn hiện trong danh sách, mở lại được). Web hiện chỉ dùng **Xóa**; khóa giữ lại cho sau này.

### B2. `email` không bắt buộc

- `NguoiDungTaoRequest.Email`: bỏ `[Required]`, thành `string?`, vẫn `[EmailAddress, StringLength(100)]` nếu có gửi.
- Cột `NguoiDung.Email`: cho phép `NULL`. Thay `UNIQUE` bằng unique index lọc `WHERE Email IS NOT NULL`.
- `NguoiDungDto.email`: `string?`, trả `null` khi không có.
- `NguoiDungCapNhatRequest.Email`: giữ như cũ (null = giữ nguyên).

---

## 5. Nhóm C: Dữ liệu cho báo cáo và kho

### C1. Báo cáo doanh thu tính theo giờ thu tiền

Áp dụng cho `bao-cao/doanh-thu`, `bao-cao/mon-ban-chay`, `bao-cao/gio-cao-diem` (sau khi có A6):

| Số liệu | Lọc khoảng thời gian theo |
|---|---|
| Doanh thu, `soDonDaThanhToan`, `theoPhuongThuc`, `theoNgay`, món bán chạy, giờ cao điểm | **`ThoiGianThanhToan`** của đơn `PAID` |
| `tongSoDon`, `soDonChoThanhToan`, `soDonHuy` | `ThoiGianTao` (như hiện nay) |
| `soDonHoanTien` | `GioHoanTien` |

- `theoNgay[].ngay` và `gio-cao-diem[].gio` lấy theo `ThoiGianThanhToan`.
- Request và response **giữ nguyên hình dạng**. Chỉ đổi cột dùng để lọc / gom nhóm.

### C2. Doanh thu topping

**Vấn đề:** `mon-ban-chay` đang tính `DonGiaBan × SoLuong` nên **không gồm topping**. Cộng doanh thu các món luôn thấp hơn `doanh-thu`.

**C2a. `GET bao-cao/mon-ban-chay`:** giữ `doanhThu` (tiền món) và **thêm** hai trường:

```json
{ "maTenMon": 3, "tenMon": "Bạc xỉu", "soLuong": 42, "doanhThu": 1218000,
  "doanhThuTopping": 84000, "tongDoanhThu": 1302000 }
```

`doanhThuTopping` = tổng `DonGiaTopping × SoLuong` của các dòng món đó. `tongDoanhThu = doanhThu + doanhThuTopping`. Sắp xếp giữ như cũ (`soLuong` giảm dần, rồi `doanhThu`).

**C2b. API mới `GET api/v1/bao-cao/topping-ban-chay`** (`QuanLy`)

- Query: `tuNgay`, `denNgay`, `soLuong` (1–50, mặc định 10), giống `mon-ban-chay`. Chỉ tính đơn `PAID`.
- Response:

```json
[ { "maTopping": 1, "tenTopping": "Trân châu", "soLuong": 37, "doanhThu": 185000 } ]
```

`soLuong` = tổng số ly có gọi topping đó (`SUM(ChiTietDonHang.SoLuong)`), `doanhThu` = `SUM(DonGiaTopping × SoLuong)`.

### C3. `lich-su-kho` trả tên người thực hiện

`LichSuKhoDto` thêm `tenNguoiDung: string?` (họ tên của `MaNguoiDung`; `null` với dòng `TieuHaoTheoDon` do hệ thống ghi). Nên trả kèm luôn **người thu tiền của đơn** cho dòng tiêu hao, nếu dễ lấy: không bắt buộc.

> **Đính chính (18/11/2026):** trang Kho **không hiển thị** người thực hiện (quyết định của chủ quán). Backend giữ trường này, web không dùng. Phần tùy chọn "người thu tiền cho dòng tiêu hao" **không làm**.

---

## 6. Nhóm D: Tùy chọn (chờ chủ quán chốt)

Chỉ làm khi chủ quán xác nhận cần. Ghi sẵn hợp đồng để hai bên làm cùng một kiểu.

### D1. Giá nhập và báo cáo chi phí

- `GiaoDichKhoRequest` (dùng cho `nhap-kho`): thêm `donGiaNhap: decimal?` (≥ 0, đ / đơn vị tính). Không gửi = không ghi giá.
- `LichSuKho`: thêm cột `DonGiaNhap DECIMAL(18,2) NULL`; `LichSuKhoDto` thêm `donGiaNhap`, `thanhTienNhap` (= `donGiaNhap × soLuongThayDoi`, chỉ với `NhapHang`).
- API mới `GET api/v1/bao-cao/chi-phi?tuNgay&denNgay` (`QuanLy`):

```json
{ "tongChiPhiNhap": 3250000,
  "theoNguyenLieu": [ { "maNguyenLieu": 1, "tenNguyenLieu": "Cà phê hạt", "soLuongNhap": 5000, "thanhTien": 1500000 } ],
  "theoNgay": [ { "ngay": "2026-11-18", "thanhTien": 1500000 } ] }
```

### D2. Tải ảnh món

- `POST api/v1/anh-mon` (`QuanLy`), `multipart/form-data`, trường `file`; chỉ nhận `image/jpeg`, `image/png`, `image/webp`, ≤ 2 MB.
- Response 201: `{ "url": "/uploads/mon/7f3a1c.webp" }` (đường dẫn tương đối ≤ 255 ký tự, lưu vào `hinhAnh` qua `POST/PATCH mon` như hiện nay).
- Lỗi: 400 `FILE_KHONG_HOP_LE` (sai loại), 413 `FILE_QUA_LON`.
- Backend phục vụ file tĩnh ở `/uploads/...` (CORS đã mở).

### D3. Đơn chuyển khoản trên tablet tự sang `PAID`

Mở từ đề xuất v2.1, cần chốt cổng thanh toán. Nếu làm: khi cổng báo đã nhận tiền, backend gọi cùng luồng `thanh-toan` (trừ kho, ghi `ThoiGianThanhToan`, `MaNguoiThu = NULL`).

---

## 7. Những thứ KHÔNG cần làm nữa

| Hạng mục | Lý do |
|---|---|
| `api/auth/forgot-password`, `verify-reset-code`, `reset-password` | Chủ quán bỏ quên mật khẩu qua email (18/11/2026). Quên mật khẩu thì chủ quán đặt lại bằng `PATCH nguoi-dung/{id} { matKhauMoi }` |
| Gửi mail (SMTP) | Như trên |
| Thay đổi `auth/doi-mat-khau` | Giữ nguyên API. Web hiện không gọi (đã bỏ trang "Tài khoản của tôi") nhưng tablet hoặc sau này có thể dùng |
| `DELETE` cứng món / topping / nguyên liệu | Giữ xóa mềm như hiện nay |

---

## 8. Tổng hợp thay đổi SQL

Đề xuất một script chạy lại nhiều lần không lỗi: **`database/DongBoWebAdmin_20261118.sql`**, đồng thời cập nhật `QuanCaPheDB.sql` cho DB tạo mới.

```sql
-- ============ A1: số nhận món + trạng thái phục vụ
IF COL_LENGTH('DonHang', 'SoNhanMon') IS NULL
    ALTER TABLE DonHang ADD SoNhanMon INT NULL;
IF COL_LENGTH('DonHang', 'TrangThaiPhucVu') IS NULL
    ALTER TABLE DonHang ADD TrangThaiPhucVu VARCHAR(20) NOT NULL
        CONSTRAINT DF_DonHang_TrangThaiPhucVu DEFAULT 'WAITING';
GO
-- Đơn cũ (trước hôm nay) đã thu / đã hủy coi như đã phục vụ
UPDATE DonHang SET TrangThaiPhucVu = 'SERVED'
WHERE TrangThaiThanhToan IN ('PAID', 'CANCELLED') AND TrangThaiPhucVu = 'WAITING'
  AND ThoiGianTao < CAST(GETDATE() AS DATE);
GO
IF OBJECT_ID('CK_DonHang_SoNhanMon') IS NULL
    ALTER TABLE DonHang ADD CONSTRAINT CK_DonHang_SoNhanMon CHECK (SoNhanMon IS NULL OR SoNhanMon BETWEEN 1 AND 50);
IF OBJECT_ID('CK_DonHang_TrangThaiPhucVu') IS NULL
    ALTER TABLE DonHang ADD CONSTRAINT CK_DonHang_TrangThaiPhucVu CHECK (TrangThaiPhucVu IN ('WAITING', 'PREPARING', 'SERVED'));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DonHang_SoNhanMon_DangGiu')
    CREATE UNIQUE NONCLUSTERED INDEX UX_DonHang_SoNhanMon_DangGiu ON DonHang (SoNhanMon)
    WHERE SoNhanMon IS NOT NULL AND TrangThaiPhucVu IN ('WAITING', 'PREPARING') AND TrangThaiThanhToan <> 'CANCELLED';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DonHang_TrangThaiPhucVu')
    CREATE NONCLUSTERED INDEX IX_DonHang_TrangThaiPhucVu ON DonHang (TrangThaiPhucVu, ThoiGianTao DESC);
GO

-- ============ A6: giờ thu tiền + người thu
IF COL_LENGTH('DonHang', 'ThoiGianThanhToan') IS NULL
    ALTER TABLE DonHang ADD ThoiGianThanhToan DATETIME NULL;
IF COL_LENGTH('DonHang', 'MaNguoiThu') IS NULL
    ALTER TABLE DonHang ADD MaNguoiThu INT NULL
        CONSTRAINT FK_DonHang_NguoiThu REFERENCES NguoiDung(MaNguoiDung);
GO
UPDATE DonHang SET ThoiGianThanhToan = ThoiGianTao
WHERE ThoiGianThanhToan IS NULL AND (TrangThaiThanhToan = 'PAID' OR DaHoanTien = 1);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DonHang_ThoiGianThanhToan')
    CREATE NONCLUSTERED INDEX IX_DonHang_ThoiGianThanhToan ON DonHang (ThoiGianThanhToan) WHERE ThoiGianThanhToan IS NOT NULL;
GO
-- sp_ThanhToanVaTruKho: thêm tham số @MaNguoiThu INT = NULL và trong UPDATE DonHang:
--   SET TrangThaiThanhToan = 'PAID', ThoiGianThanhToan = GETDATE(), MaNguoiThu = @MaNguoiThu
-- DonHangService.ThanhToan gọi: EXEC sp_ThanhToanVaTruKho @MaDonHang = {id}, @MaNguoiThu = {User.MaNguoiDung()}

-- ============ B1: xóa tài khoản (mềm)
IF COL_LENGTH('NguoiDung', 'DaXoa') IS NULL
    ALTER TABLE NguoiDung ADD DaXoa BIT NOT NULL CONSTRAINT DF_NguoiDung_DaXoa DEFAULT 0;
GO

-- ============ B2: email không bắt buộc
-- 1) Xóa UNIQUE constraint tự sinh trên Email (tên constraint khác nhau giữa các máy → tra sys.key_constraints)
DECLARE @uq SYSNAME = (
    SELECT kc.name FROM sys.key_constraints kc
    JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE kc.type = 'UQ' AND OBJECT_NAME(kc.parent_object_id) = 'NguoiDung' AND c.name = 'Email');
IF @uq IS NOT NULL EXEC (N'ALTER TABLE NguoiDung DROP CONSTRAINT ' + @uq);
GO
ALTER TABLE NguoiDung ALTER COLUMN Email VARCHAR(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_NguoiDung_Email')
    CREATE UNIQUE NONCLUSTERED INDEX UX_NguoiDung_Email ON NguoiDung (Email) WHERE Email IS NOT NULL;
GO

-- ============ D1 (tùy chọn): giá nhập
-- IF COL_LENGTH('LichSuKho', 'DonGiaNhap') IS NULL ALTER TABLE LichSuKho ADD DonGiaNhap DECIMAL(18,2) NULL;
```

> Unique index lọc cần `QUOTED_IDENTIFIER ON` khi chạy script: dùng `sqlcmd -I` như hướng dẫn trong `CLAUDE.md` của backend.

---

## 9. Tổng hợp thay đổi DTO

```csharp
// ---------- Đơn hàng (thêm trường MỚI trước ChiTiet)
public record DonHangDto(
    int MaDonHang, int? MaNguoiDung, string? TenNhanVien, decimal TongTien,
    string PhuongThucThanhToan, string TrangThaiThanhToan, DateTime ThoiGianTao,
    string? LyDoHuy, bool DaHoanTien, DateTime? GioHoanTien,
    int? SoNhanMon, string TrangThaiPhucVu,                                 // A1
    DateTime? ThoiGianThanhToan, int? MaNguoiThu, string? TenNguoiThu,      // A6
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ChiTietDonDto>? ChiTiet);

public class DonHangQuery : PageQuery
{
    [AllowedValues("PENDING", "PAID", "CANCELLED", null)] public string? TrangThai { get; set; }
    [AllowedValues("WAITING", "PREPARING", "SERVED", null)] public string? TrangThaiPhucVu { get; set; }       // A5
    [AllowedValues("maDonHang", "-maDonHang", "tongTien", "-tongTien", null)] public string? SapXep { get; set; } // A5
    [RegularExpression("^[0-9]{1,10}$")] public string? TimKiem { get; set; }                                   // A5
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
}

public class SoNhanMonRequest { [Range(1, 50)] public int? SoNhanMon { get; set; } }                          // A2
public class TrangThaiPhucVuRequest
{
    [Required, AllowedValues("PREPARING", "SERVED")] public string TrangThaiPhucVu { get; set; } = "";      // A3
}
// A4 dùng lại DatMonRequest.

// ---------- Tài khoản
public record NguoiDungDto(int MaNguoiDung, string TenDangNhap, string HoTen, string? Email, string VaiTro, bool TrangThai); // B2
public class NguoiDungTaoRequest
{
    // ... giữ nguyên các trường khác ...
    [EmailAddress, StringLength(100)] public string? Email { get; set; }                                    // B2: bỏ [Required]
}

// ---------- Báo cáo
public record MonBanChayDto(int MaTenMon, string TenMon, int SoLuong, decimal DoanhThu,
    decimal DoanhThuTopping, decimal TongDoanhThu);                                                         // C2a
public record ToppingBanChayDto(int MaTopping, string TenTopping, int SoLuong, decimal DoanhThu);         // C2b

// ---------- Kho
public record LichSuKhoDto(
    int MaLichSu, int MaNguyenLieu, string TenNguyenLieu, int? MaDonHang, int? MaNguoiDung,
    string? TenNguoiDung,                                                                                    // C3
    string LoaiGiaoDich, decimal SoLuongThayDoi, DateTime ThoiGian, string? GhiChu);
```

> **Lưu ý thứ tự tham số record:** thêm `TenNguoiDung` vào giữa `LichSuKhoDto` chỉ đổi thứ tự trong C#. JSON vẫn theo tên trường nên client không bị ảnh hưởng.

---

## 10. Bổ sung `openapi.yaml`

```yaml
# components/schemas/DonHang/properties — thêm
soNhanMon:         { type: integer, minimum: 1, maximum: 50, nullable: true }
trangThaiPhucVu:   { type: string, enum: [WAITING, PREPARING, SERVED] }
thoiGianThanhToan: { type: string, format: date-time, nullable: true }
maNguoiThu:        { type: integer, nullable: true }
tenNguoiThu:       { type: string, nullable: true }

# components/schemas/NguoiDung/properties/email → nullable: true
# components/schemas/NguoiDungTaoRequest → bỏ "email" khỏi required
# components/schemas/MonBanChay/properties — thêm doanhThuTopping, tongDoanhThu (number)
# components/schemas/LichSuKho/properties — thêm tenNguoiDung (string, nullable)

paths:
  /api/v1/don-hang:
    get:
      parameters:   # thêm
        - { name: trangThaiPhucVu, in: query, schema: { type: string, enum: [WAITING, PREPARING, SERVED] } }
        - { name: sapXep, in: query, schema: { type: string, enum: [maDonHang, -maDonHang, tongTien, -tongTien] } }
        - { name: timKiem, in: query, schema: { type: string, pattern: '^[0-9]{1,10}$' } }
  /api/v1/don-hang/{id}:
    put:
      summary: Sửa món của đơn chưa thanh toán, chưa phục vụ
      requestBody: { $ref: '#/components/requestBodies/DatMon' }
      responses: { '200': { $ref: '#/components/responses/DonHang' }, '409': ..., '422': ... }
  /api/v1/don-hang/{id}/so-nhan-mon:
    put:
      requestBody: { content: { application/json: { schema:
        { type: object, additionalProperties: false,
          properties: { soNhanMon: { type: integer, minimum: 1, maximum: 50, nullable: true } } } } } }
      responses: { '200': ..., '404': ..., '409': { description: SO_NHAN_MON_DANG_DUNG hoặc CONFLICT } }
  /api/v1/don-hang/{id}/trang-thai-phuc-vu:
    put:
      requestBody: { content: { application/json: { schema:
        { type: object, additionalProperties: false, required: [trangThaiPhucVu],
          properties: { trangThaiPhucVu: { type: string, enum: [PREPARING, SERVED] } } } } } }
  /api/v1/nguoi-dung/{id}:
    delete:
      summary: Xóa tài khoản (xóa mềm, giải phóng tên đăng nhập) — chỉ QuanLy
      responses: { '204': {}, '404': ..., '409': ... }
  /api/v1/bao-cao/topping-ban-chay:
    get:
      parameters: [ tuNgay, denNgay, soLuong ]   # giống mon-ban-chay
      responses: { '200': { content: { application/json: { schema:
        { type: array, items: { $ref: '#/components/schemas/ToppingBanChay' } } } } } }
```

---

## 11. Tương thích ngược và thứ tự triển khai

- Mọi thay đổi **chỉ thêm** trường response / tham số query / API mới. Tablet Flutter không phải sửa gì; `POST don-hang` của tablet giữ nguyên.
- Web Admin **đã chịu được** backend cũ: thiếu `trangThaiPhucVu` → ẩn phần phục vụ; API mới trả 404 → báo "máy chủ chưa hỗ trợ"; `sapXep` bị bỏ qua → web tự sắp xếp *(chế độ này đã bỏ 23/11/2026 vì backend đã có `sapXep`)*. Vì vậy backend có thể **triển khai từng nhóm**, không cần làm hết một lần.

**Thứ tự đề xuất:**

| Đợt | Hạng mục | Lý do |
|---|---|---|
| 1 | A1, A2, A3, A5 (`trangThaiPhucVu`) | Luồng phục vụ hằng ngày tại quầy, web đã sẵn sàng |
| 2 | A6 + C1 | Số liệu doanh thu đang lệch ngày |
| 3 | B1, B2 | Đang có lỗi không tạo lại được tài khoản cùng tên |
| 4 | A4, A5 (`sapXep`, `timKiem`), C2, C3 | Cải thiện, đã có cách tạm |
| 5 | D1, D2, D3 | Khi chủ quán chốt |

---

## 12. Phía Web Admin sẽ làm gì khi backend xong

| Hạng mục backend | Việc của Web Admin |
|---|---|
| A1–A5 | Không phải sửa code (đã gọi đúng hợp đồng). Gửi thêm `timKiem` khi tìm đơn; bỏ chế độ tự sắp xếp khi phát hiện máy chủ hỗ trợ `sapXep` — **đã làm 23/11/2026** |
| A6 | Chỉ thêm `ThoiGianThanhToan` vào `Models/DonHang`; chi tiết đơn và hóa đơn K80 hiện "Thu lúc …" (**không** có tên người thu, không thêm `maNguoiThu`/`tenNguoiThu` vào model) |
| C1 | Không đổi code; cập nhật ghi chú trang Tổng quan "doanh thu tính theo giờ thu tiền" |
| B1 | `AccountService.DeleteAsync` đổi sang `DELETE`; bỏ chú thích "đã xóa vẫn giữ tên đăng nhập" ở trang Tài khoản |
| B2 | Bỏ `NguoiDungTaoRequest.EmailNoiBo`, không gửi `email` khi tạo |
| C2 | Trang Tổng quan: cột "Doanh thu" món bán chạy dùng `tongDoanhThu`; thêm bảng "Topping bán chạy" |
| C3 | **Không làm**: trang Kho không hiển thị người thực hiện (quyết định của chủ quán 18/11/2026) |
| D1 / D2 | Thêm ô "Đơn giá nhập" khi nhập kho + thẻ chi phí ở Tổng quan / nút "Tải ảnh lên" ở form món |

Mọi thay đổi phía web đi kèm cập nhật `Services/Mock/MockApiHandler.cs` để chế độ giả lập vẫn khớp hợp đồng.

---

## 13. Checklist bàn giao

**Backend**
- [ ] Script `database/DongBoWebAdmin_20261118.sql` + cập nhật `QuanCaPheDB.sql` và `sp_ThanhToanVaTruKho`
- [ ] `Data/Entities.cs`, `AppDbContext.cs`: cột mới của `DonHang`, `NguoiDung`, (`LichSuKho`)
- [ ] `Dtos/Dtos.cs`, `Common/Mapping.cs` theo mục 9 (nhớ `Include` người thu khi map `TenNguoiThu`)
- [ ] `DonHangController` / `DonHangService`: A2, A3, A4, A5, A6
- [ ] `NguoiDungController`: B1, B2; `AuthController.DangNhap` chặn `DaXoa`
- [ ] `BaoCaoController`: C1, C2a, C2b
- [ ] `KhoController` (`lich-su-kho`): C3
- [ ] `openapi.yaml` theo mục 10; mẫu gọi trong `CoffeeShop.Api.http`
- [ ] Báo nhóm Web Admin đợt nào đã lên `main`

**Web Admin**
- [ ] Chạy thử với backend thật (`Api:UseMock: false`) sau mỗi đợt
- [ ] Làm các việc ở mục 12 tương ứng với đợt backend đã xong
