# 1. Đăng nhập · Quên mật khẩu · Đăng xuất · Phân quyền

← [Mục lục](README.md)

Đây là luồng **duy nhất đã nối hoàn chỉnh**: giao diện → service → HttpClient → API (đang là API giả `MockApiHandler`). Khi backend thật sẵn sàng, chỉ cần đổi `wwwroot/appsettings.json` → `"UseMock": false`; code trang không phải sửa.

Tài khoản thử (mật khẩu đều là `123456`, khai báo ở `Services/Mock/MockDatabase.cs:23–31`):
`admin` (Quản lý) · `nhanvien`, `minh.nv`, `lan.nv` (Nhân viên).

---

## 1.1. Bản đồ file

| Vai trò | File | Ghi chú |
|---------|------|---------|
| Giao diện đăng nhập | `Pages/Authentication/Login.razor` | Form, ô nhập, nút |
| Logic đăng nhập | `Pages/Authentication/Login.razor.cs` | `HandleLogin`, chuyển trang sau đăng nhập |
| Khung trang (logo bên trái) | `Layout/AuthLayout.razor` + `Components/Brand.razor` | Logo `wwwroot/images/logo.png` |
| Dữ liệu form | `Models/AuthenticationModels.cs` | `LoginRequest` (+ thông báo lỗi), `LoginResponse` |
| Ô nhập cập nhật khi gõ | `Components/InputTextLive.razor` | Thay cho `InputText` thường |
| Gọi API | `Services/AuthenticationService.cs` | `LoginAsync`, `RequestResetCodeAsync`, … |
| Lưu phiên đăng nhập | `Services/Auth/TokenAuthenticationStateProvider.cs` | Lưu vào `sessionStorage` |
| Gắn token vào request | `Services/Auth/AuthHeaderHandler.cs` | Header `Authorization: Bearer …` |
| Trạng thái dùng chung | `Services/AppState.cs` | Token, họ tên, vai trò |
| API giả | `Services/Mock/MockApiHandler.cs` | `Login` (dòng 149), quên mật khẩu (dòng 192–325) |
| Chặn trang khi chưa đăng nhập | `App.razor`, `Pages/_Imports.razor`, `Components/RedirectToLogin.razor` | |
| Vai trò | `Utilities/Roles.cs` | `"Admin"`, `"Nhân viên"` |
| Quên mật khẩu | `Pages/Authentication/ForgotPassword.razor` + `.razor.cs` | 3 bước |
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
      │  POST "api/auth/login"  { username, password }
      ▼
[AuthHeaderHandler.cs:22] gắn token (lúc này chưa có) → chuyển tiếp
      ▼
[MockApiHandler.cs:122] route "auth/login" → [MockApiHandler.cs:149] Login()
      │  so khớp tài khoản + mật khẩu trong MockDatabase
      │  đúng → { success: true, token: "mock-token-admin", fullName, role }
      │  sai  → { success: false, message: "Sai tên đăng nhập hoặc mật khẩu." }
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
Login.razor:3, ForgotPassword.razor:3  [AllowAnonymous]       → 2 trang công khai
Dashboard.razor:2, Products.razor:2    [Authorize(Roles="Admin")] → chỉ chủ quán
```

- `App.razor:7` dùng `AuthorizeRouteView`:
  - Chưa đăng nhập → `<RedirectToLogin />` (dòng 17).
  - Nhân viên mở `/` → chuyển sang `/orders` (dòng 19–23).
  - Nhân viên mở trang Admin khác → hiện "Không có quyền truy cập" (dòng 26–32).
- Menu khác nhau theo vai trò: `Layout/MainLayout.razor`, khối `@if (auth.User.IsInRole("Admin"))`.
- **Máy chủ cũng chặn** (không chỉ giao diện): `MockApiHandler.cs:69` trả 403 nếu nhân viên gọi API không được phép (`StaffMayCall`, dòng 440).

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

## 1.7. Quên mật khẩu (3 bước)

Link "Quên mật khẩu?" ở `Login.razor:75` mở `/forgot-password`. Trang dùng biến `step` (enum `Step { Email, Code, NewPassword, Done }`, `ForgotPassword.razor.cs:15`), và `switch (step)` trong `.razor` quyết định vẽ bước nào.

```
Bước 1 — Email                     Bước 2 — Mã 6 số                    Bước 3 — Mật khẩu mới
ForgotPassword.razor:16            ForgotPassword.razor:51             ForgotPassword.razor:126
  nút "Gửi mã xác minh" (43)         nút "Xác minh" (100)                nút "Lưu mật khẩu mới" (174)
        │ OnValidSubmit="SendCode"         │ OnValidSubmit="VerifyCode"        │ OnValidSubmit="SavePassword"
        ▼                                  ▼                                   ▼
  .razor.cs:90 SendCode()            .razor.cs:169 VerifyCode()          .razor.cs:212 SavePassword()
        ▼                                  ▼                                   ▼
  AuthenticationService.cs:106       AuthenticationService.cs:117        AuthenticationService.cs:129
  POST api/auth/forgot-password      POST api/auth/verify-reset-code     POST api/auth/reset-password
        ▼                                  ▼                                   ▼
  MockApiHandler.cs:192              MockApiHandler.cs:261               MockApiHandler.cs:301
  (sinh mã, chờ 60 giây mới cho gửi lại)   (đúng → trả resetToken)      (đổi mật khẩu, token chỉ dùng 1 lần)
                                                                               ▼
                                                                   Step.Done (182) → "Quay lại đăng nhập"
