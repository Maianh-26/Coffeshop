# 3. Trang Đơn hàng

← [Mục lục](README.md)

- **URL:** `/orders` · **Quyền:** cả **Admin** và **Nhân viên**. Trang không có `Roles`, chỉ cần đăng nhập (`[Authorize]` chung trong `Pages/_Imports.razor`). Đây là trang chính của nhân viên: đăng nhập xong vào thẳng đây (`Utilities/Roles.cs` → `HomeUrl`).
- **Một file duy nhất:** `Pages/Orders/Orders.razor` (1374 dòng):
  - Dòng **1–787**: giao diện.
  - Dòng **789–1374**: khối `@code { }`, chứa toàn bộ logic C#. Trang này **không có** file `.razor.cs` riêng.
- **Model:** `Models/Order.cs`, `Models/OrderItem.cs` (gồm `OrderItemTopping`).

> ⚠️ **Trạng thái hiện tại — cần nói đúng khi bị hỏi:**
> Trang **chưa nối `OrderService`**. Danh sách đơn được tạo sẵn trong trang (`InitSampleOrders`, dòng 893). Mọi thao tác (tiếp nhận, thanh toán, hủy, tạo đơn) chỉ sửa danh sách trong bộ nhớ, **F5 là mất**.
> Phần nối đã viết sẵn: `Services/OrderService.cs` (`AcceptOrderAsync` → `POST api/orders/{code}/accept`, `ConfirmPaymentAsync` → `POST api/orders/{code}/pay`, …) và API giả xử lý ở `MockApiHandler.cs:568` (`Orders`).

**Luồng nghiệp vụ chính:** Tablet tạo đơn → backend lưu → web admin thấy đơn **Chờ tiếp nhận** → **Tiếp nhận** (Đang pha chế) → **Thu tiền** (Đã thanh toán) → **Hoàn thành**.

---

## 3.1. Bố cục trang (2 cột: danh sách – chi tiết)

```
┌──────────────── .master-panel ─────────────────┐┌──── .detail-panel ───┐
│ [🔍 Tìm…] [Từ–Đến] [Trạng thái▾] [TT▾] [Lọc][Đặt lại] ││ Đơn hàng #DH00125  ×│ ← dòng 288–577
│ (.filters-panel, dòng 24–84)                  ││ badge trạng thái    │
│ [+Tạo đơn][Sửa][Hủy]   ● Tablet Realtime [Mô phỏng]││ thông tin, món      │
│ (.action-bar, dòng 88–147)                    ││ tạm tính / tổng     │
│ Bảng đơn hàng (.orders-table, dòng 151–243)   ││ [Tiếp nhận]          │
│ 12 đơn hàng · Trang 1/2        [‹] 1 [›]      ││ [Thu tiền / QR]      │
│ (.list-footer, dòng 245–281)                  ││ [Hoàn thành][In]     │
└───────────────────────────────────────────────┘└──────────────────────┘
 Hộp thoại Tạo/Sửa đơn (dòng 583–766) · Hộp thoại Xác nhận hủy (dòng 774–786)
```

- Khung chi tiết **chỉ hiện khi đã chọn một đơn**: `@if (SelectedOrderDetail != null)` (dòng 284). Lớp `has-detail` (dòng 19) đổi bố cục thành 2 cột.
- Màn hình hẹp (≤ 1000px): khung chi tiết nằm **dưới** danh sách. Bấm một đơn thì trang tự cuộn xuống: `OnAfterRenderAsync` (dòng 1035) gọi JS `appHelpers.scrollIntoViewOnNarrow` (`wwwroot/js/app.js`).
- CSS bố cục: `.master-detail-layout` `app.css:1478`.

Biến chính (`@code`, dòng 790–828): `orders`, `search`, `fromDate`, `toDate`, `statusFilter`, `paymentFilter`, `selected` (mã đơn đang chọn), `page`, `PageSize = 10`, `alertMessage`, `autoRefreshEnabled`, `isOrderModalOpen`, `isEditing`, `isDeleteModalOpen`, `draftOrder`.

---

## 3.2. Bộ lọc & tìm kiếm

