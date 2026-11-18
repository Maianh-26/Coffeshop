# Đề xuất API đơn hàng: Số nhận món, Trạng thái phục vụ, Sửa đơn

| | |
|---|---|
| **Gửi** | Nhóm Backend (`CoffeeShop.Api`) |
| **Từ** | Nhóm Web Admin (`03-Web-Admin`) |
| **Ngày** | 11/11/2026 |
| **Phiên bản** | 2.1 (bắt buộc đi lần lượt Chờ phục vụ → Đang chuẩn bị → Đã phục vụ) |
| **Yêu cầu gốc của chủ quán** | [yeu-cau-quan-ly-don-hang.md](yeu-cau-quan-ly-don-hang.md) |
| **Trạng thái phía Web Admin** | Đã làm xong giao diện. Chạy đầy đủ ở chế độ giả lập (`Api:UseMock: true`) và đang chờ backend |

---

## Mục lục

1. [Tóm tắt: backend cần làm gì](#1-tóm-tắt-backend-cần-làm-gì)
2. [Nghiệp vụ (đọc trước khi code)](#2-nghiệp-vụ-đọc-trước-khi-code)
3. [Thay đổi cơ sở dữ liệu (SQL)](#3-thay-đổi-cơ-sở-dữ-liệu-sql)
4. [Thay đổi Entity, DTO, Mapping](#4-thay-đổi-entity-dto-mapping)
5. [Đặc tả từng API](#5-đặc-tả-từng-api)
6. [Code mẫu gợi ý (Controller + Service)](#6-code-mẫu-gợi-ý-controller--service)
7. [Bổ sung openapi.yaml](#7-bổ-sung-openapiyaml)
8. [Danh sách test (Swagger / .http)](#8-danh-sách-test-swagger--http)
9. [Ảnh hưởng tới phần khác & tương thích ngược](#9-ảnh-hưởng-tới-phần-khác--tương-thích-ngược)
10. [Việc còn mở, cần chốt](#10-việc-còn-mở-cần-chốt)
11. [Checklist bàn giao](#11-checklist-bàn-giao)

---

## 1. Tóm tắt: backend cần làm gì

| # | Hạng mục | Loại | Ưu tiên |
|---|---|---|---|
| 1 | Thêm 2 cột `SoNhanMon` và `TrangThaiPhucVu` vào bảng `DonHang`, kèm index chống trùng số | DB | **Cao** |
| 2 | Trả thêm `soNhanMon` và `trangThaiPhucVu` trong mọi response `DonHang` | DTO | **Cao** |
| 3 | `PUT api/v1/don-hang/{id}/so-nhan-mon`: gán, đổi hoặc bỏ số nhận món | API mới | **Cao** |
| 4 | `PUT api/v1/don-hang/{id}/trang-thai-phuc-vu`: chuyển trạng thái phục vụ | API mới | **Cao** |
| 5 | `GET api/v1/don-hang`: thêm tham số lọc `trangThaiPhucVu` | Sửa API | Trung bình |
| 6 | `PUT api/v1/don-hang/{id}`: sửa món của đơn chưa thanh toán | API mới | Trung bình |
| 7 | Đơn khách chuyển khoản trên tablet tự sang `PAID` | Tích hợp | Thấp (cần chốt, xem mục 10) |
| 8 | `GET api/v1/don-hang`: thêm tham số sắp xếp `sapXep` (mã đơn / tổng tiền, tăng / giảm) — **bổ sung 12/11/2026** | Sửa API | Trung bình |

> **Không thay đổi:** `POST don-hang` (tablet đặt món), `thanh-toan`, `huy`, báo cáo. Các API cũ giữ nguyên request/response. Response chỉ **thêm** trường mới.

---

## 2. Nghiệp vụ (đọc trước khi code)

### 2.1. Số nhận món là gì?

Quán có khoảng **50 bảng gỗ nhỏ** đánh số **1 đến 50**, để lẫn trong một hộp. Khi khách gọi món:

1. Nhân viên bốc ngẫu nhiên một bảng, ví dụ số **18**, rồi nhập số đó vào đơn trên web admin.
2. Nhân viên đưa bảng số 18 cho khách. Khách mang bảng về bàn.
3. Phục vụ mang đồ uống ra **bàn có bảng số 18**.
4. Giao đủ món xong, nhân viên thu lại bảng gỗ về quầy và bấm **"Xác nhận đã phục vụ"** trên web.
5. Từ lúc đó số 18 được **giải phóng**, có thể dùng cho đơn mới.

**Quy tắc khóa số:** trong lúc một đơn còn giữ số 18 mà chưa phục vụ xong, **không đơn nào khác được dùng số 18**. Web phải báo lỗi rõ ràng, ví dụ *"Số 18 đang được dùng cho đơn #12"*.

> Số 18 **vẫn được lưu trên đơn** sau khi phục vụ hoặc hủy, để tra cứu lịch sử. "Giải phóng" nghĩa là số không còn bị khóa, **không** có nghĩa là xóa giá trị khỏi đơn.

### 2.2. Hai loại trạng thái tách biệt

Hiện backend chỉ có **trạng thái thanh toán**. Chủ quán cần thêm **trạng thái phục vụ**. Hai trạng thái **độc lập** với nhau:

| Trạng thái thanh toán `trangThaiThanhToan` (đã có) | Trạng thái phục vụ `trangThaiPhucVu` (**mới**) |
|---|---|
| `PENDING`: chưa thanh toán | `WAITING`: chờ phục vụ (mặc định khi tạo đơn) |
| `PAID`: đã thanh toán | `PREPARING`: đang chuẩn bị |
| `CANCELLED`: đã hủy | `SERVED`: đã phục vụ |

- "Đã hủy" vẫn **chỉ** dùng `trangThaiThanhToan = CANCELLED`. **Không** thêm giá trị `CANCELLED` cho `trangThaiPhucVu`.
- Web admin hiển thị cột "Trạng thái" theo quy tắc: nếu `CANCELLED` thì "Đã hủy", ngược lại hiện theo `trangThaiPhucVu`.
- Cột "Thanh toán" ghép hình thức với tình trạng, ví dụ "Tiền mặt · Đã thanh toán".

### 2.3. Sơ đồ chuyển trạng thái phục vụ

```
           "Bắt đầu chuẩn bị"               "Xác nhận đã phục vụ"
  WAITING ───────────────────▶ PREPARING ───────────────────▶ SERVED
              PUT {PREPARING}                 PUT {SERVED}

  ✗ WAITING ──▶ SERVED (bỏ qua bước chuẩn bị) bị CHẶN: 409
  ✗ Đi lùi bị CHẶN: 409
```

- **Đi lần lượt từng bước, không bỏ qua, không đi lùi.** Chưa bắt đầu chuẩn bị thì không thể xác nhận đã phục vụ (yêu cầu của chủ quán: chưa pha chế thì chưa thể mang món ra). `SERVED` là trạng thái cuối.
- Trên web admin, mỗi lúc chỉ hiện **một nút** cho bước kế tiếp: đơn "Chờ phục vụ" có nút **Bắt đầu chuẩn bị**, đơn "Đang chuẩn bị" có nút **Xác nhận đã phục vụ**.
- Đơn đã `CANCELLED` thì **không** đổi trạng thái phục vụ được nữa.
- **Không** bắt buộc thanh toán trước khi phục vụ, vì quán có cả kiểu "phục vụ trước, thu tiền sau". Web admin đã hỏi xác nhận lại khi phục vụ đơn chưa thanh toán (xem mục 10).

### 2.4. Định nghĩa "đơn đang giữ số" (dùng cho khóa số)

```
SoNhanMon IS NOT NULL
AND TrangThaiPhucVu IN ('WAITING', 'PREPARING')
AND TrangThaiThanhToan <> 'CANCELLED'
```

Hủy đơn (`POST huy`) **tự động** giải phóng số, vì đơn rơi khỏi điều kiện trên. Backend **không** cần sửa API hủy.

### 2.5. Ma trận được phép

| Thao tác | WAITING | PREPARING | SERVED | CANCELLED |
|---|:-:|:-:|:-:|:-:|
| Gán / đổi / bỏ số nhận món | ✅ | ✅ | ❌ 409 | ❌ 409 |
| Chuyển sang `PREPARING` | ✅ | ❌ 409 | ❌ 409 | ❌ 409 |
| Chuyển sang `SERVED` | ❌ 409 (phải chuẩn bị trước) | ✅ | ❌ 409 | ❌ 409 |
| Sửa món (`PUT don-hang/{id}`) | ✅ nếu `PENDING` | ✅ nếu `PENDING` | ❌ 409 | ❌ 409 |
| Thanh toán (đã có) | ✅ nếu `PENDING` | ✅ nếu `PENDING` | ✅ nếu `PENDING` | ❌ |
| Hủy (đã có, chỉ QuanLy) | ✅ | ✅ | ✅ | ❌ |

---

## 3. Thay đổi cơ sở dữ liệu (SQL)

Đề xuất tạo file **`database/ThemSoNhanMon_TrangThaiPhucVu.sql`**. File này chạy được nhiều lần mà không lỗi, theo đúng kiểu `BoSoBan_ThuTuHienThi.sql`. Đồng thời cập nhật `QuanCaPheDB.sql` cho DB tạo mới.

```sql
-- =====================================================================
-- Thêm Số nhận món + Trạng thái phục vụ cho DonHang.
-- Chạy 1 lần trên DB QuanCaPheDB đã tồn tại. Chạy lại nhiều lần không lỗi.
-- =====================================================================

-- 1) Cột số nhận món: số trên bảng gỗ 1..50, NULL = chưa nhập
IF COL_LENGTH('DonHang', 'SoNhanMon') IS NULL
    ALTER TABLE DonHang ADD SoNhanMon INT NULL;
GO

-- 2) Cột trạng thái phục vụ: mặc định WAITING
IF COL_LENGTH('DonHang', 'TrangThaiPhucVu') IS NULL
    ALTER TABLE DonHang ADD TrangThaiPhucVu VARCHAR(20) NOT NULL
        CONSTRAINT DF_DonHang_TrangThaiPhucVu DEFAULT 'WAITING';
GO

-- 3) Dữ liệu cũ: đơn đã thanh toán / đã hủy coi như đã phục vụ xong
--    (tránh hàng trăm đơn cũ hiện "Chờ phục vụ"). Chỉ chạy đúng 1 lần, ngay sau khi thêm cột.
UPDATE DonHang
SET TrangThaiPhucVu = 'SERVED'
WHERE TrangThaiThanhToan IN ('PAID', 'CANCELLED')
  AND TrangThaiPhucVu = 'WAITING'
  AND ThoiGianTao < CAST(GETDATE() AS DATE);   -- giữ nguyên đơn hôm nay để nhân viên tự xử lý
GO

-- 4) Ràng buộc giá trị
IF OBJECT_ID('CK_DonHang_SoNhanMon') IS NULL
    ALTER TABLE DonHang ADD CONSTRAINT CK_DonHang_SoNhanMon
        CHECK (SoNhanMon IS NULL OR SoNhanMon BETWEEN 1 AND 50);
IF OBJECT_ID('CK_DonHang_TrangThaiPhucVu') IS NULL
    ALTER TABLE DonHang ADD CONSTRAINT CK_DonHang_TrangThaiPhucVu
        CHECK (TrangThaiPhucVu IN ('WAITING', 'PREPARING', 'SERVED'));
GO

-- 5) CHỐNG TRÙNG SỐ ở mức DB (an toàn khi 2 máy bấm cùng lúc):
--    trong các đơn "đang giữ số", mỗi số chỉ xuất hiện 1 lần.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DonHang_SoNhanMon_DangGiu')
    CREATE UNIQUE NONCLUSTERED INDEX UX_DonHang_SoNhanMon_DangGiu
        ON DonHang (SoNhanMon)
        WHERE SoNhanMon IS NOT NULL
          AND TrangThaiPhucVu IN ('WAITING', 'PREPARING')
          AND TrangThaiThanhToan <> 'CANCELLED';
GO

-- 6) Index phục vụ lọc danh sách theo trạng thái phục vụ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DonHang_TrangThaiPhucVu')
    CREATE NONCLUSTERED INDEX IX_DonHang_TrangThaiPhucVu
        ON DonHang (TrangThaiPhucVu, ThoiGianTao DESC);
GO
```

**Lưu ý:**
- Filtered unique index (bước 5) cần `SET QUOTED_IDENTIFIER ON` và `ANSI_NULLS ON` khi INSERT/UPDATE. SqlClient/EF Core đã bật sẵn hai tùy chọn này. Nếu chạy script bằng tay trong SSMS thì cũng đã mặc định ON.
- Trong `QuanCaPheDB.sql`, thêm 2 cột vào `CREATE TABLE DonHang` cùng các constraint và index tương ứng (bỏ qua bước 3).
- `sp_ThanhToanVaTruKho` **không cần sửa**.

---

## 4. Thay đổi Entity, DTO, Mapping

### 4.1. `Data/Entities.cs`

```csharp
public class DonHang
{
    // ... các trường cũ giữ nguyên ...
    public int? SoNhanMon { get; set; }                                   // 1..50, null = chưa nhập
    public string TrangThaiPhucVu { get; set; } = TrangThaiPhucVus.Waiting; // WAITING | PREPARING | SERVED
}

public static class TrangThaiPhucVus
{
    public const string Waiting = "WAITING";
    public const string Preparing = "PREPARING";
    public const string Served = "SERVED";

    /// <summary>Thứ tự để chặn chuyển lùi.</summary>
    public static int ThuTu(string s) => s switch { Waiting => 0, Preparing => 1, Served => 2, _ => -1 };
}

public static class SoNhanMons
{
    public const int Min = 1;
    public const int Max = 50;
}
```

### 4.2. `Data/AppDbContext.cs`, trong `b.Entity<DonHang>(e => { ... })`

```csharp
e.Property(x => x.TrangThaiPhucVu).HasMaxLength(20).IsUnicode(false).HasDefaultValue(TrangThaiPhucVus.Waiting);
```

### 4.3. `Dtos/Dtos.cs`

```csharp
// DonHangDto: thêm 2 trường. Đặt TRƯỚC ChiTiet để không vỡ thứ tự tham số.
public record DonHangDto(
    int MaDonHang, int? MaNguoiDung, string? TenNhanVien, decimal TongTien,
    string PhuongThucThanhToan, string TrangThaiThanhToan, DateTime ThoiGianTao,
    string? LyDoHuy, bool DaHoanTien, DateTime? GioHoanTien,
    int? SoNhanMon, string TrangThaiPhucVu,                                   // MỚI
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ChiTietDonDto>? ChiTiet);

// DonHangQuery: thêm tham số lọc
public class DonHangQuery : PageQuery
{
    [AllowedValues("PENDING", "PAID", "CANCELLED", null)] public string? TrangThai { get; set; }
    [AllowedValues("WAITING", "PREPARING", "SERVED", null)] public string? TrangThaiPhucVu { get; set; } // MỚI
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
}

// Request mới
public class SoNhanMonRequest
{
    /// <summary>null = bỏ số đang gán</summary>
    [Range(1, 50)] public int? SoNhanMon { get; set; }
}

public class TrangThaiPhucVuRequest
{
    [Required, AllowedValues("PREPARING", "SERVED")] public string TrangThaiPhucVu { get; set; } = "";
}

// Sửa đơn: DÙNG LẠI DatMonRequest (cùng cấu trúc với POST don-hang), không tạo class mới.
```

> **Chú ý:** request body phải **đúng tên trường**, vì backend đang bật `UnmappedMemberHandling.Disallow`. Web admin gửi đúng `{ "soNhanMon": 18 }` và `{ "trangThaiPhucVu": "SERVED" }`.

### 4.4. `Common/Mapping.cs`, hàm `ToDto(this DonHang d, ...)`

Thêm `d.SoNhanMon, d.TrangThaiPhucVu` vào sau `d.GioHoanTien`.

---

## 5. Đặc tả từng API

Quy ước chung (giống các API hiện có):
- Tiền tố `api/v1/`, JSON camelCase, cần header `Authorization: Bearer <token>`.
- Lỗi theo dạng `{ "error": { "code", "message", "details" } }`, **message bằng tiếng Việt**. Web admin hiển thị nguyên văn `message` cho nhân viên đọc.
- Quyền: `NhanVien` và `QuanLy` (`VaiTros.TatCa`), trừ khi ghi khác.

### 5.1. `PUT api/v1/don-hang/{id}/so-nhan-mon`: gán / đổi / bỏ số nhận món

**Request**
```json
{ "soNhanMon": 18 }
```
`{ "soNhanMon": null }` = bỏ số đang gán.

**Response 200**: `DonHangDto` đầy đủ (có `chiTiet`).
```json
{
  "maDonHang": 21,
  "maNguoiDung": null,
  "tenNhanVien": null,
  "tongTien": 74000,
  "phuongThucThanhToan": "CASH",
  "trangThaiThanhToan": "PENDING",
  "thoiGianTao": "2026-11-11T15:23:00",
  "lyDoHuy": null,
  "daHoanTien": false,
  "gioHoanTien": null,
  "soNhanMon": 18,
  "trangThaiPhucVu": "WAITING",
  "chiTiet": [ ... ]
}
```

**Lỗi**

| HTTP | `code` | Khi nào | `message` gợi ý |
|---|---|---|---|
| 400 | `VALIDATION_ERROR` | Số ngoài khoảng 1–50, hoặc body sai | (do validation tự sinh) |
| 404 | `NOT_FOUND` | Không có đơn `{id}` | `Đơn hàng không tồn tại` |
| 409 | **`SO_NHAN_MON_DANG_DUNG`** | Số đang được giữ bởi đơn khác | `Số 18 đang được dùng cho đơn #12 (chưa phục vụ xong). Hãy chọn số khác.` |
| 409 | `CONFLICT` | Đơn đã `SERVED` hoặc `CANCELLED` | `Đơn đã phục vụ hoặc đã hủy, không đổi được số nhận món` |

- Gửi lại **đúng số đơn đang có** → 200, không coi là trùng với chính nó.
- Với `SO_NHAN_MON_DANG_DUNG`, nên trả thêm `details`: `{ "soNhanMon": 18, "maDonHangDangGiu": 12 }`.
- Hai máy cùng gán số 18 một lúc: unique index (mục 3) chặn máy thứ hai. Backend bắt `DbUpdateException` với `SqlException.Number` là **2601** hoặc **2627** rồi trả 409 `SO_NHAN_MON_DANG_DUNG` (xem code mục 6).

### 5.2. `PUT api/v1/don-hang/{id}/trang-thai-phuc-vu`: chuyển trạng thái phục vụ

**Request**
```json
{ "trangThaiPhucVu": "PREPARING" }
```
Giá trị hợp lệ: `PREPARING`, `SERVED`. Không cho gửi `WAITING`, vì không có thao tác lùi.

**Response 200**: `DonHangDto`.

**Lỗi**

| HTTP | `code` | Khi nào | `message` gợi ý |
|---|---|---|---|
| 400 | `VALIDATION_ERROR` | Giá trị không hợp lệ | |
| 404 | `NOT_FOUND` | Không có đơn | `Đơn hàng không tồn tại` |
| 409 | `CONFLICT` | Đơn đã hủy | `Đơn đã bị hủy` |
| 409 | `CONFLICT` | Bỏ qua bước: `WAITING` sang `SERVED` | `Đơn chưa bắt đầu chuẩn bị, không thể xác nhận đã phục vụ` |
| 409 | `CONFLICT` | Chuyển lùi hoặc giữ nguyên (vd. `SERVED` sang `PREPARING`) | `Đơn đang ở trạng thái Đã phục vụ, không chuyển sang Đang chuẩn bị được` |

Quy tắc kiểm tra: **chỉ chấp nhận đúng bước kế tiếp**, tức `ThuTu(mới) == ThuTu(hiện tại) + 1`.

Chuyển sang `SERVED` thì số nhận món **tự giải phóng** nhờ điều kiện ở mục 2.4. **Không** đặt `SoNhanMon = NULL`.

### 5.3. `GET api/v1/don-hang`: thêm lọc `trangThaiPhucVu`

```
GET api/v1/don-hang?page=1&limit=20&trangThaiPhucVu=PREPARING
```
- Khi có `trangThaiPhucVu`, **loại đơn đã hủy** khỏi kết quả: `TrangThaiThanhToan <> 'CANCELLED'`.
- Kết hợp được với `trangThai`, `tuNgay`, `denNgay` như hiện nay.
- Web admin lọc "Đã hủy" bằng `trangThai=CANCELLED` (tham số đã có).

### 5.3b. `GET api/v1/don-hang`: thêm tham số sắp xếp `sapXep` (bổ sung 12/11/2026)

```
GET api/v1/don-hang?page=1&limit=20&sapXep=-tongTien
```

| Giá trị `sapXep` | Thứ tự |
|---|---|
| (bỏ trống) | Như hiện nay: `ThoiGianTao` giảm dần, rồi `MaDonHang` giảm dần (mới nhất trước) |
| `maDonHang` / `-maDonHang` | Mã đơn tăng / giảm dần |
| `tongTien` / `-tongTien` | Tổng tiền tăng / giảm dần; **bằng nhau thì `MaDonHang` giảm dần** để thứ tự giữa các trang ổn định |

- Sắp xếp **trước khi** `Skip/Take` (phân trang), để trang 2 nối tiếp đúng trang 1. Web admin cần điều này: sắp xếp chỉ trong 20 đơn của một trang là sai.
- Giá trị khác danh sách trên → 400 `VALIDATION_ERROR` (giống các tham số khác; ví dụ `[AllowedValues("maDonHang", "-maDonHang", "tongTien", "-tongTien", null)]` trên `DonHangQuery.SapXep`).
- Kết hợp được với mọi bộ lọc (`trangThai`, `trangThaiPhucVu`, `tuNgay`, `denNgay`).
- Gợi ý code trong `DonHangController.DanhSach`:

```csharp
query = q.SapXep switch
{
    "maDonHang"  => query.OrderBy(d => d.MaDonHang),
    "-maDonHang" => query.OrderByDescending(d => d.MaDonHang),
    "tongTien"   => query.OrderBy(d => d.TongTien).ThenByDescending(d => d.MaDonHang),
    "-tongTien"  => query.OrderByDescending(d => d.TongTien).ThenByDescending(d => d.MaDonHang),
    _            => query.OrderByDescending(d => d.ThoiGianTao).ThenByDescending(d => d.MaDonHang)
};
```

- **Tương thích:** backend hiện tại bỏ qua tham số lạ trên query string, nên web gửi `sapXep` không gây lỗi. Khi máy chủ chưa hỗ trợ (danh sách trả về không đúng thứ tự), web tự sắp xếp trong trang và hiện ghi chú "Máy chủ chưa hỗ trợ sắp xếp toàn bộ danh sách".
- **Lưu ý `tuNgay`:** web giờ gửi mốc chính xác đến giờ phút (vd. `tuNgay=2026-11-11T13:27:43.000` cho "24 giờ qua"), không còn cắt về 00:00. Backend đang so sánh `ThoiGianTao >= tuNgay` nên không cần sửa.

### 5.4. `PUT api/v1/don-hang/{id}`: sửa món của đơn

**Request**: **giống hệt** `POST api/v1/don-hang`:
```json
{
  "phuongThucThanhToan": "CASH",
  "chiTiet": [
    { "maChiTietMon": 5, "soLuong": 1, "ghiChu": "ít đá", "toppings": [1, 2] },
    { "maChiTietMon": 12, "soLuong": 1, "ghiChu": null, "toppings": null }
  ]
}
```
- `chiTiet` **thay thế toàn bộ** danh sách món cũ. Web admin luôn gửi đủ danh sách sau khi sửa.
- `phuongThucThanhToan` = `null` thì giữ nguyên giá trị cũ.

**Response 200**: `DonHangDto`, với `tongTien` đã tính lại.

**Xử lý:**
1. Chỉ cho sửa khi `TrangThaiThanhToan = PENDING` **và** `TrangThaiPhucVu <> SERVED`. Ngược lại trả 409 `CONFLICT` với message `Chỉ sửa được đơn chưa thanh toán, chưa hủy và chưa phục vụ`.
2. Kiểm tra món, size, topping và tồn kho **giống `DatMon`**. Cùng các mã lỗi 404 / 422 `MON_NGUNG_BAN`, `MON_HET_HANG`, `SIZE_KHONG_AP_DUNG`, `TOPPING_HET_HANG`, `HET_NGUYEN_LIEU`. Nên tách phần dựng chi tiết của `DatMon` ra một hàm dùng chung (xem mục 6).
3. Giá khuyến mãi: **áp theo `ThoiGianTao` của đơn**, không theo giờ sửa, để khách không bị đổi giá khi qua khung giờ vàng. Mock của web admin đang làm theo cách này.
4. Xóa các dòng `ChiTietDonHang_Topping` rồi `ChiTietDonHang` cũ, thêm dòng mới, tính lại `TongTien`. Làm trong **một transaction**. SQL hiện **không có ON DELETE CASCADE**, nên phải xóa topping trước.
5. **Không** trừ kho. Kho chỉ trừ lúc thanh toán bằng `sp_ThanhToanVaTruKho`, như hiện nay.
6. Giữ nguyên `MaNguoiDung`, `ThoiGianTao`, `SoNhanMon`, `TrangThaiPhucVu`.

### 5.5. Các API hiện có: chỉ thay đổi response

- `POST don-hang`: đơn mới có `trangThaiPhucVu = "WAITING"` và `soNhanMon = null`. Tablet không phải gửi thêm gì.
- `GET don-hang`, `GET don-hang/{id}`, `POST thanh-toan` (trong `donHang`), `POST huy`: response có thêm 2 trường.

---

## 6. Code mẫu gợi ý (Controller + Service)

Viết theo đúng phong cách hiện tại của `DonHangController` / `DonHangService`. Nhóm backend có thể điều chỉnh.

### 6.1. `Controllers/DonHangController.cs`

```csharp
/// <summary>PUT /don-hang/{id} — sửa món của đơn chưa thanh toán, chưa phục vụ</summary>
[HttpPut("{id:int}"), Authorize(Roles = VaiTros.TatCa)]
public async Task<DonHangDto> Sua(int id, DatMonRequest req) => (await service.SuaDon(id, req)).ToDto();

/// <summary>PUT /don-hang/{id}/so-nhan-mon — gán / đổi / bỏ số nhận món</summary>
[HttpPut("{id:int}/so-nhan-mon"), Authorize(Roles = VaiTros.TatCa)]
public async Task<DonHangDto> SoNhanMon(int id, SoNhanMonRequest req) =>
    (await service.GanSoNhanMon(id, req.SoNhanMon)).ToDto();

/// <summary>PUT /don-hang/{id}/trang-thai-phuc-vu — WAITING → PREPARING → SERVED</summary>
[HttpPut("{id:int}/trang-thai-phuc-vu"), Authorize(Roles = VaiTros.TatCa)]
public async Task<DonHangDto> TrangThaiPhucVu(int id, TrangThaiPhucVuRequest req) =>
    (await service.DoiTrangThaiPhucVu(id, req.TrangThaiPhucVu)).ToDto();
```

Trong `DanhSach(...)`, thêm:
```csharp
if (q.TrangThaiPhucVu is not null)
    query = query.Where(d => d.TrangThaiPhucVu == q.TrangThaiPhucVu && d.TrangThaiThanhToan != TrangThais.Cancelled);
```

### 6.2. `Services/DonHangService.cs`

```csharp
// ------------------------------------------------------------------ SỐ NHẬN MÓN
private static bool DangGiuSo(DonHang d) =>
    d.TrangThaiThanhToan != TrangThais.Cancelled && d.TrangThaiPhucVu != TrangThaiPhucVus.Served;

public async Task<DonHang> GanSoNhanMon(int id, int? so)
{
    var don = await Tim(id);
    if (!DangGiuSo(don))
        throw ApiException.Conflict("Đơn đã phục vụ hoặc đã hủy, không đổi được số nhận món");
    if (don.SoNhanMon == so) return don; // không đổi gì

    if (so is { } n)
    {
        var dangGiu = await db.DonHangs
            .Where(d => d.MaDonHang != id && d.SoNhanMon == n
                        && d.TrangThaiThanhToan != TrangThais.Cancelled
                        && d.TrangThaiPhucVu != TrangThaiPhucVus.Served)
            .Select(d => (int?)d.MaDonHang)
            .FirstOrDefaultAsync();
        if (dangGiu is { } maDon) throw SoDangDung(n, maDon);
    }

    don.SoNhanMon = so;
    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
    {
        // 2 máy gán cùng số đúng một lúc → unique index UX_DonHang_SoNhanMon_DangGiu chặn máy sau
        throw SoDangDung(so!.Value, null);
    }
    return don;
}

private static ApiException SoDangDung(int so, int? maDon) => new(409, "SO_NHAN_MON_DANG_DUNG",
    maDon is { } m
        ? $"Số {so} đang được dùng cho đơn #{m} (chưa phục vụ xong). Hãy chọn số khác."
        : $"Số {so} vừa được gán cho đơn khác. Hãy chọn số khác.",
    new { soNhanMon = so, maDonHangDangGiu = maDon });

// ------------------------------------------------------------------ TRẠNG THÁI PHỤC VỤ
public async Task<DonHang> DoiTrangThaiPhucVu(int id, string trangThaiMoi)
{
    var don = await Tim(id);
    if (don.TrangThaiThanhToan == TrangThais.Cancelled) throw ApiException.Conflict("Đơn đã bị hủy");
    // Chỉ cho sang ĐÚNG bước kế tiếp: WAITING → PREPARING → SERVED (không bỏ qua, không lùi)
    if (TrangThaiPhucVus.ThuTu(trangThaiMoi) != TrangThaiPhucVus.ThuTu(don.TrangThaiPhucVu) + 1)
        throw ApiException.Conflict(
            don.TrangThaiPhucVu == TrangThaiPhucVus.Waiting && trangThaiMoi == TrangThaiPhucVus.Served
                ? "Đơn chưa bắt đầu chuẩn bị, không thể xác nhận đã phục vụ"
                : $"Đơn đang ở trạng thái {TenTrangThai(don.TrangThaiPhucVu)}, không chuyển sang {TenTrangThai(trangThaiMoi)} được");

    don.TrangThaiPhucVu = trangThaiMoi; // SERVED → số nhận món tự hết bị khóa (vẫn giữ giá trị)
    await db.SaveChangesAsync();
    return don;
}

private static string TenTrangThai(string s) => s switch
{
    TrangThaiPhucVus.Waiting => "Chờ phục vụ",
    TrangThaiPhucVus.Preparing => "Đang chuẩn bị",
    TrangThaiPhucVus.Served => "Đã phục vụ",
    _ => s
};

// ------------------------------------------------------------------ SỬA ĐƠN
public async Task<DonHang> SuaDon(int id, DatMonRequest req)
{
    var don = await Tim(id);
    if (don.TrangThaiThanhToan != TrangThais.Pending || don.TrangThaiPhucVu == TrangThaiPhucVus.Served)
        throw ApiException.Conflict("Chỉ sửa được đơn chưa thanh toán, chưa hủy và chưa phục vụ");

    // Tách phần dựng chi tiết + kiểm tra món/topping/tồn kho từ DatMon thành hàm dùng chung:
    //   Task<List<ChiTietDonHang>> DungChiTiet(DatMonRequest req, DateTime thoiDiemGia)
    // DatMon gọi DungChiTiet(req, DateTime.Now); SuaDon gọi DungChiTiet(req, don.ThoiGianTao).
    var chiTietMoi = await DungChiTiet(req, don.ThoiGianTao);

    await using var tx = await db.Database.BeginTransactionAsync();
    db.ChiTietDonHangToppings.RemoveRange(don.ChiTiets.SelectMany(c => c.Toppings)); // không có ON DELETE CASCADE
    db.ChiTietDonHangs.RemoveRange(don.ChiTiets);
    await db.SaveChangesAsync();

    don.ChiTiets = chiTietMoi;
    don.TongTien = chiTietMoi.Sum(c => (c.DonGiaBan + c.Toppings.Sum(t => t.DonGiaTopping)) * c.SoLuong);
    if (req.PhuongThucThanhToan is { } pt) don.PhuongThucThanhToan = pt;
    await db.SaveChangesAsync();
    await tx.CommitAsync();

    db.ChangeTracker.Clear();
    return await Tim(id);
}
```

---

## 7. Bổ sung `openapi.yaml`

```yaml
# components/schemas — thêm vào DonHang.properties
        soNhanMon: { type: integer, nullable: true, minimum: 1, maximum: 50, example: 18,
                     description: 'Số trên bảng gỗ đưa cho khách. Bị khóa khi đơn chưa phục vụ xong.' }
        trangThaiPhucVu: { $ref: '#/components/schemas/TrangThaiPhucVu' }

    TrangThaiPhucVu:
      type: string
      enum: [WAITING, PREPARING, SERVED]
      description: 'WAITING = chờ phục vụ, PREPARING = đang chuẩn bị, SERVED = đã phục vụ. Đã hủy xem trangThaiThanhToan = CANCELLED.'
    SoNhanMonRequest:
      type: object
      properties:
        soNhanMon: { type: integer, nullable: true, minimum: 1, maximum: 50 }
    TrangThaiPhucVuRequest:
      type: object
      required: [trangThaiPhucVu]
      properties:
        trangThaiPhucVu: { type: string, enum: [PREPARING, SERVED] }

# paths
  /don-hang:
    get:
      parameters:
        # ... thêm:
        - { name: trangThaiPhucVu, in: query, schema: { $ref: '#/components/schemas/TrangThaiPhucVu' } }

  /don-hang/{id}:
    put:
      tags: [DonHang]
      summary: Sửa món của đơn (chỉ đơn PENDING, chưa SERVED) — thay toàn bộ chi tiết
      requestBody: { required: true, content: { application/json: { schema: { $ref: '#/components/schemas/DatMonRequest' } } } }
      responses:
        '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/DonHang' } } } }
        '409': { description: Đơn đã thanh toán / đã hủy / đã phục vụ }
        '422': { description: Hết món / hết nguyên liệu (giống POST don-hang) }

  /don-hang/{id}/so-nhan-mon:
    put:
      tags: [DonHang]
      summary: Gán / đổi / bỏ số nhận món
      requestBody: { required: true, content: { application/json: { schema: { $ref: '#/components/schemas/SoNhanMonRequest' } } } }
      responses:
        '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/DonHang' } } } }
        '409': { description: 'SO_NHAN_MON_DANG_DUNG (số đang dùng ở đơn khác) hoặc CONFLICT (đơn đã phục vụ/hủy)' }

  /don-hang/{id}/trang-thai-phuc-vu:
    put:
      tags: [DonHang]
      summary: Chuyển trạng thái phục vụ sang đúng bước kế tiếp (WAITING → PREPARING → SERVED)
      requestBody: { required: true, content: { application/json: { schema: { $ref: '#/components/schemas/TrangThaiPhucVuRequest' } } } }
      responses:
        '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/DonHang' } } } }
        '409': { description: Đơn đã hủy, bỏ qua bước (WAITING → SERVED) hoặc chuyển lùi }
```

---

## 8. Danh sách test (Swagger / `.http`)

Chuẩn bị: đăng nhập `nhanvien` để lấy token. Tạo 3 đơn A, B, C bằng `POST don-hang`.

| # | Thao tác | Kết quả mong đợi |
|---|---|---|
| T1 | `GET don-hang/{A}` | Có `soNhanMon: null` và `trangThaiPhucVu: "WAITING"` |
| T2 | `PUT A/so-nhan-mon {18}` | 200, `soNhanMon = 18` |
| T3 | `PUT B/so-nhan-mon {18}` | **409 `SO_NHAN_MON_DANG_DUNG`**, message nêu đơn A |
| T4 | `PUT A/so-nhan-mon {18}` (gửi lại số cũ) | 200, không lỗi |
| T5 | `PUT A/so-nhan-mon {0}` và `{51}` | 400 |
| T5b | `PUT A/trang-thai-phuc-vu {SERVED}` khi A còn `WAITING` | **409**, `Đơn chưa bắt đầu chuẩn bị…` (không bỏ qua bước) |
| T6 | `PUT A/trang-thai-phuc-vu {PREPARING}` | 200 |
| T7 | `PUT A/trang-thai-phuc-vu {PREPARING}` (lần 2) | 409 (không chuyển lùi hoặc giữ nguyên) |
| T8 | `PUT A/trang-thai-phuc-vu {SERVED}` | 200, `soNhanMon` **vẫn = 18** |
| T9 | `PUT B/so-nhan-mon {18}` | **200**, vì số 18 đã được giải phóng |
| T10 | `PUT A/so-nhan-mon {5}` | 409 `CONFLICT` (A đã phục vụ) |
| T11 | `PUT C/so-nhan-mon {7}`, rồi QuanLy `POST C/huy` | Hủy OK. Sau đó `PUT B/so-nhan-mon {7}` → 200 (hủy đã giải phóng số) |
| T12 | `GET don-hang?trangThaiPhucVu=SERVED` | Có A, **không** có C (đã hủy) |
| T13 | `PUT don-hang/{B}` với `chiTiet` mới | 200, `tongTien` tính lại, `soNhanMon` giữ nguyên |
| T14 | `POST B/thanh-toan`, rồi `PUT don-hang/{B}` | 409 (đã thanh toán) |
| T15 | `PUT don-hang/{id}` có món hết nguyên liệu | 422 `HET_NGUYEN_LIEU` (giống POST) |
| T16 | Gửi body thừa trường, vd. `{ "soNhanMon": 1, "abc": 1 }` | 400 (giống hành vi hiện tại) |
| T17 | Hai request `PUT so-nhan-mon {20}` gửi **đồng thời** cho 2 đơn khác nhau | Một 200, một 409 `SO_NHAN_MON_DANG_DUNG` (không bao giờ cả hai 200) |
| T18 | `POST thanh-toan` một đơn đang `PREPARING` | 200, `trangThaiPhucVu` vẫn `PREPARING` (thanh toán không đổi trạng thái phục vụ) |

---

## 9. Ảnh hưởng tới phần khác & tương thích ngược

| Thành phần | Ảnh hưởng |
|---|---|
| **Tablet (Flutter)** | Không phải sửa. Response `POST don-hang` chỉ có thêm 2 trường, nên app bỏ qua trường lạ là được |
| **Báo cáo** (`bao-cao/*`) | Không đổi. Doanh thu vẫn tính theo `PAID` |
| **Kho / `sp_ThanhToanVaTruKho`** | Không đổi. Sửa đơn chưa thanh toán không đụng tới kho |
| **Web Admin** | Đã làm xong. Khi backend chưa trả `trangThaiPhucVu`, web tự ẩn phần số nhận món / phục vụ và khóa bộ lọc phục vụ. Khi backend deploy xong, web dùng được ngay, **không cần build lại web** |
| **Dữ liệu cũ** | Script mục 3 chuyển đơn cũ đã thanh toán / hủy sang `SERVED` |

---

## 10. Việc còn mở, cần chốt

| # | Câu hỏi | Đề xuất của nhóm Web Admin |
|---|---|---|
| Q1 | Có **bắt buộc** thanh toán trước khi chuyển `SERVED` không? | Không bắt buộc ở backend. Web đã hỏi xác nhận lại khi phục vụ đơn chưa thanh toán |
| Q2 | Có cho **hủy đơn đã `SERVED`** không? | Chờ chủ quán quyết định. Hiện giữ như cũ (cho hủy, đơn PAID thì đánh dấu hoàn tiền) |
| Q3 | **Khách chuyển khoản trên tablet → tự `PAID`**: ai gọi thanh toán? | Cần chốt giữa tablet và backend. (a) Cổng thanh toán (VietQR/PayOS…) gọi webhook backend, backend chạy logic `ThanhToan`. (b) Tạm thời: tablet gọi `POST thanh-toan` sau khi khách xác nhận đã chuyển (kém an toàn hơn). Web admin tự làm mới mỗi 10 giây nên sẽ thấy ngay |
| Q4 | Số bảng gỗ có cố định **50** không? | Tạm cố định 1–50 (CHECK + validation). Nếu cần đổi thì đưa vào cấu hình |
| Q5 | Có cần **tìm kiếm theo mã đơn / số nhận món ở máy chủ** không? | Nên có: `GET don-hang?tuKhoa=18` khớp `MaDonHang` hoặc `SoNhanMon`. Hiện web chỉ tìm trong trang đang xem (20 đơn) |

---

## 11. Checklist bàn giao

- [ ] Chạy `database/ThemSoNhanMon_TrangThaiPhucVu.sql` trên DB dev. Cập nhật `QuanCaPheDB.sql`
- [ ] Entity, DbContext, `DonHangDto`, `DonHangQuery`, 2 request mới, `Mapping.ToDto`
- [ ] 3 endpoint mới + lọc `trangThaiPhucVu` trong `GET don-hang`
- [ ] Bắt lỗi trùng index (2601/2627), trả 409 `SO_NHAN_MON_DANG_DUNG`
- [ ] Tách `DungChiTiet` dùng chung cho `DatMon` và `SuaDon`
- [ ] Cập nhật `openapi.yaml` và `CoffeeShop.Api.http`
- [ ] Chạy hết T1–T18 (kể cả T5b)
- [ ] Báo nhóm Web Admin để test chung với `Api:UseMock: false`

**Liên hệ:** nhóm Web Admin. Có thể chạy web admin ở chế độ giả lập (`03-Web-Admin/wwwroot/appsettings.json` → `"UseMock": true`) để xem đúng hành vi mong muốn. Logic giả lập nằm trong `Services/Mock/MockApiHandler.cs` (các hàm `SetPickupNumber`, `SetServingStatus`, `UpdateOrder`).
