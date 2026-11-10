# Hướng dẫn đọc code — Web Admin Coffee Shop

Bộ tài liệu này giúp trả lời nhanh câu hỏi kiểu **"Nút này em code ở đâu, gọi tới đâu, tham chiếu đến đâu?"** cho từng màn hình của web admin (Blazor WebAssembly, .NET 8).

> Số dòng (ví dụ `Login.razor.cs:48`) đúng tại thời điểm viết (07/11/2026). Nếu code bị sửa, số dòng có thể lệch vài dòng — khi đó **tìm theo tên hàm** (ví dụ `HandleLogin`), tên hàm thì không đổi.

## Mục lục

| # | Tài liệu | Màn hình | Trạng thái |
|---|----------|----------|------------|
| 1 | [01-dang-nhap.md](01-dang-nhap.md) | Đăng nhập, Quên mật khẩu (3 bước), Đăng xuất, phân quyền | ✅ |
| 2 | [02-tong-quan.md](02-tong-quan.md) | Tổng quan (bộ lọc ngày, thống kê, biểu đồ, xuất báo cáo) | ✅ |
| 3 | [03-don-hang.md](03-don-hang.md) | Đơn hàng (lọc, chi tiết, tiếp nhận, thanh toán, tạo/sửa/hủy, Tablet) | ✅ |
| 4 | [04-mon.md](04-mon.md) | Món (Món / Danh mục / Topping & Size, ảnh, công thức) | ✅ |
| 5–11 | *(sẽ viết tiếp)* | Kho nguyên liệu, Khuyến mãi, Tài khoản, Tài khoản của tôi, Khung trang (sidebar/header), Mock API, CSS & giao diện | ⏳ |

---

## Kiến trúc trong 30 giây

```
Trình duyệt (Blazor WASM)
 ├─ Pages/<Tính năng>/<Trang>.razor        ← giao diện (HTML + @onclick, @bind)
 │   └─ <Trang>.razor.cs  hoặc  @code {}   ← logic C# của trang (hàm xử lý nút bấm)
 ├─ Models/*.cs                            ← dữ liệu + quy tắc kiểm tra ([Required], [Range]…)
 ├─ Services/*Service.cs                   ← gọi API "api/..." qua HttpClient
 │   └─ Services/Auth/AuthHeaderHandler.cs ← gắn token vào MỌI request
 │       └─ Services/Mock/MockApiHandler.cs← (đang bật UseMock) API giả trả lời thay backend
 ├─ Components/*.razor                     ← mảnh giao diện dùng chung (Icon, Modal, Brand…)
 ├─ Layout/MainLayout.razor, AuthLayout    ← khung trang: sidebar + header / trang đăng nhập
 └─ wwwroot/css/app.css, js/app.js         ← toàn bộ CSS, vài hàm JavaScript nhỏ
```

- **`Program.cs`** đăng ký mọi service (dòng 56–66) và chọn API giả hay thật (dòng 19–46, đọc `wwwroot/appsettings.json` → `Api:UseMock`).
- **Bấm nút** trong `.razor` → gọi **hàm C#** (cùng tên trong `@code` hoặc `.razor.cs`) → (nếu có) gọi **Service** → **HttpClient** → `AuthHeaderHandler` → **MockApiHandler** (hoặc backend thật).

> ⚠️ Hiện tại **chỉ luồng Đăng nhập / Quên mật khẩu / Tài khoản của tôi** thật sự gọi Service → API.
> Các trang Tổng quan, Đơn hàng, Món… **đang dùng dữ liệu mẫu viết sẵn trong trang** (chưa nối Service). Mỗi tài liệu đều ghi rõ điểm này để trả lời đúng.

---

## Cách tìm code nhanh nhất (áp dụng cho mọi nút / mọi chữ trên màn hình)

### Cách 1 — Tìm theo chữ hiện trên màn hình (nhanh nhất)
1. Nhìn chữ trên nút / tiêu đề, ví dụ **"Áp dụng"**.
2. Visual Studio: **Ctrl + Shift + F** (Find in Files) → gõ `Áp dụng` → phạm vi *Entire solution*.
   (VS Code: **Ctrl + Shift + F**; dòng lệnh: `git grep -n "Áp dụng"`.)
3. Kết quả trỏ tới file `.razor`, ví dụ `Dashboard.razor:34`. Nhìn thuộc tính `@onclick="Apply"` → biết hàm xử lý tên là `Apply`.
4. Đặt con trỏ vào chữ `Apply` → **F12** (Go To Definition) → nhảy sang `Dashboard.razor.cs:36`.
5. Trong hàm, gặp tên service (ví dụ `AuthService.LoginAsync`) → **F12** tiếp để đi sâu vào service.

### Cách 2 — Tìm theo đường dẫn (URL)
URL `/orders` → tìm `@page "/orders"` → ra `Pages/Orders/Orders.razor`. Bảng tra nhanh:

| URL | File giao diện | File logic | Quyền |
|-----|----------------|-----------|-------|
| `/login` | `Pages/Authentication/Login.razor` | `Login.razor.cs` | Ai cũng vào |
| `/forgot-password` | `Pages/Authentication/ForgotPassword.razor` | `ForgotPassword.razor.cs` | Ai cũng vào |
| `/` | `Pages/Dashboard/Dashboard.razor` | `Dashboard.razor.cs` | Admin |
| `/orders` | `Pages/Orders/Orders.razor` | khối `@code` trong cùng file (từ dòng 789) | Admin + Nhân viên |
| `/products` | `Pages/Products/Products.razor` | `Products.razor.cs` | Admin |

### Cách 3 — Tìm theo giao diện (CSS)
Chuột phải vào phần tử trên trình duyệt → **Inspect** (F12) → xem `class`, ví dụ `class="stat-card"` → tìm `.stat-card` trong `wwwroot/css/app.css`. Toàn bộ CSS nằm trong **một file** này (không có file `.razor.css`).

### Cách 4 — Ai đang dùng hàm / class này?
Con trỏ vào tên → **Shift + F12** (Find All References) → liệt kê mọi nơi gọi tới.

### Mẹo đọc file `.razor`
| Cú pháp | Ý nghĩa |
|---------|---------|
| `@onclick="TenHam"` | Bấm → chạy hàm C# `TenHam` |
| `@bind="bien"` | Ô nhập ↔ biến C# (2 chiều; cập nhật khi rời ô / đổi lựa chọn) |
| `@bind:event="oninput"` | Cập nhật biến **ngay khi gõ** |
| `@if (...) { ... }` | Chỉ vẽ phần này khi điều kiện đúng |
| `@foreach (...)` | Lặp để vẽ danh sách (dòng bảng, option…) |
| `<EditForm OnValidSubmit="X">` | Form có kiểm tra dữ liệu; hợp lệ mới gọi `X` |
| `[Inject]` / `@inject` | Lấy service đã đăng ký trong `Program.cs` |
| `@attribute [Authorize(Roles = "Admin")]` | Chỉ tài khoản Admin vào được trang |
