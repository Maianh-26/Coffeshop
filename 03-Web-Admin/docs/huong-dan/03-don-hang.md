# 3. Trang Đơn hàng

← [Mục lục](README.md)

> **Cập nhật 24/11/2026:** đã **xóa chế độ API giả** (`Services/Mock/`, `Api:UseMock`). Web luôn gọi backend thật `CoffeeShop.Api` tại `Api:BaseUrl` (`wwwroot/appsettings.json`, mặc định `http://localhost:3000/`), mọi route nằm dưới `api/v1/`. Một số số dòng, tên route cũ trong file này viết cho bản trước, khi cần thì tra lại bằng Ctrl+Shift+F.

- **URL:** `/orders` · **Quyền:** cả **Admin** và **Nhân viên**. Trang không có `Roles`, chỉ cần đăng nhập (`[Authorize]` chung trong `Pages/_Imports.razor`). Đây là trang chính của nhân viên: đăng nhập xong vào thẳng đây (`Utilities/Roles.cs` → `HomeUrl`).
- **Một file duy nhất:** `Pages/Orders/Orders.razor` (1374 dòng):
  - Dòng **1–787**: giao diện.
  - Dòng **789–1374**: khối `@code { }`, chứa toàn bộ logic C#. Trang này **không có** file `.razor.cs` riêng.
- **Model:** `Models/Order.cs`, `Models/OrderItem.cs` (gồm `OrderItemTopping`).

> ⚠️ **Trạng thái hiện tại — cần nói đúng khi bị hỏi:**
> Trang **chưa nối `OrderService`**. Danh sách đơn được tạo sẵn trong trang (`InitSampleOrders`, dòng 893). Mọi thao tác (tiếp nhận, thanh toán, hủy, tạo đơn) chỉ sửa danh sách trong bộ nhớ, **F5 là mất**.
> Phần nối đã viết sẵn: `Services/OrderService.cs` (`AcceptOrderAsync` → `POST api/orders/{code}/accept`, `ConfirmPaymentAsync` → `POST api/orders/{code}/pay`, …), backend xử lý các route `api/v1/don-hang/...`.

**Luồng nghiệp vụ chính:** Tablet tạo đơn → backend lưu → web admin thấy đơn **Chờ tiếp nhận** → **Tiếp nhận** (Đang pha chế) → **Thu tiền** (Đã thanh toán) → **Hoàn thành**.

---

## 3.1. Bố cục trang (2 cột: danh sách – chi tiết)

```
┌──────────────── .master-panel ─────────────────┐┌──── .detail-panel ───┐
│ [▣ Đơn mới #40 từ Tablet… 57.600 đ  Xem  ×]    ││ Đơn hàng #40       × │
│ (.new-order-alert — chỉ khi có đơn mới)        ││ trạng thái, thời gian│
│ [🔍 Tìm…] [Thời gian▾] [Trạng thái▾] [TT▾] [↻] ││ món đã đặt, tổng     │
│ [+ Tạo đơn tại quầy] [Sửa đơn] [Hủy đơn]       ││ số nhận món          │
│ Đã gán số nhận món 49 cho đơn #40.             ││ phục vụ: 3 bước      │
│ (dòng kết quả thao tác + khung vàng cảnh báo   ││ thu tiền / in hóa đơn│
│  kho nếu có — như các trang khác)              ││                      │
│ Bảng đơn hàng (.orders-table)                  ││                      │
│ 20 đơn / trang · phân trang                    ││                      │
└────────────────────────────────────────────────┘└──────────────────────┘
 Hộp thoại Tạo / Sửa đơn · Hộp thoại Hủy đơn (chỉ Quản lý)
```

- Khung chi tiết **chỉ hiện khi đã chọn một đơn**: `@if (SelectedOrderDetail != null)` (dòng 284). Lớp `has-detail` (dòng 19) đổi bố cục thành 2 cột.
- Màn hình hẹp (≤ 1000px): khung chi tiết nằm **dưới** danh sách. Bấm một đơn thì trang tự cuộn xuống: `OnAfterRenderAsync` (dòng 1035) gọi JS `appHelpers.scrollIntoViewOnNarrow` (`wwwroot/js/app.js`).
- CSS bố cục: `.master-detail-layout` `app.css:1478`.

Biến chính (đầu khối `@code`): `orders`, `search`, `fromDate`, `toDate`, `statusFilter`, `paymentFilter`, `selectedId` (mã đơn đang chọn), `page`, `PageSize = 20`, `RefreshSeconds = 3`, `alertMessage`, `isOrderModalOpen`, `editingOrderId` (khác null = đang sửa đơn đó), `isCancelModalOpen`.

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

