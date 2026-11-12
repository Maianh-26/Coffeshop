# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Language

**Luôn trả lời câu hỏi và báo cáo công việc bằng tiếng Việt.** (Always respond to questions and report on work in Vietnamese.)

## Overview

CoffeeShop.Admin is a standalone **Blazor WebAssembly** (.NET 8) admin panel for a coffee shop: dashboard/reports, orders, products (menu items, categories, toppings, recipes), ingredient inventory, promotions, and accounts. All UI text, comments, and docs are in **Vietnamese**; keep new UI strings and code comments in Vietnamese to match.

## System context

This repo is one part of a team-built coffee-shop management system. Each part is owned by a different sub-team; only the web admin lives here:

- **Backend** (other team): ASP.NET Core Web API (IIS Express / Kestrel) + SQL Server database. This app's `Services/` call it.
- **Tablet app** (other team): Flutter. Customers pick items and place orders on the tablet.
- **Web admin** (this repo, our team): Blazor WebAssembly. Owner and staff manage the orders placed from tablets, plus menu, inventory, promotions, accounts and reports.

Core flow: tablet creates order (`POST api/v1/don-hang`, no token) → backend stores it as `PENDING` → web admin sees it (auto-refresh every 10 s), takes payment (`POST api/v1/don-hang/{id}/thanh-toan`, backend deducts stock via `sp_ThanhToanVaTruKho`) or the owner cancels it (`POST api/v1/don-hang/{id}/huy` + reason). The backend has **only payment statuses** `PENDING`/`PAID`/`CANCELLED` — no "accept / brewing / done" step yet (requested from the backend team).

We don't write backend code here. The API contract (routes, DTO shapes, auth) is agreed with the backend team: keep `Models/` and `api/...` routes in sync with what they expose, and flag mismatches instead of silently changing them.

Relevant installed skills: `dotnet-blazor:*` (components, forms, data fetching, auth), `dotnet:csharp-expert`, `dotnet-test:*`, `ui-ux-pro-max` and `frontend-design` (UI/UX).

## Commands

```bash
dotnet build                     # build
dotnet run                       # dev server (launchSettings: http://localhost:5101, https://localhost:7220)
dotnet watch                     # hot reload
dotnet publish -c Release        # static output for deployment
```

There is no test project in this repo and no linter configured.

To verify UI changes visually: start `dotnet run` in the background, then use the Playwright MCP tools (plugin `playwright`, uses the installed Chrome) to open `http://localhost:5101`, log in (`quanly` / `QuanLy@123`, against the running backend or the mock), take screenshots (also at phone/tablet widths) and read the browser console — Blazor WASM runtime errors only show there, not in `dotnet build`.

## Architecture

