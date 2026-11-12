# 2. Trang Tổng quan (Dashboard)

← [Mục lục](README.md)

- **URL:** `/` · **Quyền:** chỉ Admin (`Dashboard.razor:2` → `[Authorize(Roles = "Admin")]`). Nhân viên mở `/` sẽ bị chuyển sang `/orders` (`App.razor:19–23`).
- **Giao diện:** `Pages/Dashboard/Dashboard.razor` (311 dòng)
- **Logic:** `Pages/Dashboard/Dashboard.razor.cs` (156 dòng) — class `partial class Dashboard`, ghép với file `.razor` thành một component.

> ⚠️ **Trạng thái hiện tại — cần nói đúng khi bị hỏi:**
> Trang **chưa nối service**. Lúc mở trang, báo cáo là danh sách rỗng (`Dashboard.razor.cs:26–34`), nên các thẻ hiện "—", biểu đồ hiện "Chưa có dữ liệu.".
> Nút **Áp dụng** hiện chỉ cập nhật dòng mô tả khoảng thời gian. Nút **Xuất báo cáo** mới hiện thông báo "sẽ được thêm sau".
> Phần còn thiếu đã có sẵn: `DashboardService` (gọi API), `ReportExporter` + `wwwroot/js/app.js` (xuất CSV / in), và API giả `api/dashboard/report`. Chỉ còn bước gọi chúng từ trang (xem mục 2.7).

---

## 2.1. Bố cục trang

```
┌───────────────────────────────────────────────────────────────────────────┐
│ [Từ ngày] – [Đến ngày]  [Tùy chọn ▾]  [Áp dụng]           [Xuất báo cáo]  │ ← .dashboard-toolbar (razor 6–49)
│ Toàn bộ thời gian                                                          │ ← .filter-caption (razor 51–53)
├──────────────────────┬──────────────────────┬─────────────────────────────┤
│ Doanh thu đã TT      │ Đơn đã thanh toán    │ Chi phí nhập kho            │ ← .stat-grid (razor 62–134)
├──────────────────────┴──────────────────────┴─────────────────────────────┤
│ Doanh thu theo ngày  (biểu đồ cột tự vẽ bằng HTML/CSS)                     │ ← .chart-panel (razor 136–215)
├───────────────────────────────────────────────────────────────────────────┤
│ Báo cáo theo ngày (bảng + dòng Tổng)                                       │ ← .report-panel (razor 217–274)
└───────────────────────────────────────────────────────────────────────────┘
  Hộp thoại "Xuất báo cáo" (Modal) — chỉ hiện khi exportOpen = true         ← razor 276–312
```

Biến trạng thái của trang (`Dashboard.razor.cs:9–17`):

| Biến | Kiểu | Dùng để |
|------|------|---------|
| `filter` | `DashboardFilter` (`From`, `To`: `DateTime?`) | Giữ ngày đang chọn (`Models/DashboardFilter.cs`) |
| `report` | `DashboardReport` (`Rows` + tổng `Orders`, `Revenue`, `Cost`) | Dữ liệu vẽ thẻ, biểu đồ, bảng (`Models/DashboardReport.cs`, `DailyReport.cs`) |
| `range` | `string` | Dòng chữ "Toàn bộ thời gian" / "01/11/2026 – 07/11/2026" |
| `exportOpen` | `bool` | Mở / đóng hộp thoại xuất báo cáo |
| `error` | `string?` | Hộp báo lỗi đỏ (razor 55–60) |

---

## 2.2. Bộ lọc ngày — thiết kế thế nào?

### a) Hai ô "Từ ngày – Đến ngày"
```razor
<!-- Dashboard.razor:10–22 -->
<div class="date-range">
    <input type="date" aria-label="Từ ngày"  @bind="filter.From" />
    <span>–</span>
    <input type="date" aria-label="Đến ngày" @bind="filter.To" />
</div>
```
- `type="date"` là **ô chọn ngày có sẵn của HTML5**. Không dùng thư viện ngoài.
- `@bind="filter.From"` **ràng buộc 2 chiều** ô nhập với biến `DateTime?`:
  - Người dùng chọn ngày → Blazor tự chuyển chuỗi `"2026-11-07"` thành `DateTime` rồi gán vào `filter.From` (khi xảy ra sự kiện `change`).
  - Code đổi `filter.From` → lần vẽ lại sau, ô tự hiện ngày mới.
- `aria-label` giúp trình đọc màn hình đọc được "Từ ngày" / "Đến ngày", vì ô không có nhãn hiển thị.

### b) *"Sao bấm vào ô lại hiện ra lịch chọn ngày / tháng / năm?"*
> "Do em dùng `<input type="date">`. Lịch bật lên là **giao diện có sẵn của trình duyệt** (Chrome, Edge…), em không phải tự vẽ. Định dạng hiển thị (`mm/dd/yyyy` hay `dd/mm/yyyy`) **theo ngôn ngữ của trình duyệt / Windows**: trình duyệt tiếng Anh hiện `mm/dd/yyyy`, tiếng Việt hiện `dd/mm/yyyy`. Giá trị gửi vào code thì luôn là chuẩn `yyyy-MM-dd`, Blazor tự chuyển thành `DateTime`."