| Ô | Code (razor) | Biến | Khi nào cập nhật |
|---|--------------|------|------------------|
| Tìm mã đơn, bàn, món… | dòng 32–35 `@bind="search" @bind:event="oninput"` | `search` | **Mỗi lần gõ phím** (nhờ `oninput`) |
| Từ ngày – Đến ngày | dòng 39–51, `<input type="date">` | `fromDate`, `toDate` | Khi chọn ngày. Lịch là của trình duyệt (giải thích ở [02-tong-quan.md mục 2.2b](02-tong-quan.md)) |
| Trạng thái đơn | dòng 53–59 `<select @bind="statusFilter">` | `statusFilter` | Khi đổi lựa chọn |
| Trạng thái thanh toán | dòng 61–65 | `paymentFilter` | Khi đổi lựa chọn |
| Nút **Lọc** | dòng 67 → `Filter()` (dòng 1052) | `page = 1` | |
| Nút **Đặt lại** | dòng 75 → `Reset()` (dòng 1057) | xóa hết điều kiện, về trang 1 | |

**Cơ chế lọc:** thuộc tính tính toán `FilteredOrders` (dòng 1002–1013) dùng LINQ `Where`:
```csharp
(search == "" || mã đơn / thu ngân / bàn / tên món chứa search, không phân biệt hoa thường)
&& (fromDate rỗng || ngày tạo >= fromDate) && (toDate rỗng || ngày tạo <= toDate)
&& (statusFilter == "" || Status == statusFilter)
&& (paymentFilter == "" || PaymentStatus == paymentFilter)
→ sắp xếp đơn mới nhất lên đầu (OrderByDescending CreatedAt)
```
> Thầy hỏi *"Nút Lọc làm gì?"*: "Danh sách **lọc ngay** khi đổi điều kiện, vì `FilteredOrders` là thuộc tính được tính lại mỗi lần Blazor vẽ trang. Nút Lọc chỉ đưa về **trang 1**, để không bị đứng ở trang 3 khi kết quả lọc chỉ còn 1 trang."

**Phân trang** (dòng 1015–1022, 1067): `VisibleOrders = FilteredOrders.Skip((page-1)*10).Take(10)`. `Pages = ceil(số đơn / 10)`, nhỏ nhất là 1. Nút ‹ › gọi `Move(-1)` / `Move(1)`, không cho vượt quá trang đầu / cuối.

> Chuỗi trạng thái phải **khớp từng ký tự** (có dấu) giữa trang, `Models/Order.cs` và `MockApiHandler`: `Chờ tiếp nhận`, `Đang pha chế`, `Hoàn thành`, `Đã hủy`, `Chưa thanh toán`, `Đã thanh toán`.

---

## 3.3. Bảng đơn hàng & chọn đơn

- Dòng 174–237: `@foreach (var order in VisibleOrders)` vẽ mỗi đơn một `<tr>`.
- Bấm một dòng → `SelectOrder(order.Code)` (dòng 1027) → `selected = code` → khung chi tiết hiện ra. Dòng đang chọn có lớp `selected` để tô nền.
- Cột **Nguồn**: badge tím nếu `Tablet`, xám nếu `Tại quầy`. Cột **Thanh toán**: xanh nếu đã thanh toán, vàng nếu chưa. Cột **Trạng thái**: màu theo `StatusBadgeColor` (dòng 840: Chờ tiếp nhận → cam, Đang pha chế → xanh dương, Hoàn thành → xanh lá, Đã hủy → đỏ).
- CSS: `.orders-table` `app.css:2570`, `.badge` 765, `.selectable` (hover / chọn) 737, `.pagination` 808.

---

## 3.4. Khung chi tiết & các nút nghiệp vụ

Khung chi tiết dòng 288–577 dùng biến `detail = SelectedOrderDetail` (dòng 837: `orders.FirstOrDefault(o => o.Code == selected)`).

**Tiền trong đơn** (tính trong model, không tính ở trang):

| Dòng hiển thị | Thuộc tính | Công thức (`Models/Order.cs`) |
|---------------|-----------|-------------------------------|
| Tạm tính | `SubTotal` (dòng 34) | Σ giá gốc × số lượng |
| Phụ thu size / topping | `ToppingTotal` (dòng 39) | Σ (phụ thu size + Σ topping) × số lượng |
| Giảm giá | `DiscountAmount` (dòng 44) | nhập tay khi tạo / sửa đơn |
| **Tổng tiền** | `Total` (dòng 49) | Σ `TotalPrice` của từng món − giảm giá, không âm |

Đơn giá một món: `OrderItem.UnitPrice = BasePrice + SizeExtraPrice + Σ Toppings.Price` (`OrderItem.cs:46–56`).

**Nút nào hiện lúc nào** (điều kiện `@if` trong `.razor`):

