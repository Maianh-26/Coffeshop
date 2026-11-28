# 1. Đăng nhập · Đăng xuất · Phân quyền

> **Cập nhật 18/11/2026:** đã **bỏ** luồng Quên mật khẩu (trang `/forgot-password`, mã xác minh qua email) và trang **Tài khoản của tôi** (`/my-account`). Quán chỉ dùng vài tài khoản: ai quên mật khẩu thì chủ quán đặt lại ở trang **Tài khoản** (Sửa → Đặt lại mật khẩu) hoặc lấy lại trực tiếp trong CSDL. Tên + vai trò ở header chỉ để hiển thị, bấm vào không mở trang nào. Form tài khoản không còn ô Email.

← [Mục lục](README.md)

> **Cập nhật 24/11/2026:** đã **xóa chế độ API giả** (`Services/Mock/`, `Api:UseMock`). Web luôn gọi backend thật `CoffeeShop.Api` tại `Api:BaseUrl` (`wwwroot/appsettings.json`, mặc định `http://localhost:3000/`), mọi route nằm dưới `api/v1/`. Một số số dòng, tên route cũ trong file này viết cho bản trước, khi cần thì tra lại bằng Ctrl+Shift+F.

Luồng: giao diện → service → HttpClient → `AuthHeaderHandler` → backend thật (`POST api/v1/auth/dang-nhap`).

Tài khoản mẫu do backend tạo sẵn trong CSDL `QuanCaPheDB`: `quanly` / `QuanLy@123` (Quản lý) · `nhanvien` / `NhanVien@123` (Nhân viên).

---

## 1.1. Bản đồ file

| Vai trò | File | Ghi chú |
|---------|------|---------|
| Giao diện đăng nhập | `Pages/Authentication/Login.razor` | Form, ô nhập, nút |
| Logic đăng nhập | `Pages/Authentication/Login.razor.cs` | `HandleLogin`, chuyển trang sau đăng nhập |
| Khung trang (logo bên trái) | `Layout/AuthLayout.razor` + `Components/Brand.razor` | Logo `wwwroot/images/logo.png` |
| Dữ liệu form | `Models/AuthenticationModels.cs` | `LoginRequest` (+ thông báo lỗi), `LoginResponse` |
| Ô nhập cập nhật khi gõ | `Components/InputTextLive.razor` | Thay cho `InputText` thường |
| Gọi API | `Services/AuthenticationService.cs` | `LoginAsync`, `LogoutAsync` (tài khoản đang đăng nhập: `AccountService.GetMeAsync`) |
| Lưu phiên đăng nhập | `Services/Auth/TokenAuthenticationStateProvider.cs` | Lưu vào `sessionStorage` |
| Gắn token vào request | `Services/Auth/AuthHeaderHandler.cs` | Header `Authorization: Bearer …` |
| Trạng thái dùng chung | `Services/AppState.cs` | Token, họ tên, vai trò |
| API (backend) | `CoffeeShop.Api` → `POST api/v1/auth/dang-nhap` | Nhóm backend làm, không nằm trong repo web |
| Chặn trang khi chưa đăng nhập | `App.razor`, `Pages/_Imports.razor`, `Components/RedirectToLogin.razor` | |
| Vai trò | `Utilities/Roles.cs` | `QuanLy` / `NhanVien` (nhãn "Quản lý" / "Nhân viên"), `AfterLogin` chọn trang sau đăng nhập |
| CSS | `wwwroot/css/app.css` | `.auth-shell` 992, `.auth-card` 1033, `.password-field` 1058, `.auth-submit` 1077 |

---

## 1.2. Luồng bấm nút "Đăng nhập" (từ đầu đến cuối)