```

| Chi tiết | Ở đâu |
|----------|-------|
| Kiểm tra email đúng định dạng | `[EmailAddress]` ở `Models/PasswordResetModels.cs:15–16` |
| Mã phải đúng 6 số | `[RegularExpression(@"^\d{6}$")]` ở `PasswordResetModels.cs:42–43` |
| Dán "677 137" vẫn nhận | `NormalizeCode()` lọc chỉ giữ chữ số, `.razor.cs:163` |
| Mật khẩu mới ≥ 6 ký tự, nhập lại phải khớp | `[StringLength(MinimumLength = 6)]`, `[Compare]` ở `PasswordResetModels.cs:60–65` |
| Đếm ngược "Gửi lại sau N giây" | `StartCountdown`/`RunCountdownAsync`, `.razor.cs:248–276`; cập nhật giao diện bằng `InvokeAsync(StateHasChanged)` |
| Nút "Đổi email", "Gửi lại mã" | `ChangeEmail` (`.razor.cs:149`), `ResendCode` (`.razor.cs:143`) |
| Hộp "Chế độ giả lập" hiện mã | Chỉ hiện khi `Api:UseMock = true` (`.razor.cs:46, 131`). Backend thật gửi mã qua Gmail, không trả mã về |
| Nhập sai nhiều lần / mã hết hạn | Máy chủ trả `errorCode` → khóa ô nhập (`codeLocked`), `.razor.cs:190–195` |
| Rời trang thì dừng đếm ngược | `Dispose()`, `.razor.cs:286` (class có `: IDisposable`) |
| Quy tắc phía máy chủ (giả) | Mã sống 10 phút, gửi lại sau 60 giây, tối đa 5 lần/giờ, token 15 phút: `MockApiHandler.cs:181–185` |

> Thầy hỏi *"Gửi mail ở đâu?"*: "Web admin **không** gửi mail. Việc gửi mã qua Gmail là của backend (team backend), vì mật khẩu Gmail không được nằm trong code chạy trên trình duyệt. Web admin chỉ gọi API `forgot-password`."

---

## 1.8. Giao diện (CSS) của 2 trang này

| Phần | Class | Dòng trong `app.css` |
|------|-------|----------------------|
| Bố cục 2 cột (logo / form) | `.auth-shell` | 992 (+ bản sửa lỗi 2422) |
| Cột logo nền kem | `.auth-brand` | 999 |
| Logo to + chữ "Quản lý quán cà phê" | `.brand.large`, `.brand-caption` | 1007, 1020 |
| Thẻ trắng chứa form | `.auth-card` | 1033 |
| Ô mật khẩu + nút mắt | `.password-field` | 1058 (màu nâu của nút mắt ở ~2449) |
| Nút Đăng nhập rộng hết thẻ | `.auth-submit` | 1077 |
| Ẩn nút mắt của Edge / nền khi tự điền | `input::-ms-reveal`, `input:-webkit-autofill` | 2433, 2440 |
| "Bước 1/3", chú thích, nút quay lại | `.auth-step`, `.auth-intro`, `.auth-back` | 2634, 2641, 2684 |
| Điện thoại: logo thành dải ngang phía trên | `@media (max-width: 650px)` | tìm `.auth-shell` trong khối media |

---

## 1.9. Tìm nhanh

| Muốn tìm | Gõ Ctrl+Shift+F |
|----------|-----------------|
| Chữ trên nút | `Đăng nhập`, `Gửi mã xác minh`, `Quên mật khẩu?` |
| Hàm xử lý | `HandleLogin`, `SendCode`, `VerifyCode`, `SavePassword` |
| API | `api/auth/login`, `api/auth/forgot-password` |
| Câu báo lỗi | `Sai tên đăng nhập hoặc mật khẩu` (ra cả mock lẫn service) |