| Nút | Hiện khi | Dòng razor | Hàm | Kết quả |
|-----|----------|------------|-----|---------|
| **Tiếp nhận đơn hàng** | `Status == "Chờ tiếp nhận"` | 449–457 | `AcceptOrder` (1077) | → `Đang pha chế`, ghi `AcceptedAt` |
| **Xác nhận đã thu tiền mặt** | chưa thanh toán, chưa hủy, `PaymentMethod == "Tiền mặt"` | 460–476 | `ConfirmCashPayment` (1084) | → `Đã thanh toán`; nếu đang chờ thì tự chuyển `Đang pha chế` |
| **Mã QR + "Mô phỏng khách quét QR thành công"** | chưa thanh toán, chưa hủy, chuyển khoản QR | 478–541 | `SimulateQrSuccess` (1099) | như trên. Mã QR là hình SVG minh họa (dòng 486), chưa phải VietQR thật |
| **Hoàn thành đơn** | `Đang pha chế` **và** `Đã thanh toán` | 544–553 | `CompleteOrder` (1114) | → `Hoàn thành` |
| **In hóa đơn** | luôn | 555–562 | `PrintReceipt` (1365) | JS `appHelpers.printPage` → `window.print()` |
| **×** đóng chi tiết | luôn | 305–311 | lambda `selected = null` | ẩn khung chi tiết |

Sau mỗi thao tác, `ShowAlert(...)` (dòng 1359) hiện banner xanh ở đầu trang (dòng 8–17). Có nút × để tắt banner.

> Thầy hỏi *"Nút Tiếp nhận em code ở đâu, gọi tới đâu?"*: "Nút ở `Orders.razor` dòng 449–457, chỉ hiện khi đơn đang `Chờ tiếp nhận`. Bấm thì gọi `AcceptOrder(detail)` ở dòng 1077, đổi `Status` thành `Đang pha chế` và ghi giờ tiếp nhận. Hiện trang đang chạy dữ liệu mẫu. Khi nối backend, hàm này sẽ gọi `OrderService.AcceptOrderAsync(code)` để gửi `POST api/orders/{code}/accept`. API (giả ở `MockApiHandler.cs:602`) trả **409** nếu đơn không còn ở trạng thái chờ."

---

## 3.5. Tạo đơn tại quầy / Sửa đơn / Hủy đơn

### Tạo đơn — nút "+ Tạo đơn tại quầy" (dòng 92) → `OpenCreateOrder` (dòng 1175)
1. Tạo `draftOrder` mới: mã `DH…`, `Chờ tiếp nhận`, `Chưa thanh toán`, nguồn `Tại quầy`.
2. `isOrderModalOpen = true` → hiện `<Modal>` (dòng 583).
3. **Thông tin đơn** (dòng 590–626): bàn / vị trí, hình thức thanh toán, thu ngân, giảm giá, ghi chú. Mỗi ô `@bind` thẳng vào `draftOrder.*`.
4. **Bộ chọn món** (dòng 630–684): chọn món, size, số lượng, ghi chú, tick topping (`ToggleToppingInBuilder`, dòng 1258, lưu vào `HashSet selectedToppingCodes`). Bấm **"Thêm món này vào danh sách"** (dòng 681) → `AddItemToDraftOrder` (dòng 1267): ghép thành `OrderItem` rồi đưa vào `draftOrder.Items`.
5. Bảng món đã thêm (dòng 688–750). Nút thùng rác bỏ một món: `draftOrder.Items.Remove(item)`.
6. **Lưu** (dòng 761) → `SaveDraftOrder` (dòng 1302): chưa có món thì báo lỗi **ngay trong hộp thoại** (`orderModalError`, dòng 754–757). Có món thì chèn vào đầu danh sách, chọn đơn mới và hiện banner.

Danh mục món / size / topping cho bộ chọn được tạo sẵn ở `InitCatalog` (dòng 864). Sau này nên lấy từ `ProductService` / `ToppingService`.

### Sửa đơn — nút "Sửa đơn" (dòng 100–106) → `OpenEditOrder` (dòng 1196)
- Nút bị khóa khi chưa chọn đơn: `disabled="@(selected == null)"`.
- **Sao chép** đơn đang chọn sang `draftOrder` (copy từng món, từng topping), rồi sửa trên bản sao. Bấm Hủy thì đơn gốc không bị đổi. Bấm Lưu thì `SaveDraftOrder` mới chép ngược lại (nhánh `if (isEditing)`, dòng 1313).

