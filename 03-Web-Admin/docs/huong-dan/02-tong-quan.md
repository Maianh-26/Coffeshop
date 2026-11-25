# 2. Trang Tổng quan (Dashboard)

← [Mục lục](README.md)

- **URL:** `/` · **Quyền:** chỉ chủ quán (`[Authorize(Roles = Roles.Admin)]`). Nhân viên mở `/` sẽ được chuyển sang `/orders`.
- **Giao diện:** `Pages/Dashboard/Dashboard.razor` · **Logic:** `Pages/Dashboard/Dashboard.razor.cs` (partial class `Dashboard`).
- **Gọi API:** `Services/DashboardService.cs` → `api/v1/bao-cao/doanh-thu`, `mon-ban-chay`, `gio-cao-diem`, `topping-ban-chay` (chỉ chủ quán, nhân viên bị 403).
- **Xuất Excel:** `Services/ReportExporter.cs` (nội dung 4 trang tính) + `Utilities/XlsxWriter.cs` (dựng file `.xlsx`) + `appHelpers.downloadFile` trong `wwwroot/js/app.js` (tải file).

---

## 2.1. Bố cục

```
┌─────────────────────────────────────────────────────────────┐
│ [Hôm nay ▾]  [⟳]                              [Xuất Excel]  │  bộ lọc (khung .filters-panel)
├───────────────────┬───────────────────┬─────────────────────┤
│ Doanh thu thuần   │ Đơn đã thanh toán │ Giá trị đơn TB      │  3 thẻ (.stat-grid)
│ Tiền mặt · QR     │ Chờ TT · Đã hủy   │ doanh thu ÷ số đơn  │
├───────────────────┴───────────────────┴─────────────────────┤
│ Biểu đồ doanh thu   Hôm nay, 23/11/2026                     │  biểu đồ cột (.chart-panel)
├─────────────────────────────────────────────────────────────┤
│ Món bán chạy (top 5)                                        │  bảng (.report-panel)
└─────────────────────────────────────────────────────────────┘
```

Trang **không** có bảng "Báo cáo theo ngày", bảng "Topping bán chạy" hay nút in PDF (đã bỏ theo yêu cầu chủ quán). Số liệu từng ngày xem bằng cách rê chuột vào cột biểu đồ hoặc trong file Excel.

---

## 2.2. Bộ lọc thời gian

Ô chọn dùng `DatePresets.ReportOptions` (`Utilities/DatePresets.cs`). Mốc nhanh áp dụng ngay; "Chọn khoảng ngày…" mở 2 ô Từ ngày / Đến ngày, chỉ lọc khi bấm **Áp dụng** (giống trang Đơn hàng, Kho).

| Lựa chọn | Khoảng gửi lên API | Biểu đồ |
|----------|--------------------|---------|
| **Hôm nay** (mặc định) | 00:00 → 23:59:59 hôm nay | Theo giờ, 00:00 → giờ hiện tại |
| Trong 24 giờ qua | Đầu giờ của 23 giờ trước → hết giờ hiện tại (đúng 24 khung giờ) | Theo giờ, 24 cột; cột 00:00 ghi thêm ngày |
| Trong 7 / 30 ngày qua | 00:00 của 7 / 30 ngày lịch gần nhất (tính cả hôm nay) → nay | Theo ngày |
| Chọn khoảng ngày… | 00:00 ngày đầu → 23:59:59 ngày cuối | 1 ngày → theo giờ; ≤ 62 ngày → theo ngày; dài hơn → theo tháng |

- Vì sao mặc định **Hôm nay**: chủ quán mở trang để xem hôm nay bán được bao nhiêu (giống Sapo, KiotViet). "24 giờ qua" lúc 8 giờ sáng vẫn gồm doanh thu tối hôm trước.
- **Không có "Tất cả thời gian"** (chủ quán quyết định 23/11/2026: quá chung chung cho báo cáo); xem khoảng dài thì dùng "Chọn khoảng ngày…".
- Trang Đơn hàng / Kho vẫn dùng `DatePresets.Options` (có "Tất cả thời gian", không có "Hôm nay"); mốc 7 / 30 ngày ở đó là **khoảng trượt** (7×24 giờ), còn ở Tổng quan là **ngày lịch** để mỗi cột biểu đồ là một ngày trọn vẹn.
- Nút **Làm mới** (⟳): tải lại; nếu đang lọc khác "Hôm nay" (có chấm nâu) thì đưa về Hôm nay.
- Khoảng đang xem ghi gọn cạnh tiêu đề biểu đồ (`Describe` trong `.razor.cs`): "Hôm nay, 23/11/2026", "17/11 – 23/11/2026", "Từ 15:00 22/11/2026 đến nay".