> Chuỗi trạng thái phải **khớp từng ký tự** (có dấu) giữa trang, `Models/Order.cs` và backend: `Chờ tiếp nhận`, `Đang pha chế`, `Hoàn thành`, `Đã hủy`, `Chưa thanh toán`, `Đã thanh toán`.

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
| **Hoàn thành đơn** | `Đang pha chế` **và** `Đã thanh toán` | 544–553 | `CompleteOrder` (1114) | → `Hoàn thành` |
| **In hóa đơn** | luôn | 555–562 | `PrintReceipt` (1365) | JS `appHelpers.printReceipt` (dựng hóa đơn K80 trong iframe ẩn rồi in) |
| **×** đóng chi tiết | luôn | 305–311 | lambda `selected = null` | ẩn khung chi tiết |

Sau mỗi thao tác, `ShowAlert(...)` hiện một dòng thông báo **ngay dưới nhóm nút** Tạo đơn / Sửa / Hủy (giống các trang quản lý khác), không tự ẩn, thao tác kế tiếp thay nó; thu tiền có nguyên liệu sắp hết thì có thêm khung vàng liệt kê. Bấm Làm mới không báo gì. Màn ≤1000px đang mở khung chi tiết thì dòng kết quả hiện ở đầu khung chi tiết (khung nằm dưới danh sách). Thông báo đơn mới từ Tablet (màu cam, nút Xem / ×) nằm **ngay dưới tiêu đề trang, trên mục lọc**.

> Thầy hỏi *"Nút Tiếp nhận em code ở đâu, gọi tới đâu?"*: "Nút ở `Orders.razor` dòng 449–457, chỉ hiện khi đơn đang `Chờ tiếp nhận`. Bấm thì gọi `AcceptOrder(detail)` ở dòng 1077, đổi `Status` thành `Đang pha chế` và ghi giờ tiếp nhận. Hiện trang đang chạy dữ liệu mẫu. Khi nối backend, hàm này sẽ gọi `OrderService.AcceptOrderAsync(code)` để gửi `POST api/orders/{code}/accept`. Backend trả **409** nếu đơn không còn ở trạng thái chờ."

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

## 3.6. Tự nhận đơn mới từ Tablet

Trang **luôn** tự làm mới; không có nút bật / tắt. Nút "Tự cập nhật" cũ đã bị bỏ ngày 25/11/2026, vì tắt nó là quầy mất báo đơn mới mà không hay biết.

| Thành phần | Ở đâu | Thực tế làm gì |
|------------|-------|----------------|
| Bộ hẹn giờ 3 giây | hằng `RefreshSeconds = 3`; tạo `Timer` ở cuối `OnInitializedAsync` | Mỗi 3 giây gọi `InvokeAsync(AutoRefresh)`. Phải dùng `InvokeAsync` vì timer chạy ngoài luồng giao diện |
| hàm `AutoRefresh` | `@code` | Tải lại danh sách (`LoadOrders(quiet: true)`) và dò đơn mới. **Bỏ qua lượt** khi đang thao tác (đang tải, thu tiền, tạo / sửa / hủy đơn, đang mở hộp thoại) để bảng không nhảy dưới tay; lượt sau sẽ cập nhật |
| Hủy timer khi rời trang | `@implements IDisposable` (dòng 2) + hàm `Dispose()` cuối file | Không để timer chạy ngầm sau khi đã rời trang |
| Đơn mới từ Tablet | App tablet gửi `POST api/v1/don-hang` lên backend | Lượt làm mới kế tiếp (≤ 3 giây) thấy đơn và hiện thông báo đơn mới. Muốn thử không cần tablet: gửi `POST api/v1/don-hang` **không kèm token** bằng Swagger `http://localhost:3000/docs` |

> Thầy hỏi *"Realtime là sao?"*: "Hiện tại là **làm mới định kỳ** (polling) mỗi 3 giây. Bước tiếp theo là backend đẩy tin qua SignalR để quầy nhận đơn dưới 1 giây — kế hoạch ở `docs/ke-hoach-dong-bo-thoi-gian-thuc.md`."

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

> Phân quyền phía máy chủ: nhân viên được xem, thu tiền, đổi trạng thái phục vụ; **hủy đơn chỉ Quản lý** (backend trả 403).

---

## 3.8. Tìm nhanh

| Muốn tìm | Gõ Ctrl+Shift+F |
|----------|-----------------|
| Nút Tiếp nhận | `Tiếp nhận đơn hàng` hoặc `AcceptOrder` |
| Thu tiền | `ConfirmCashPayment` |
| Điều kiện lọc | `FilteredOrders` |
| Màu badge trạng thái | `StatusBadgeColor` |
| Hộp thoại tạo / sửa | `isOrderModalOpen`, `SaveDraftOrder` |
| Timer realtime | `autoRefreshTimer` |
| Dữ liệu mẫu | `InitSampleOrders`, `InitCatalog` |
| API đơn hàng | `api/v1/don-hang` (trong `OrderService.cs`) |