### Hủy đơn — nút "Hủy đơn" (dòng 109–115) → `OpenDeleteOrder` (dòng 1343)
- Mở hộp thoại xác nhận (dòng 774–786). Hộp thoại ghi rõ mã đơn, bàn, tổng tiền.
- "Xác nhận hủy đơn" → `ConfirmCancelOrder` (dòng 1349): **không xóa đơn**, chỉ đổi `Status = "Đã hủy"` để còn lưu lịch sử.

---

## 3.6. "Tablet Realtime" & Mô phỏng đơn từ Tablet

| Thành phần | Ở đâu | Thực tế làm gì |
|------------|-------|----------------|
| Bộ hẹn giờ 4 giây | `OnInitialized` (dòng 849–862), `System.Threading.Timer` | Mỗi 4 giây gọi `InvokeAsync(StateHasChanged)` để vẽ lại trang. Phải dùng `InvokeAsync` vì timer chạy ngoài luồng giao diện |
| Nút "Tablet Realtime: Đang bật / Tạm dừng" | dòng 123–131 → `ToggleAutoRefresh` (dòng 1124) | Bật / tắt cờ `autoRefreshEnabled`. Chấm xanh nhấp nháy là CSS `.live-indicator` |
| Nút "Mô phỏng gửi đơn từ Tablet" | dòng 134–144 → `SimulateTabletOrder` (dòng 1130) | Thêm 1 đơn `Tablet` (bàn ngẫu nhiên) vào đầu danh sách. **Chỉ hiện khi `Api:UseMock = true`** (dòng 134) |
| Hủy timer khi rời trang | `@implements IDisposable` (dòng 2) + `Dispose()` (dòng 1370) | Không để timer chạy ngầm sau khi đã rời trang |
| API giả tự sinh đơn Tablet | `MockApiHandler.cs:643` `SimulateTabletOrders` | Mỗi `MockNewTabletOrderEverySeconds` giây (`appsettings.json`) thêm đơn khi có `GET api/orders`. Có tác dụng khi trang đã nối `OrderService` |

> Thầy hỏi *"Realtime là sao?"*: "Hiện tại là **làm mới định kỳ** (polling) mỗi 4 giây. Khi nối backend, mỗi nhịp sẽ gọi `OrderService.GetAllAsync()` để lấy đơn mới từ tablet. Nếu backend có SignalR thì có thể đổi sang đẩy dữ liệu thời gian thực."

CSS: `.realtime-pill` `app.css:1819`, `.qr-payment-box` 1849.

---

## 3.7. Hướng nối service (gợi ý — chưa có trong code)

| Thao tác trên trang | Gọi | API |
|---------------------|-----|-----|
| Mở trang / mỗi nhịp timer | `OrderService.GetAllAsync()` | `GET api/orders` |
| Tiếp nhận | `AcceptOrderAsync(code)` | `POST api/orders/{code}/accept` |
| Thu tiền / QR | `ConfirmPaymentAsync(code, paymentMethod)` | `POST api/orders/{code}/pay` |
| Tạo đơn | `CreateAsync(order)` | `POST api/orders` |
| Sửa đơn / Hoàn thành / Hủy | `UpdateAsync(order)` | `PUT api/orders/{code}` |

Khi nối: thêm `@inject OrderService Orders` và `ILogger`. Đổi các hàm sang `async Task` và bắt lỗi, hiện câu tiếng Việt cố định. Khóa nút trong lúc gửi (`disabled`). Tải dữ liệu trong `OnInitializedAsync`.

> Phân quyền phía máy chủ: nhân viên được gọi **mọi** API `orders` (`MockApiHandler.cs:448`).

---

## 3.8. Tìm nhanh

| Muốn tìm | Gõ Ctrl+Shift+F |
|----------|-----------------|
| Nút Tiếp nhận | `Tiếp nhận đơn hàng` hoặc `AcceptOrder` |
| Thu tiền | `ConfirmCashPayment`, `SimulateQrSuccess` |
| Điều kiện lọc | `FilteredOrders` |
| Màu badge trạng thái | `StatusBadgeColor` |
| Hộp thoại tạo / sửa | `isOrderModalOpen`, `SaveDraftOrder` |
| Timer realtime | `autoRefreshTimer` |
| Dữ liệu mẫu | `InitSampleOrders`, `InitCatalog` |
| API đơn hàng | `api/orders` (ra `OrderService.cs` và `MockApiHandler.cs`) |