- **Entry point**: `Program.cs` registers one `HttpClient` whose `BaseAddress` is the app's own host, plus a `Scoped` service per domain (`Services/*Service.cs`), `ReportExporter`, and a `Singleton` `AppState`.
- **Services** are thin wrappers over `ApiClient` calling relative `api/v1/...` routes; `HttpClient.BaseAddress` = `Api:BaseUrl` (real backend) or the app's own host (mock). **No backend code lives in this repo.**
- **Backend (real API):** `CoffeeShop.Api` (ASP.NET Core 8 + SQL Server `QuanCaPheDB`), API-first, contract = its `openapi/openapi.yaml` (Swagger at `http://localhost:3000/docs`). All routes are under **`api/v1/`** with Vietnamese names (`auth/dang-nhap`, `auth/toi`, `auth/doi-mat-khau`, `nguoi-dung`, `danh-muc`, `kich-thuoc`, `mon`, `mon/{id}/kich-thuoc`, `mon/{id}/con-hang`, `chi-tiet-mon/{id}/cong-thuc`, `topping`, `khuyen-mai`, `don-hang`, `nguyen-lieu`, `nguyen-lieu/{id}/nhap-kho|xuat-huy`, `lich-su-kho`, `bao-cao/doanh-thu|mon-ban-chay|gio-cao-diem`). Ids are **ints**; JSON fields are camelCase of the DB columns (`maDonHang`, `tongTien`…); some lists are paged `{ data, meta }` (`mon`, `don-hang`, `lich-su-kho`, limit ≤ 100). Errors are `{ "error": { "code", "message", "details" } }` (message is Vietnamese; 422 `HET_NGUYEN_LIEU` has shortages in `details`). **Unknown fields in a request body → 400**, so request DTOs must match exactly. `wwwroot/appsettings.json` → `Api:UseMock: false`, `Api:BaseUrl: http://localhost:3000/` (CORS is open). Backend sample logins: `quanly` / `QuanLy@123` (QuanLy), `nhanvien` / `NhanVien@123` (NhanVien). Local run: create the DB with `database/QuanCaPheDB.sql` **then `database/FixCongThucUnique.sql`** (otherwise seeding fails on `UQ_CongThuc_Topping`), then `dotnet run` (override the connection string with env `ConnectionStrings__QuanCaPheDB` if not `.\SQLEXPRESS`).
- **Models/** mirror the backend DTOs 1-1 (`NguoiDung`, `Mon`, `GiaTheoSize`, `Topping`, `DanhMuc`, `KichThuoc`, `KhuyenMai`, `NguyenLieu`, `LichSuKho`, `DonHang`, `ChiTietDon`, `BaoCaoDoanhThu`…) plus `*Form` classes for EditForm validation. **Services** use `Services/ApiClient` (JSON in/out, throws `ApiException` whose `Message` is always a user-facing Vietnamese sentence — pages may show it; `Code`/`Details` carry `error.code`/`details`). `Services/Query.Build` builds query strings; date filters send `tuNgay` = 00:00 and `denNgay` = 23:59:59.997 (backend compares with `<=`).
- **Mock API (`Services/Mock/`)**: `Api:UseMock: true` makes `Program.cs` give `HttpClient` a `MockApiHandler` that answers the **same `api/v1` contract** from the in-memory `MockDatabase` (same routes, roles, error JSON, status codes, paging, unknown-field → 400, stock check when ordering, stock deduction on payment without blocking — like `sp_ThanhToanVaTruKho`). Mock logins mirror the backend: `quanly` / `QuanLy@123`, `nhanvien` / `NhanVien@123`, `minh.nv` / `NhanVien@123`. Mock data resets on F5. `MockNewTabletOrderEverySeconds` adds a tablet order on `GET api/v1/don-hang`; the Orders page button "Mô phỏng đơn từ Tablet" (mock only) sets `MockDatabase.TabletOrderRequested`. `Demo:AutoLoginAs` auto-logs-in only in mock mode. When the backend contract changes, update `Models/`, the service and `MockApiHandler` together.
- The old API contract Claude Doc (https://claude.ai/artifact/4tvnvHxhbnW6GzaAdQY4L1) describes the earlier `api/...` design and is **outdated** — the backend's `openapi.yaml` is now the source of truth.
- **UI/flow overview (reference only):** `docs/Coffee_Shop_Toan_bo_luong_giao_dien (1).pdf` — 61 pages showing the intended screens, flows and business rules (last page = general interaction rules). The owner's decision: it is for **understanding the overall flow**; the **existing pages/UI stay as they are** — do not rewrite pages to match the PDF unless asked. Things explicitly adopted from it: role split owner/staff, forgot password via email code, account management owner-only, no public registration.
- **All pages load data through the services** (`OnInitializedAsync`, cancel via a page `CancellationTokenSource` disposed in `Dispose`). Backend constraints reflected in the UI: products are priced **per size** (`PUT mon/{id}/kich-thuoc`), image is a URL string ≤ 255 chars (no upload), delete of món/topping/nguyên liệu is soft (ngừng bán / ngừng sử dụng), accounts cannot be deleted (lock with `trangThai=false`), orders cannot be edited, cancel is owner-only with a reason, inventory changes only via nhập kho / xuất hủy, no purchase cost (dashboard has no "chi phí nhập kho").
- **Auth** (`Services/Auth/`): `TokenAuthenticationStateProvider` is the Blazor `AuthenticationStateProvider` — it keeps token/name/role in `AppState` and persists them in `sessionStorage` (`coffeeshop.session`: survives F5, cleared when the tab closes). `AuthenticationService.LoginAsync` calls `api/v1/auth/dang-nhap` (`{ tenDangNhap, matKhau }` → `{ token, hetHan, nguoiDung }`) then `SignInAsync`; `LogoutAsync` signs out. `AuthHeaderHandler` (wraps the mock or real handler in `Program.cs`) adds `Authorization: Bearer <token>` to every request and signs out on 401. `App.razor` uses `AuthorizeRouteView` → `RedirectToLogin` (`/login?returnUrl=…`). `Pages/_Imports.razor` puts `[Authorize]` on **every page**; public pages need `@attribute [AllowAnonymous]` (only `/login` and `/forgot-password`). **Role split (owner's requirement):** `QuanLy` (chủ quán, shown as "Quản lý") sees everything; `NhanVien` sees only **Đơn hàng** (`/orders`) and **Tài khoản** (`/my-account` — own info + change password). Dashboard (`/`), Products, Inventory, Promotions and Accounts are `[Authorize(Roles = Roles.Admin)]`; the sidebar in `MainLayout` renders a different menu per role; staff opening `/` are redirected to `/orders`, other admin pages show "Không có quyền truy cập"; after login staff land on `/orders` (a `returnUrl` to an admin page is ignored for staff — `Login.TargetAfterLogin`). Clicking the user name in the header opens `/my-account` for both roles. When adding a page, decide its role and update both the attribute and the menu. Roles are the strings `QuanLy` / `NhanVien` from the backend (`vaiTro`) — use `Utilities/Roles` (`Roles.Admin`, `Roles.Label`, `Roles.HomeUrl`) instead of string literals. The backend (and the mock) enforce roles server-side: staff may read the menu, list/view/pay orders, toggle món/topping availability and read recipes; cancel order, accounts, inventory, reports and menu edits are QuanLy only (403). Own-account APIs: `GET api/v1/auth/toi`, `POST api/v1/auth/doi-mat-khau { matKhauCu, matKhauMoi }` (wrong old password → 400 `BAD_REQUEST`, mapped to `ResetErrorCodes.WrongPassword`). **Forgot password is not in the backend yet**: in real mode the login page shows "liên hệ chủ quán" and `/forgot-password` shows a notice; the old flow (`api/auth/forgot-password`…, ProblemDetails) still works only in mock mode.
- **Forgot password** (`/forgot-password`, `[AllowAnonymous]`, linked from the login page; there is **no public registration** — only the owner creates accounts): email → `POST api/auth/forgot-password` (always 200, `resendAfterSeconds`) → 6-digit code → `POST api/auth/verify-reset-code` → `{ resetToken }` → `POST api/auth/reset-password { resetToken, newPassword }` (204). Errors come back as ProblemDetails `title` (Vietnamese) and are shown as-is. **Sending the code by Gmail is the backend's job** (SMTP with an App Password — never put mail credentials in this WASM app). The mock returns an extra `devCode` so the page can show the code in a "Chế độ giả lập" box; the real backend must never return it. Mock rules: code valid 10 min, 5 wrong attempts, resend after 60 s and max 5 code requests/hour **per email, applied identically whether or not the email exists** (no account enumeration), reset token single-use 15 min. Errors carry a machine-readable `errorCode` (`invalid_code`, `code_expired`, `too_many_attempts`, `token_expired`, `resend_too_soon`) and 429 carries a `Retry-After` header; the page uses both. `devCode` is only displayed when `Api:UseMock` is true.
- **`Components/InputTextLive`**: an `InputText` that updates on every keystroke (`oninput`). Use it for short forms submitted with Enter or browser autofill (login, forgot password) — plain `InputText` only updates on `change`, so submitting while the cursor is still in the field validates an empty model.
- **Testing note:** with simple sample passwords, Chrome may show its native "password found in a data breach" dialog after login; it silently blocks Playwright clicks/keys (the app is fine — dispatch clicks via `browser_evaluate` or dismiss the dialog).
- **Routing/layout**: `App.razor` defaults to `Layout/MainLayout.razor` (sidebar nav + header). `/login` uses `AuthLayout`. The header title comes from a URL→title `switch` in `MainLayout` — when adding a page, add its nav link **and** its title mapping there.
- **Pages** live in `Pages/<Feature>/`. Bigger pages split logic into a `.razor.cs` partial class using `[Inject]` properties; others keep everything in an `@code` block (Orders.razor is >1000 lines).
- **Shared components** (`Components/`): `Icon` (inline SVG looked up by `Name` from an internal path dictionary — add new icons there), `Modal` (dialog with backdrop/Escape close), `Brand`.
- **Editing pattern**: forms edit a separate `*Form` object and send a request DTO on save; `Utilities/ModelCopy` (JSON deep copy) is still available.
- **Export**: `ReportExporter` builds CSV in C# and calls JS helpers in `wwwroot/js/app.js` (`window.appHelpers.downloadCsv` adds a UTF-8 BOM so Excel shows Vietnamese correctly; `printPage` for PDF via the print dialog). Formatting uses `vi-VN` culture.
- **Styling**: all styles are in one global stylesheet, `wwwroot/css/app.css` (no scoped `.razor.css` is loaded — `index.html` doesn't link the scoped bundle). It starts with a **design-token** `:root` block (`--coffee-50…800`, `--ink`/`--muted`, `--line`, status pairs like `--success`/`--success-bg`, `--r-sm/md/lg/pill`, `--shadow-*`, `--focus-ring`, `--fs-*`, `--ease`); use these tokens, never raw hex. The "TINH CHỈNH GIAO DIỆN" block at the end holds interaction states (hover/focus/disabled), badge/table/pagination polish and mobile/reduced-motion rules. Font: Be Vietnam Pro via Google Fonts (falls back to Segoe UI); the logo SVG keeps Segoe UI on purpose.
- **Cache busting**: `index.html` links `css/app.css?v=N` and `js/app.js?v=N` — bump `N` whenever that file changes, otherwise browsers keep the old copy (a stale `app.js` makes new JS-interop calls throw and crash the render with "Có lỗi xảy ra"). Wrap non-essential JS-interop calls in `try/catch (JSException)`.
- **Culture**: the csproj sets `BlazorWebAssemblyLoadAllGlobalizationData` so `vi-VN` formatting works (`29.000 đ`); without it WASM silently falls back to `29,000`.

## Coding rules

Follow these when writing code (existing code still violates some — fix them when touching that code):

- Never show `ex.Message` / `exception.Message` to users; show a fixed Vietnamese message and log details via `ILogger`.
- Components that subscribe to events (`NavigationManager.LocationChanged`, `AppState.OnChange`) or create `Timer`/`CancellationTokenSource` must implement `IDisposable`/`IAsyncDisposable` and unsubscribe/dispose.
- Load data in `OnInitializedAsync`; update UI from timers/background threads via `InvokeAsync(StateHasChanged)`; disable submit buttons while saving.
- Status/role strings come from the backend and live in ONE place each: `TrangThaiDon` (`PENDING`/`PAID`/`CANCELLED` + Vietnamese `Label`/`Color`), `PhuongThucThanhToans` (`CASH`/`QR_TRANSFER`), `LoaiGiaoDichKho`, `TrangThaiMon` (display status computed from `ngungBan`/`conHang`/`coTheBan`), `Roles` — never write these literals in pages.
- Put styles in `wwwroot/css/app.css` using the design tokens — not new inline `style="..."`, and not a `.razor.css` (the scoped-CSS bundle isn't linked in `index.html`, so it would have no effect). Bump `app.css?v=N` in `index.html` after editing.
- Any change to an `api/...` route or DTO shape: update `MockApiHandler` to match and flag that the API contract doc / backend team needs updating.

## Quy trình bắt buộc khi sửa / nâng cấp chức năng (yêu cầu của chủ dự án)

Áp dụng cho **mọi phiên** phát triển dự án này, với mọi thay đổi code hoặc giao diện (trừ sửa chữ / lỗi chính tả rất nhỏ):

1. **Trước khi sửa: gọi skill liên quan** (Skill tool) để hướng sửa đúng quy trình, rồi mới viết code. Chọn theo việc:
   - Component / trang Blazor: `dotnet-blazor:author-component`; form, ô nhập, validation: `dotnet-blazor:collect-user-input`; gọi API / tải dữ liệu: `dotnet-blazor:fetch-and-send-data`; đăng nhập / phân quyền: `dotnet-blazor:configure-auth`; JS interop: `dotnet-blazor:use-js-interop`; kế hoạch đổi giao diện lớn: `dotnet-blazor:plan-ui-change`.
   - Giao diện / trải nghiệm: `ui-ux-pro-max:ui-ux-pro-max` (hoặc `frontend-design:frontend-design`).
   - Code C# thuần: `dotnet:csharp-expert`; test: `dotnet-test:*`; tài liệu Word: `anthropic-skills:docx`.
   Nói ngắn cho người dùng biết đã gọi skill nào.
2. **Sau khi làm xong: gọi agent kiểm tra** — `code-reviewer`, và thêm `ui-ux-reviewer` nếu thay đổi chạm tới giao diện hoặc luồng người dùng (chạy song song được). **Sửa mọi lỗi được xác nhận**, build lại, rồi báo kết quả (đã sửa gì, còn gì chưa sửa và vì sao).
3. Nếu phiên được mở ở thư mục gốc repo (`C:\dev\Coffeshop`), các agent trong `03-Web-Admin/.claude/agents/` không được nạp: dùng agent `general-purpose` và bảo nó đọc rồi làm đúng theo file `.claude/agents/code-reviewer.md` / `ui-ux-reviewer.md`.
4. Muốn test giao diện với dữ liệu giả thì tạm đặt `Api:UseMock: true`, **xong phải trả về `false`**. Không chạy `ui-ux-reviewer` khi người dùng đang tự chạy app ở cổng 5101 nếu chưa hỏi.

## Review workflow

Project subagents live in `.claude/agents/` (read-only reviewers; they report, the main agent fixes):

- **`code-reviewer`** — project-specific code review (API contract sync, Blazor patterns, status strings, conventions).
- **`ui-ux-reviewer`** — runs the app with Playwright, screenshots at 3 widths, reads the console, checks accessibility and the order flow.

Standing rule (superseded/strengthened by "Quy trình bắt buộc" above): after **every** feature change or fix, run `code-reviewer`; if it touched UI or a user flow, also run `ui-ux-reviewer`. Then fix confirmed issues and report. Only trivial text/typo edits may skip this. Don't run `ui-ux-reviewer` while the user is debugging on port 5101 unless they agree.

## Repo notes

- **Location:** this app lives in the team monorepo https://github.com/Maianh-26/Coffeshop (public) under `03-Web-Admin/`, next to `01-Backend-Blazor/` and `02-Flutter-Tablet/`. Local clone: `C:\dev\Coffeshop` — open `C:\dev\Coffeshop\03-Web-Admin\CoffeeShop.Admin.sln` in Visual Studio and run Claude Code from `C:\dev\Coffeshop\03-Web-Admin`. The old copy in OneDrive (`...\projectTT\CoffeeShop.Admin`) is an archive only — never edit it.
- **Git workflow:** never push straight to `main`; work on a branch (`web-admin` or `feature/...`), push, open a Pull Request for the team lead. Only touch files under `03-Web-Admin/` — other folders belong to the other teams. Run `git pull` before starting work. Commit/push only when the user asks.
- The repo is public: never commit secrets, real credentials or private screenshots.
- `.playwright-mcp/` holds Playwright screenshots/logs — scratch output, don't commit it.

- `server.out`/`_check-server.*` are stale logs.