```
[Login.razor:66] <button type="submit">Đăng nhập
      │  (nút submit nằm trong EditForm)
      ▼
[Login.razor:18] <EditForm Model="model" OnValidSubmit="HandleLogin">
      │  DataAnnotationsValidator kiểm tra Models/AuthenticationModels.cs
      │  (thiếu tên / mật khẩu → hiện chữ đỏ dưới ô, KHÔNG gọi HandleLogin)
      ▼
[Login.razor.cs:48] HandleLogin()
      │  loading = true → nút đổi thành "Đang đăng nhập…" và bị khóa
      ▼
[AuthenticationService.cs:30] LoginAsync(model)
      │  POST "api/v1/auth/dang-nhap"  { tenDangNhap, matKhau }
      ▼
[AuthHeaderHandler.cs:22] gắn token (lúc này chưa có) → chuyển tiếp
      ▼
[Backend CoffeeShop.Api] kiểm tra tài khoản + mật khẩu trong CSDL
      │  đúng → 200 { token, hetHan, nguoiDung { hoTen, vaiTro, … } }
      │  sai  → 401 { error: { code, message: "Sai tên đăng nhập hoặc mật khẩu…" } }
      ▼
[AuthenticationService.cs:64] thành công → _authState.SignInAsync(token, tên, vai trò)
      ▼
[TokenAuthenticationStateProvider.cs:61] SignInAsync
      │  ① AppState.Login(...)          → lưu token trong bộ nhớ
      │  ② sessionStorage "coffeeshop.session" → F5 không mất đăng nhập
      │  ③ NotifyAuthenticationStateChanged → toàn bộ giao diện biết đã đăng nhập
      ▼
[Login.razor.cs:61] Navigation.NavigateTo(TargetAfterLogin(...))
         Admin → "/" (Tổng quan) · Nhân viên → "/orders"
```

### Trả lời khi thầy hỏi: *"Nút Đăng nhập em code ở đâu?"*
> "Nút nằm ở `Pages/Authentication/Login.razor` dòng 66, là nút `type="submit"` trong `EditForm`. Khi bấm, `EditForm` kiểm tra dữ liệu theo các thuộc tính `[Required]` trong `Models/AuthenticationModels.cs`. Hợp lệ thì gọi hàm `HandleLogin` ở `Login.razor.cs` dòng 48. Hàm này gọi `AuthenticationService.LoginAsync`, service gửi `POST api/auth/login`. Thành công thì `TokenAuthenticationStateProvider.SignInAsync` lưu token vào `sessionStorage` và báo cho giao diện, rồi chuyển trang theo vai trò."

### Các chi tiết hay bị hỏi

| Câu hỏi | Trả lời ngắn | Ở đâu |
|---------|--------------|-------|
| Bỏ trống ô thì sao? | `[Required]` báo "Vui lòng nhập tên đăng nhập." / "…mật khẩu." ngay dưới ô (`ValidationMessage`) | `AuthenticationModels.cs:5–12`, `Login.razor:33, 60` |
| Sai mật khẩu thì sao? | Hiện hộp đỏ, xóa ô mật khẩu để gõ lại | `Login.razor.cs:63–69`, `Login.razor:11–16` |
| Mất mạng / máy chủ lỗi? | Service bắt lỗi, trả câu tiếng Việt cố định (không lộ lỗi kỹ thuật) | `AuthenticationService.cs:74–89` |
| Bấm 2 lần liên tiếp? | `if (loading) return;` + nút bị `disabled` | `Login.razor.cs:50`, `Login.razor:68` |
| Tại sao gõ xong bấm Enter vẫn nhận? | Dùng `InputTextLive` (cập nhật theo sự kiện `oninput`) thay vì `InputText` (chỉ cập nhật khi rời ô) | `Components/InputTextLive.razor` |
| Nút con mắt ẩn/hiện mật khẩu? | Đổi `type` giữa `password` và `text` theo biến `showPassword` | `Login.razor:43–56`, biến ở `Login.razor.cs:30` |
| Sao chỉ có 1 con mắt? | Edge tự chèn thêm nút mắt riêng, đã ẩn bằng CSS `input::-ms-reveal { display: none; }` | `app.css:2433` |
| Đã đăng nhập mà mở lại `/login`? | Tự chuyển vào trang chính | `Login.razor.cs:34–46` |
| `returnUrl` là gì? | Đang ở trang X thì hết phiên → về `/login?returnUrl=X`, đăng nhập xong quay lại X. Có kiểm tra chống chuyển hướng ra trang ngoài | `RedirectToLogin.razor`, `Login.razor.cs:89–133` |
| Mật khẩu có lưu ở trình duyệt không? | Không. Chỉ lưu **token, họ tên, vai trò** trong `sessionStorage` (đóng tab là mất) | `TokenAuthenticationStateProvider.cs:69–70` |

---

## 1.3. Phân quyền: chưa đăng nhập / nhân viên mở trang của chủ quán