---

## 2.3. Ba thẻ số liệu — công thức

Tất cả lấy từ một lần gọi `GET api/v1/bao-cao/doanh-thu` (backend `BaoCaoController.DoanhThu`).

**Tiền một đơn** (lưu lúc đặt đơn): `Tổng tiền = Σ (giá món theo size sau khuyến mãi + giá topping) × số lượng`.

| Thẻ | Công thức | Dòng phụ |
|-----|-----------|----------|
| **Doanh thu thuần** | Σ tổng tiền các đơn **đã thanh toán** (`PAID`) có **giờ thu tiền** trong khoảng | Tiền mặt · QR (doanh thu chia theo cách trả) |
| **Đơn đã thanh toán** | Số đơn `PAID` có giờ thu tiền trong khoảng | Chờ thanh toán (`PENDING`) · Đã hủy (`CANCELLED`) — đếm theo **giờ tạo đơn** |
| **Giá trị đơn trung bình** | Doanh thu thuần ÷ số đơn đã thanh toán, làm tròn đồng (0 đơn → 0 đ) | Không có (đã bỏ dòng "X đ ÷ N đơn": chỉ lặp lại 2 số của thẻ 1 và 2) |

**Vì sao gọi là "doanh thu thuần"** (chủ quán đã xác nhận định nghĩa này ngày 23/11/2026): doanh thu thuần = doanh thu bán hàng − các khoản giảm trừ (giảm giá, hàng bán bị trả lại). Ở đây giá món đã trừ khuyến mãi, quán không thu thuế / phí dịch vụ, không có giảm giá theo cả đơn, và đơn hoàn tiền đã chuyển sang `CANCELLED` nên không được cộng → tổng tiền các đơn đã thanh toán chính là doanh thu thuần. Đây **không** phải lợi nhuận (backend không có giá vốn nguyên liệu).

Ví dụ: 6 đơn đã thanh toán, cộng 277.850 đ (tiền mặt 220.250 đ + QR 57.600 đ) → trung bình 277.850 ÷ 6 = 46.308 đ.

---

## 2.4. Biểu đồ doanh thu

Tự vẽ bằng HTML/CSS, không dùng thư viện. Tên cố định **"Biểu đồ doanh thu"** cho mọi khoảng lọc.

| Thành phần | Cách hoạt động |
|------------|----------------|
| Trục tiền (trái) | `AxisScale.For(max)`: tự co giãn theo mốc cao nhất, bước tròn 1 / 2 / 2,5 / 5 × 10ⁿ đ, tối đa 5 khoảng. Không có doanh thu → chỉ còn "0 đ" và dòng "Chưa có doanh thu trong khoảng thời gian này". Nhãn gọn: `200k đ`, `1,5tr đ` |
| Cột | Mảnh (tối đa 28px), giữa ô; chiều cao = doanh thu ÷ đỉnh trục. Không ghi số trên đầu cột |
| Thẻ số liệu | Rê chuột / chạm / Tab vào cột → tô nền ô và hiện mốc + doanh thu + số đơn, đặt ngay trên đỉnh cột; 1/3 số cột mỗi mép canh vào trong để không tràn khung |
| Nhãn thời gian | Giờ `HH:mm`, ngày `dd/MM`, tháng `MM/yyyy`; nhiều cột thì thưa nhãn (màn hình rộng ≤ ~15 nhãn, vùng vẽ hẹp ≤ ~6 nhãn — CSS container query) |
| Mốc 0 | Mọi giờ / ngày không bán vẫn có ô (cột 0) để trục thời gian liền mạch |

---

## 2.5. Món bán chạy

Top 5 món (`mon-ban-chay?soLuong=5`), xếp theo số lượng. Cột Doanh thu = tiền món **gồm topping gọi kèm** (`DoanhThuGomTopping`).

---

## 2.6. Xuất Excel (.xlsx)

