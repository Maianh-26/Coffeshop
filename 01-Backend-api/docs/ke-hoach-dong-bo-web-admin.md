# Kế hoạch đồng bộ Backend với Web Admin

| | |
|---|---|
| **Căn cứ** | `03-Web-Admin/docs/hop-dong-api-bo-sung.md` v1.0 (18/11/2026, bản chốt) |
| **Đối chiếu** | Backend `01-Backend-api` trên nhánh `API-HIHI` (commit `eb1502c`, đã đồng bộ `main`) và code Web Admin (`Services/*.cs`, `Models/*.cs`, `Services/Mock/MockApiHandler.cs`) |
| **Ngày lập** | 18/11/2026 |

---

## 1. Kết luận nhanh

- Toàn bộ API **đang có** của backend khớp với các lời gọi Web Admin hiện dùng (đường dẫn, phương thức, tham số `sapHet`, `dangSuDung`, `baoGomNgungBan`, phân trang, định dạng lỗi). Phần đang chạy không có gì lệch.
- **Chưa làm hạng mục nào** trong hợp đồng bổ sung. 13/13 hạng mục bắt buộc (A1–A6, B1–B2, C1–C3) còn thiếu, D1–D3 chờ chủ quán chốt.
- Ngoài hợp đồng còn **4 lỗi/thiếu sót**. Nếu không sửa cùng lúc, vài hạng mục sẽ trả 500 hoặc chạy sai (mục 3).

---

## 2. Đối chiếu từng hạng mục hợp đồng với code hiện tại

| # | Hạng mục | Hiện trạng backend | Web Admin hiện tại | Chỗ cần sửa |
|---|---|---|---|---|
| A1 | `soNhanMon`, `trangThaiPhucVu` trên đơn | Chưa có cột, entity, DTO | Model đã có 2 trường; ẩn UI khi null | SQL, `Entities.DonHang`, `DonHangDto`, `Mapping` |
| A2 | `PUT don-hang/{id}/so-nhan-mon` | Chưa có route → 404 | `OrderService.SetPickupNumberAsync` đã gọi | `DonHangController`, `DonHangService`, DTO `SoNhanMonRequest` |
| A3 | `PUT don-hang/{id}/trang-thai-phuc-vu` | Chưa có route → 404 | `SetServingStatusAsync` đã gọi | như A2, DTO `TrangThaiPhucVuRequest` |
| A4 | `PUT don-hang/{id}` sửa món | Chưa có route → 404. Phần dựng chi tiết đang nằm gọn trong `DatMon`, chưa tách được để dùng lại | `UpdateAsync` đã gọi | Tách `DungChiTiet(...)` từ `DatMon`; giá KM theo `ThoiGianTao` |
| A5 | `GET don-hang`: `trangThaiPhucVu`, `sapXep`, `timKiem` | `DonHangQuery` chỉ có `TrangThai/TuNgay/DenNgay`. Tham số lạ **bị bỏ qua lặng lẽ** (200) | Đã gửi `trangThaiPhucVu`, `sapXep`; web tự sắp xếp | `DonHangQuery`, `DonHangController.DanhSach` |
| A6 | Giờ thu tiền, người thu | Chỉ có `ThoiGianTao`. `sp_ThanhToanVaTruKho` chỉ nhận `@MaDonHang` | Chưa có trường | SQL + sp (thêm `@MaNguoiThu`), entity, `AppDbContext` (FK thứ hai tới `NguoiDung`), `Query()` Include `NguoiThu` |
| B1 | `DELETE nguoi-dung/{id}` (xóa mềm) | Chưa có route, chưa có cột `DaXoa` | `AccountService.DeleteAsync` dùng tạm `PATCH trangThai=false` → không tạo lại được tên đăng nhập | SQL, entity, `NguoiDungController` (lọc `DaXoa`), `AuthController` |
| B2 | Email không bắt buộc | DB `Email NOT NULL UNIQUE`; DTO `[Required]`; `NguoiDungDto.Email` là `string` | Gửi tạm `<ten>@coffeeshop.local` | SQL (bỏ UQ, thêm unique index lọc), DTO, `Entities.NguoiDung.Email` → `string?`, bỏ check trùng khi `null` |
| C1 | Báo cáo theo giờ thu tiền | `TrongKhoang()` lọc mọi số liệu theo `ThoiGianTao` | Phụ thuộc A6 | `BaoCaoController`: tách lọc theo `ThoiGianTao` / `ThoiGianThanhToan` / `GioHoanTien` |
| C2a | `mon-ban-chay` thêm `doanhThuTopping`, `tongDoanhThu` | Chỉ tính `DonGiaBan × SoLuong` | Model `MonBanChay` chưa có 2 trường | `MonBanChayDto`, query join `ChiTietDonHang_Topping` |
| C2b | `GET bao-cao/topping-ban-chay` | Chưa có | Chưa có | `BaoCaoController`, DTO `ToppingBanChayDto` |
| C3 | `lich-su-kho` thêm `tenNguoiDung` | `LichSuKho` chưa có navigation tới `NguoiDung` | Model chưa có | Entity + `AppDbContext`, `LichSuKhoDto`, `Mapping`, `KhoController` Include |
| — | `openapi.yaml`, `CoffeeShop.Api.http` | Không có path/schema nào ở trên | — | Theo mục 10 hợp đồng |
| D1–D3 | Tùy chọn | Chưa có | Chưa có | Chờ chủ quán chốt |