```
Pages/_Imports.razor:3        @attribute [Authorize]          → MỌI trang trong Pages/ đều cần đăng nhập
Login.razor:3                 [AllowAnonymous]                → trang công khai duy nhất
Dashboard.razor:2, Products.razor:2    [Authorize(Roles="Admin")] → chỉ chủ quán
```

- `App.razor:7` dùng `AuthorizeRouteView`:
  - Chưa đăng nhập → `<RedirectToLogin />` (dòng 17).
  - Nhân viên mở `/` → chuyển sang `/orders` (dòng 19–23).
  - Nhân viên mở trang Admin khác → hiện "Không có quyền truy cập" (dòng 26–32).
- Menu khác nhau theo vai trò: `Layout/MainLayout.razor`, khối `@if (auth.User.IsInRole("Admin"))`.
- **Máy chủ cũng chặn** (không chỉ giao diện): backend trả 403 nếu nhân viên gọi API chỉ dành cho Quản lý (hủy đơn, tài khoản, kho, báo cáo, sửa thực đơn).

> Thầy hỏi *"Ẩn menu thì nhân viên gõ URL vẫn vào được à?"*: "Không ạ. Trang có `[Authorize(Roles = "Admin")]` nên `AuthorizeRouteView` chặn. API phía máy chủ cũng kiểm tra vai trò và trả 403."

---

## 1.4. Token đi theo mọi request & tự đăng xuất khi hết hạn

`Program.cs:40–45` bọc HttpClient bằng `AuthHeaderHandler`. Nhờ vậy **mọi** service đều tự gắn token, không cần viết lại ở từng service.

`AuthHeaderHandler.cs`:
- Dòng 28–31: có token thì thêm `Authorization: Bearer <token>`.
- Dòng 37–43: máy chủ trả **401** (token hết hạn) thì gọi `SignOutAsync(byUser: false)` để về trang đăng nhập, có kèm `returnUrl`.

---

## 1.5. Đăng xuất

| Bước | Ở đâu |
|------|-------|
| Nút "Đăng xuất" trên header | `Layout/MainLayout.razor:125` → `@onclick="Logout"` |
| Hàm `Logout()` | `MainLayout.razor:207` → `AuthService.LogoutAsync()` |
| Xóa phiên | `TokenAuthenticationStateProvider.cs:85` `SignOutAsync`: xóa `AppState`, xóa `sessionStorage`, báo giao diện |
| Về trang đăng nhập | Tự động: `AuthorizeRouteView` thấy chưa đăng nhập → `RedirectToLogin`. Vì người dùng tự bấm nên **không** nhớ `returnUrl` (`RedirectToLogin.razor:14`) |

---

## 1.6. Khôi phục đăng nhập khi F5

`Program.cs:75` gọi `InitializeAsync()` **trước** khi vẽ giao diện. Hàm này đọc `sessionStorage` (`TokenAuthenticationStateProvider.cs:102`), nên sau F5 không bị chớp về trang đăng nhập.

---

## 1.7. Giao diện (CSS) của trang đăng nhập

| Phần | Class | Dòng trong `app.css` |
|------|-------|----------------------|
| Bố cục 2 cột (logo / form) | `.auth-shell` | 992 (+ bản sửa lỗi 2422) |
| Cột logo nền kem | `.auth-brand` | 999 |
| Logo to + chữ "Quản lý quán cà phê" | `.brand.large`, `.brand-caption` | 1007, 1020 |
| Thẻ trắng chứa form | `.auth-card` | 1033 |
| Ô mật khẩu + nút mắt | `.password-field` | 1058 (màu nâu của nút mắt ở ~2449) |
| Nút Đăng nhập rộng hết thẻ | `.auth-submit` | 1077 |
| Ẩn nút mắt của Edge / nền khi tự điền | `input::-ms-reveal`, `input:-webkit-autofill` | 2433, 2440 |
| Điện thoại: logo thành dải ngang phía trên | `@media (max-width: 650px)` | tìm `.auth-shell` trong khối media |

---

## 1.8. Tìm nhanh

| Muốn tìm | Gõ Ctrl+Shift+F |
|----------|-----------------|
| Chữ trên nút | `Đăng nhập`, `Đăng xuất` |
| Hàm xử lý | `HandleLogin`, `Logout` |
| API | `api/v1/auth/dang-nhap` |
| Câu báo lỗi | `Sai tên đăng nhập hoặc mật khẩu` (do backend trả về, service hiện nguyên câu) |