Vì lịch là của trình duyệt nên **không đổi được màu lịch bằng CSS**. CSS chỉ chỉnh được khung ô: `.date-range` ở `app.css:428`, `.date-range input` ở `app.css:439`.

### c) Ô chọn nhanh "Tùy chọn / 7 ngày / 30 ngày / Tháng này"
```razor
<!-- Dashboard.razor:24–32 -->
<select aria-label="Khoảng thời gian" @onchange="ChangePeriod">
    <option value="">Tùy chọn</option>
    <option value="7">7 ngày gần nhất</option>
    <option value="30">30 ngày gần nhất</option>
    <option value="month">Tháng này</option>
</select>
```
Chọn một mục → gọi `ChangePeriod` (`Dashboard.razor.cs:54–85`):

| Chọn | `filter.From` | `filter.To` |
|------|---------------|-------------|
| `7` | hôm nay − 6 ngày (`today.AddDays(-6)`) — tính cả hôm nay là đủ 7 ngày | hôm nay |
| `30` | hôm nay − 29 ngày | hôm nay |
| `month` | ngày 1 của tháng này (`new DateTime(năm, tháng, 1)`) | hôm nay |
| `""` (Tùy chọn) | `null` | `null` → hai ô trống = "toàn bộ thời gian" |

**Vì sao chọn xong, 2 ô ngày tự đổi theo?** `ChangePeriod` gán `filter.From/To`. Sau khi hàm xử lý sự kiện chạy xong, Blazor **tự vẽ lại** component. Hai ô ngày đang `@bind` vào đúng 2 biến đó nên hiện ngày mới. Không cần viết code cập nhật ô.

**Vì sao `<select>` dùng `@onchange` mà không dùng `@bind`?** Lựa chọn này không cần lưu lại. Nó chỉ là "nút tắt" để tính `From/To`, nên chỉ cần bắt sự kiện thay đổi.

### d) Nút "Áp dụng"
`Dashboard.razor:34–37` → `@onclick="Apply"` → `Dashboard.razor.cs:36–52`:
- Nếu có `From`, `range` thành `"dd/MM/yyyy – dd/MM/yyyy"`; không có thì `"Toàn bộ thời gian"`.
- Dòng chữ dưới thanh lọc (`<p class="filter-caption">@range</p>`, razor 51–53) tự đổi theo.
- **Chưa** gọi `DashboardService`: xem cách hoàn thiện ở mục 2.7.

---

## 2.3. Ba thẻ thống kê

`Dashboard.razor:62–134`. Mỗi thẻ là `<section class="stat-card">` gồm icon (`<Icon Name="coins" />`, `orders`, `box`), nhãn, số liệu và chú thích nhỏ.

| Thẻ | Giá trị | Công thức |
|-----|---------|-----------|
| Doanh thu đã thanh toán | `Money(report.Revenue)` | `Revenue = Rows.Sum(Revenue)` (`DashboardReport.cs`) |
| Đơn đã thanh toán | `report.Orders + " đơn"` | `Rows.Sum(Orders)` |
| Chi phí nhập kho | `Money(report.Cost)` | `Rows.Sum(Cost)` |

- Chưa có dữ liệu (`report.Rows.Count == 0`) → hiện `—`.
- `Money()` (`Dashboard.razor.cs:21–24`) định dạng kiểu Việt Nam: `29.000 đ`. Muốn ra dấu chấm phân cách thì project cần `BlazorWebAssemblyLoadAllGlobalizationData` trong `.csproj` (thiếu thì Blazor WASM hiện `29,000`).
- CSS: `.stat-grid` (3 cột) `app.css:465`, `.stat-card` 472, `.stat-icon` (vòng tròn icon) 483.

---

## 2.4. Biểu đồ "Doanh thu theo ngày" (tự vẽ, không dùng thư viện)

`Dashboard.razor:136–215` + 4 hàm tính toán ở `Dashboard.razor.cs:119–156`.

| Thành phần | Code | Cách hoạt động |
|------------|------|----------------|
| Giá trị trần trục Y | `ChartMax` (.cs:119) | Doanh thu lớn nhất, làm tròn **lên** bội số của 1 triệu, tối thiểu 1 triệu |
| 7 nhãn trục Y | vòng `for (i = 6 → 0)` (razor 159) + `AxisPosition(i)` (.cs:149) | Nhãn i nằm ở `top: (6−i)/6 × 100%`, giá trị = `ChartMax × i/6`, đổi ra triệu |
| 7 đường kẻ ngang | razor 171–177 `.grid-lines` | CSS chia đều |
| Chiều cao cột | `BarHeight(row.Revenue)` (.cs:131) | `height = Revenue / ChartMax × 150px` |
| Số trên đầu cột | `LabelPosition` (.cs:140) | Đặt cao hơn đầu cột 6px |
| Nhãn ngày dưới cột | `row.Date.ToString("dd/MM")` (razor 200) | |
| Cuộn ngang khi nhiều ngày | `min-width: max(600, số ngày × 48)px` (razor 155) + `.chart-scroll` | Mỗi cột ≥ 48px, trang không bị bóp méo |