Nút **Xuất Excel** tải ngay file báo cáo cho **đúng khoảng đang xem** (không có hộp thoại, không còn CSV / PDF). Tên file theo kỳ báo cáo: `BaoCaoDoanhThu_20261123.xlsx` (một ngày) hoặc `BaoCaoDoanhThu_20261117-20261123.xlsx`. Lúc bấm, trang tải lại toàn bộ số liệu của khoảng đó (cả top 50 món, top 50 topping) để mọi trang tính tính tới cùng một thời điểm.

File là **báo cáo số liệu** — không có chữ giải thích cách tính (chủ quán yêu cầu 23/11/2026). Nội dung file do `ReportExporter.Build` quyết định hoàn toàn; muốn đổi cấu trúc thì sửa ở đó.

Đầu mỗi trang tính: tên quán (+ địa chỉ, điện thoại nếu có — lấy từ mục `Receipt` trong `wwwroot/appsettings.json`, giống hóa đơn), tiêu đề, **Kỳ báo cáo: từ 00:00 17/11/2026 đến 15:02 23/11/2026** (mốc cuối không vượt quá lúc xuất), **Ngày lập**.

| Trang tính | Cột / nội dung |
|------------|----------------|
| Tổng hợp | **1. Doanh thu**: Khoản mục · Số tiền · Tỷ trọng — Tiền mặt, Chuyển khoản, **Doanh thu thuần** (dòng tổng), Giá trị trung bình / đơn. **2. Đơn hàng**: đã thanh toán, chờ thanh toán, đã hủy, hoàn tiền |
| Doanh thu theo ngày | Ngày · Thứ · Số đơn · Doanh thu thuần · Trung bình / đơn + dòng Tổng |
| Doanh thu theo giờ | Ngày · Khung giờ (09:00 – 10:00) · Số đơn · Doanh thu thuần · Trung bình / đơn + dòng Tổng |
| Doanh thu theo tháng | Tháng · Số đơn · Doanh thu thuần · Trung bình / đơn + dòng Tổng |
| Món bán chạy | Hạng · Tên món · Số lượng · Doanh thu món · Doanh thu topping kèm · Tổng doanh thu · Tỷ trọng (so với doanh thu thuần cả kỳ) + dòng Tổng |
| Topping bán chạy | Hạng · Tên topping · Số ly · Doanh thu + dòng Tổng; backend cũ chưa có báo cáo topping → "Máy chủ chưa hỗ trợ báo cáo topping" |

- Số tiền là **số thật** (`#,##0 đ`), ngày / tháng là **ô ngày thật** (`dd/mm/yyyy`, `mm/yyyy`) → lọc, sắp xếp, vẽ biểu đồ trong Excel được.
- **Tỷ trọng** (`0.0%`) chỉ có ở Tổng hợp (cơ cấu tiền mặt / chuyển khoản) và Món bán chạy (món đóng góp bao nhiêu % doanh thu thuần). Không có ở trang theo giờ / ngày / tháng (cột doanh thu đã đủ so sánh, 24 giờ / 30 ngày toàn "0,0%" gây rối) và trang Topping (tiền topping đã nằm trong doanh thu món → cộng tỷ trọng 2 trang sẽ trùng) — chủ quán chọn 23/11/2026.
- Dòng Tổng, cột Trung bình / đơn ở trang theo mốc và tỷ trọng ở Tổng hợp là **công thức** (`SUM`, `ROUND`, `IF`) → sửa số thì Excel tự tính lại.
- Dòng tiêu đề cột được cố định khi cuộn.
- `XlsxWriter` tự dựng file Office Open XML bằng `System.IO.Compression` (không thêm thư viện). Đã kiểm tra: `OpenXmlValidator` của Microsoft 0 lỗi, mở đúng trong WPS Spreadsheets và Microsoft Excel (chủ quán mở thử bản báo cáo số liệu 23/11/2026).

---

## 2.7. Tìm nhanh

| Muốn tìm | Gõ Ctrl+Shift+F |
|----------|-----------------|
| Mốc mặc định / danh sách lựa chọn | `DefaultPreset`, `ReportOptions` |
| Khoảng thời gian của từng mốc | `RangeOf` |
| Chữ khoảng ngày cạnh tiêu đề biểu đồ | `Describe` |
| Trục tiền | `AxisScale`, `AxisLabel` |
| Cột / thẻ số liệu biểu đồ | `bar-column`, `bar-tip`, `TipSide` |
| Xuất Excel | `ExportExcel`, `ReportExporter.Build`, `XlsxWriter` |