---

## 3. Lệch phát hiện thêm (hợp đồng chưa ghi)

| # | Mức | Vấn đề | Hệ quả nếu bỏ qua | Cách sửa |
|---|---|---|---|---|
| G1 | **Cao** | `ErrorHandlingMiddleware` chỉ bắt `DbUpdateException` khi message chứa `"UNIQUE"` (phân biệt hoa thường). Lỗi **2601** của *unique index* có message `"...with unique index..."` (chữ thường) nên lọt xuống **500** | A2 (hai máy gán trùng số), B2 (trùng email) và `CongThucMon` hiện tại đều ra 500 thay vì 409 | Bắt theo `SqlException.Number is 2601 or 2627`; A2 tự bắt riêng để trả `SO_NHAN_MON_DANG_DUNG` + `details` |
| G2 | **Cao** | Chỉ `auth/toi` và `doi-mat-khau` kiểm tra `TrangThai`. Token của tài khoản **bị khóa** vẫn gọi được mọi API khác trong 12 giờ | Hợp đồng B1 yêu cầu token của tài khoản `DaXoa` bị 401 ở **mọi** API. Code hiện tại không làm được | Thêm `JwtBearerEvents.OnTokenValidated` trong `Program.cs`: tra `NguoiDung` (`TrangThai = 1 AND DaXoa = 0`), không đạt thì `ctx.Fail(...)` → 401 |
| G3 | **Cao** | `DonHang` sẽ có unique index lọc (A1). SQL Server đòi `QUOTED_IDENTIFIER ON` khi ghi vào bảng có index lọc. Stored procedure giữ thiết lập **từ lúc tạo**, nên nếu `sp_ThanhToanVaTruKho` được tạo không có `-I` thì sẽ lỗi | **Thanh toán hỏng hoàn toàn** (lỗi 1934) trên máy tạo sp không có `-I` | Script patch đặt `SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;` trước `CREATE OR ALTER PROCEDURE` và ghi rõ phải chạy `sqlcmd -I` |
| G4 | Trung bình | Model web `NguoiDungTaoRequest.Email` mặc định `""`. Sau B2, nếu web gửi `""` thì `[EmailAddress]` từ chối → 400 | Tạo tài khoản lỗi ngay sau khi web bỏ `EmailNoiBo` | Chốt với web: **không gửi** hoặc gửi `null`. Backend nên coi `""` như `null` cho chắc |
| G5 | Thấp | `PATCH nguoi-dung` kiểm tra trùng email với giá trị **chưa** `ToLowerInvariant()`, nhưng lại lưu bản đã lower | Có thể lọt hai email khác nhau chỉ ở chữ hoa (DB collation CI thường chặn, nhưng sẽ ra 500 vì G1) | Lower trước khi so sánh |
| G6 | Thấp | `NguoiDungDto.Email` sẽ thành nullable, nhưng model web vẫn là `string` | Không lỗi lúc chạy (System.Text.Json gán `null`), chỉ hiện trống | Báo web đổi thành `string?` |