> Thầy hỏi *"Biểu đồ dùng thư viện gì?"*: "Không dùng thư viện. Em vẽ bằng thẻ `div`: chiều cao mỗi cột tính trong C# theo tỉ lệ doanh thu so với giá trị trần (`BarHeight`), rồi gán vào `style`. Trục Y chia 6 khoảng đều nhau."

CSS: `.axis-unit` 513, `.chart-*` 520–547, `.plot` 554, `.grid-lines` 561, `.bars` 575, `.bar-column` 584, `.bar` 594.

---

## 2.5. Bảng "Báo cáo theo ngày"

`Dashboard.razor:217–274`:
- `@foreach (var row in report.Rows)` vẽ mỗi ngày một dòng: ngày `dd/MM/yyyy`, số đơn, doanh thu, chi phí.
- Không có dòng nào → một ô gộp 4 cột (`colspan="4"`) ghi "Chưa có dữ liệu báo cáo." (razor 248–255).
- `<tfoot>` là dòng **Tổng**, lấy từ thuộc tính tính sẵn `report.Orders/Revenue/Cost`.
- Bảng nằm trong `.table-wrap`, nên trên điện thoại bảng cuộn ngang được mà trang không bị tràn. CSS `.report-table` ở `app.css:654`.

---

## 2.6. Nút "Xuất báo cáo" & hộp thoại

| Bước | Ở đâu |
|------|-------|
| Nút | `Dashboard.razor:41–47` → `@onclick="() => exportOpen = true"` (lambda gán biến, không cần hàm riêng) |
| Hộp thoại | `Dashboard.razor:276–312` → `<Modal Title="Xuất báo cáo" OnClose="...">`, component `Components/Modal.razor` (tự đóng bằng phím Esc, bấm nền tối, nút ×) |
| Nút "Excel (.csv)" | razor 285 → `ExportExcel` (`.cs:87`): **đang là placeholder** |
| Nút "PDF / In" | razor 290 → `PrintPdf` (`.cs:103`): **đang là placeholder** |
| Phần đã viết sẵn để nối | `Services/ReportExporter.cs`: `ExportExcelAsync` tạo CSV, gọi JS `appHelpers.downloadCsv` (`wwwroot/js/app.js`, có thêm BOM UTF‑8 để Excel hiện đúng tiếng Việt). `PrintPdfAsync` gọi `window.print()` |

---

## 2.7. Hoàn thiện: nối trang với dữ liệu thật (gợi ý — chưa có trong code)

API đã sẵn sàng: `DashboardService.GetReportAsync(filter)` (`Services/DashboardService.cs:18`) gọi `GET api/dashboard/report?from=yyyy-MM-dd&to=yyyy-MM-dd`. API giả trả lời ở `MockApiHandler.cs:691` (`Report`): lấy các đơn **Đã thanh toán**, gom theo ngày, cộng chi phí từ phiếu **Nhập mua** của kho.

```csharp
// Dashboard.razor.cs — hướng hoàn thiện
[Inject] private DashboardService DashboardService { get; set; } = default!;
[Inject] private ReportExporter Exporter { get; set; } = default!;
[Inject] private ILogger<Dashboard> Logger { get; set; } = default!;
private bool loading;

protected override Task OnInitializedAsync() => Apply();   // mở trang là tải luôn

private async Task Apply()
{
    if (filter.From > filter.To) { error = "Từ ngày phải trước Đến ngày."; return; }
    loading = true; error = null;
    try
    {
        report = await DashboardService.GetReportAsync(filter);
        range = filter.From.HasValue ? $"{filter.From:dd/MM/yyyy} – {filter.To:dd/MM/yyyy}" : "Toàn bộ thời gian";
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Không tải được báo cáo.");
        error = "Không tải được báo cáo. Vui lòng thử lại.";   // không hiện ex.Message cho người dùng
    }
    finally { loading = false; }
}

private async Task ExportExcel() { await Exporter.ExportExcelAsync(report, range); exportOpen = false; }
private async Task PrintPdf()    { exportOpen = false; await Exporter.PrintPdfAsync(); }
```
Khi làm, nhớ thêm `disabled="@loading"` cho nút Áp dụng. Code hiện tại còn chỗ gán `error = exception.Message` (`.cs:49, 97, 113`), trái quy tắc của dự án (không hiện lỗi kỹ thuật cho người dùng). Nên sửa luôn khi nối service.

---

## 2.8. Tìm nhanh

| Muốn tìm | Gõ Ctrl+Shift+F |
|----------|-----------------|
| Bộ lọc ngày | `date-range` (ra cả trang Đơn hàng, dùng chung CSS) |
| Chọn nhanh khoảng thời gian | `ChangePeriod` |
| Nút Áp dụng | `Apply` trong `Dashboard.razor.cs` |
| Định dạng tiền | `Money(` |
| Biểu đồ | `BarHeight`, `ChartMax` |
| API báo cáo | `api/dashboard/report` |
| Tiêu đề "Tổng quan" trên header | `"" => "Tổng quan"` ở `Layout/MainLayout.razor:171` |