> Ghi chú B1: quy tắc "phải còn ít nhất một Quản lý" luôn đúng khi người xóa là Quản lý đang hoạt động và không được xóa chính mình. Vẫn nên kiểm tra phòng hờ (rẻ), không cần đặt nặng.

---

## 4. Kế hoạch triển khai

Thứ tự theo mục 11 của hợp đồng, thêm **Đợt 0** cho phần nền. Mỗi đợt là **một PR `API-HIHI` → `main`**, merge xong báo nhóm Web Admin. Thứ tự sửa trong mỗi đợt theo quy ước API-first: `openapi.yaml` → `Dtos.cs` → Service/Controller → `CoffeeShop.Api.http`. Riêng SQL làm trước: script patch + `QuanCaPheDB.sql` → `Entities.cs`/`AppDbContext.cs`.

Ước lượng tính cho 1 người, gồm cả test tay.

### Đợt 0: Nền tảng (≈ 2 giờ)
1. Tạo `database/DongBoWebAdmin_20261118.sql` (chạy lại nhiều lần không lỗi). Đầu file có `SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;` (G3).
2. Sửa `ErrorHandlingMiddleware`: map `SqlException` 2601/2627 → 409 `CONFLICT` (G1).
3. Sao lưu DB trên máy trước khi chạy patch.

**Xong khi:** gán trùng công thức `CongThucMon` trả 409 thay vì 500.

### Đợt 1: Luồng phục vụ tại quầy (A1, A2, A3, A5 phần `trangThaiPhucVu`) (≈ 1 ngày)
1. SQL: cột `SoNhanMon`, `TrangThaiPhucVu`, CHECK, `UX_DonHang_SoNhanMon_DangGiu`, `IX_DonHang_TrangThaiPhucVu`, cập nhật dữ liệu cũ thành `SERVED`. Viết lại `sp_ThanhToanVaTruKho` có `QUOTED_IDENTIFIER ON` (G3).
2. `Entities.DonHang`: `int? SoNhanMon`, `string TrangThaiPhucVu = "WAITING"`. Thêm hằng `TrangThaiPhucVus`.
3. `DonHangDto` thêm 2 trường **trước** `ChiTiet`. Cập nhật `Mapping.ToDto`.
4. `DonHangService.GanSoNhanMon`: kiểm tra ma trận (SERVED/CANCELLED → 409). Kiểm tra trước số đang bị đơn khác giữ → 409 `SO_NHAN_MON_DANG_DUNG` + `details { soNhanMon, maDonHangDangGiu }`. Bắt thêm `DbUpdateException` 2601 cho trường hợp hai máy gán cùng lúc. Gửi lại đúng số cũ → 200.
5. `DonHangService.DoiTrangThaiPhucVu`: chỉ cho bước kế tiếp, kèm thông điệp 409 theo bảng A3.
6. `DonHangQuery.TrangThaiPhucVu` (`AllowedValues`), lọc kèm loại đơn `CANCELLED`.
7. `openapi.yaml` và `.http`.

**Kiểm thử:** gán số 18 cho đơn #1 rồi gán 18 cho #2 → 409 có `maDonHangDangGiu = 1`. Chuyển #1 sang `SERVED` rồi gán 18 cho #2 → 200. `WAITING → SERVED` → 409. Hủy đơn rồi gán số → 409. Tablet `POST don-hang` không gửi trường mới vẫn 201. Thanh toán vẫn chạy (kiểm tra G3).

### Đợt 2: Doanh thu đúng ngày (A6, C1) (≈ 0,5 ngày)
1. SQL: `ThoiGianThanhToan`, `MaNguoiThu` + FK, index lọc, backfill `ThoiGianThanhToan = ThoiGianTao`. Thêm `@MaNguoiThu INT = NULL` vào sp và ghi 2 cột trong `UPDATE DonHang`.
2. Entity + `AppDbContext`: navigation `NguoiThu` với `HasForeignKey(x => x.MaNguoiThu)`. Đây là FK thứ hai tới `NguoiDung` nên phải khai báo rõ, tránh EF tự sinh cột ảo.
3. `DonHangService.ThanhToan`: `EXEC sp_ThanhToanVaTruKho @MaDonHang = {id}, @MaNguoiThu = {maNguoiDung}`. `Query()` Include `NguoiThu`.
4. `DonHangDto` thêm `ThoiGianThanhToan`, `MaNguoiThu`, `TenNguoiThu`.
5. `BaoCaoController`: tách bộ lọc. Doanh thu, `soDonDaThanhToan`, `theoPhuongThuc`, `theoNgay`, món bán chạy, giờ cao điểm lọc và gom theo `ThoiGianThanhToan`. `tongSoDon`, `soDonChoThanhToan`, `soDonHuy` theo `ThoiGianTao`. `soDonHoanTien` theo `GioHoanTien`. Hình dạng response giữ nguyên.

**Kiểm thử:** tạo đơn lúc 23:50 (sửa `ThoiGianTao` bằng SQL), thu tiền hôm sau → doanh thu tính vào ngày thu. Hủy đơn đã PAID vẫn giữ `thoiGianThanhToan`.

### Đợt 3: Tài khoản (B1, B2, G2, G4, G5) (≈ 0,5–1 ngày)
1. SQL: `DaXoa`. Bỏ UQ email (tra tên constraint động), `Email NULL`, `UX_NguoiDung_Email` lọc.
2. `NguoiDung.Email` → `string?`, `NguoiDungDto.Email` → `string?`. `NguoiDungTaoRequest.Email` bỏ `[Required]`. Coi `""` như `null` (G4).
3. `DELETE nguoi-dung/{id}`: 404 nếu không có hoặc đã xóa. 409 nếu xóa chính mình hoặc quản lý cuối. Đặt `DaXoa = 1`, `TrangThai = 0`, `TenDangNhap = <cũ>#xoa<id>`, `Email = NULL`. Lưu ý `TenDangNhap` dài tối đa 50: cắt bớt phần tên cũ nếu cần.
4. `GET` danh sách/chi tiết lọc `DaXoa = 0`. `DangNhap`: `DaXoa` → 401 như sai mật khẩu (kiểm tra **trước** `TrangThai`).
5. `OnTokenValidated` chặn token của tài khoản khóa/xóa ở mọi API (G2).
6. Sửa so sánh email lower-case (G5).

**Kiểm thử:** xóa `nv1` rồi tạo lại `nv1` → 201. Token cũ của `nv1` gọi `GET don-hang` → 401. Tạo hai tài khoản không có email → đều 201. Hai tài khoản cùng email → 409 (không phải 500).

### Đợt 4: Cải thiện (A4, A5 `sapXep`/`timKiem`, C2, C3) (≈ 1 ngày)
1. Tách `DungChiTiet(req, thoiDiemGia)` từ `DatMon` (gồm kiểm tra món/size/topping/tồn kho). `DatMon` dùng `now`, `SuaMon` dùng `don.ThoiGianTao`.
2. `PUT don-hang/{id}`: chỉ khi `PENDING` và chưa `SERVED`. Một transaction: xóa `ChiTietDonHang_Topping` → `ChiTietDonHang` → thêm mới → tính lại `TongTien`. Không trừ kho. Giữ `phuongThucThanhToan` khi gửi `null`.
3. `sapXep` (sắp trước `Skip/Take`, hòa thì `MaDonHang` giảm dần), `timKiem` (mã đơn bắt đầu bằng / `soNhanMon` bằng).
4. C2a: thêm `DoanhThuTopping`, `TongDoanhThu`. C2b: `GET bao-cao/topping-ban-chay`.
5. C3: navigation `LichSuKho.NguoiDung`, `LichSuKhoDto.TenNguoiDung` (thêm vào giữa record thì JSON vẫn theo tên nên không ảnh hưởng client).

**Kiểm thử:** sửa đơn có món ngừng bán → 422 `MON_NGUNG_BAN`. Sửa đơn đã thanh toán → 409. Tổng `tongDoanhThu` của các món bằng `doanhThu` của báo cáo cùng khoảng.

### Đợt 5: Tùy chọn (D1–D3)
Chỉ làm khi chủ quán xác nhận. Hợp đồng đã đủ chi tiết, ước lượng D1 ≈ 0,5 ngày, D2 ≈ 0,5 ngày, D3 tùy cổng thanh toán.

---

## 5. Việc phối hợp với nhóm Web Admin

| Khi nào | Việc cần chốt / báo |
|---|---|
| Trước Đợt 3 | Web **không gửi** `email` (hoặc gửi `null`) sau khi bỏ `EmailNoiBo` (G4). Đổi `NguoiDung.Email` thành `string?` (G6) |
| Sau Đợt 3 | Web đổi `AccountService.DeleteAsync` sang `DELETE`. Báo web: token của tài khoản bị khóa giờ bị 401 ở mọi API (G2), nên web cần xử lý 401 bằng cách đưa về trang đăng nhập |
| Sau mỗi đợt | Báo commit đã lên `main` để web chạy `Api:UseMock: false` |
| Bất kỳ lúc nào | Nhóm tablet Flutter: không phải sửa gì (chỉ thêm trường) |

---

## 6. Rủi ro

- **Chạy patch SQL trên DB của từng thành viên**: mỗi người phải tự chạy `DongBoWebAdmin_20261118.sql` bằng `sqlcmd -I`. Thiếu `-I` thì thanh toán hỏng (G3). Ghi rõ trong mô tả PR.
- **Chưa có project test**: toàn bộ kiểm thử là test tay qua `.http`. Có thể cân nhắc thêm project xUnit cho các quy tắc trạng thái (A2/A3/A4) vì ma trận trạng thái dễ sai.
- **Tên constraint UQ email khác nhau giữa các máy**: script đã tra động, nhưng nên chạy thử trên một DB tạo mới và một DB cũ.
- **`OnTokenValidated` thêm 1 truy vấn mỗi request**: chấp nhận được với quy mô quán. Nếu cần, cache ngắn (30–60 giây) bằng `IMemoryCache`.

---

## 7. Checklist

- [x] Đợt 0: script patch khung, G1 middleware (18/11/2026, reviewer PASS)
- [x] Đợt 1: A1, A2, A3, A5 (`trangThaiPhucVu`), sp tạo lại với `QUOTED_IDENTIFIER ON` (18/11/2026, reviewer PASS)
- [x] Đợt 2: A6, C1 (18/11/2026, reviewer PASS)
- [x] Đợt 3: B1, B2, G2 (kiểm cả vai trò), G4, G5 + chuyển tài khoản `TrangThai = 0` cũ thành đã xóa (18/11/2026, reviewer PASS, security-review không có finding)
- [ ] Đợt 4: A4, A5 (`sapXep`, `timKiem`), C2a, C2b, C3
- [ ] `QuanCaPheDB.sql` cập nhật cho DB tạo mới sau mỗi đợt
- [ ] `openapi.yaml` + `CoffeeShop.Api.http` sau mỗi đợt
- [ ] Báo nhóm Web Admin sau mỗi lần merge
- [ ] Đợt 5 (D1–D3): chờ chủ quán
