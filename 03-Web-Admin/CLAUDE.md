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

Core flow: tablet creates order → backend stores it → web admin sees it, accepts it, and takes payment (`api/orders/{code}/accept`, `api/orders/{code}/pay`).

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

To verify UI changes visually: start `dotnet run` in the background, then use the Playwright MCP tools (plugin `playwright`, uses the installed Chrome) to open `http://localhost:5101`, log in with a mock account, take screenshots (also at phone/tablet widths) and read the browser console — Blazor WASM runtime errors only show there, not in `dotnet build`.

## Architecture

- **Entry point**: `Program.cs` registers one `HttpClient` whose `BaseAddress` is the app's own host, plus a `Scoped` service per domain (`Services/*Service.cs`), `ReportExporter`, and a `Singleton` `AppState`.
- **Services** are thin REST wrappers over `HttpClient` calling relative `api/...` routes (e.g. `api/products/{code}`, `api/orders/{code}/pay`, `api/dashboard/report?from=&to=`). Entities are identified by a string `Code`, not numeric IDs. **No backend lives in this repo** — the API is expected to be served at the same origin.
- **Mock API (`Services/Mock/`)**: while the backend isn't ready, `wwwroot/appsettings.json` → `Api:UseMock: true` makes `Program.cs` give `HttpClient` a `MockApiHandler`, which answers every `api/...` request from the in-memory `MockDatabase` (JSON round-trip, real status codes: 201/204/404/409, ProblemDetails). Services and pages don't know they're mocked. Set `UseMock: false` and `Api:BaseUrl` to hit the real backend. Mock logins (all password `123456`, see `MockDatabase.DefaultPassword`): `admin` (Admin, admin@coffeeshop.local); staff (Nhân viên) `nhanvien` (thuha@coffeeshop.local), `minh.nv` (minh.nv@coffeeshop.local), `lan.nv` (lan.nv@coffeeshop.local). Accounts the owner creates in mock mode also get `123456`. Mock data resets on F5. `MockNewTabletOrderEverySeconds` injects a fake tablet order on `GET api/orders`. When the API contract changes, update `MockApiHandler` to match.
- The API contract shared with the backend team is a Claude Doc: https://claude.ai/artifact/4tvnvHxhbnW6GzaAdQY4L1
- **UI/flow overview (reference only):** `docs/Coffee_Shop_Toan_bo_luong_giao_dien (1).pdf` — 61 pages showing the intended screens, flows and business rules (last page = general interaction rules). The owner's decision: it is for **understanding the overall flow**; the **existing pages/UI stay as they are** — do not rewrite pages to match the PDF unless asked. Things explicitly adopted from it: role split owner/staff, forgot password via email code, account management owner-only, no public registration.
- **Pages still use their own hardcoded sample data, not the services.** `Orders.razor`, `Products.razor.cs`, `Dashboard.razor.cs`, etc. seed in-memory lists in `OnInitialized(Async)`. Only `Login` actually calls a service (`AuthenticationService` → `api/auth/login`). Wiring pages to services is the expected next step; check which path a page uses before changing data logic.
- **Auth** (`Services/Auth/`): `TokenAuthenticationStateProvider` is the Blazor `AuthenticationStateProvider` — it keeps token/name/role in `AppState` and persists them in `sessionStorage` (`coffeeshop.session`: survives F5, cleared when the tab closes). `AuthenticationService.LoginAsync` calls `api/auth/login` then `SignInAsync`; `LogoutAsync` signs out. `AuthHeaderHandler` (wraps the mock or real handler in `Program.cs`) adds `Authorization: Bearer <token>` to every request and signs out on 401. `App.razor` uses `AuthorizeRouteView` → `RedirectToLogin` (`/login?returnUrl=…`). `Pages/_Imports.razor` puts `[Authorize]` on **every page**; public pages need `@attribute [AllowAnonymous]` (only `/login` and `/forgot-password`). **Role split (owner's requirement):** `Admin` (chủ quán, shown as "Quản lý") sees everything; `Nhân viên` sees only **Đơn hàng** (`/orders`) and **Tài khoản** (`/my-account` — own info + change password). Dashboard (`/`), Products, Inventory, Promotions and Accounts are `[Authorize(Roles = "Admin")]`; the sidebar in `MainLayout` renders a different menu per role; staff opening `/` are redirected to `/orders`, other admin pages show "Không có quyền truy cập"; after login staff land on `/orders` (a `returnUrl` to an admin page is ignored for staff — `Login.TargetAfterLogin`). Clicking the user name in the header opens `/my-account` for both roles. When adding a page, decide its role and update both the attribute and the menu. Roles are the strings `Admin` / `Nhân viên` from the login response — use `Utilities/Roles` (`Roles.Admin`, `Roles.Label`, `Roles.HomeUrl`) instead of string literals. The mock enforces the same split server-side (`MockApiHandler.StaffMayCall`): staff may use `orders`, `auth/*`, and only **GET** `products`/`categories`/`toppings`/`promotions`; everything else → 403. Own-account APIs: `GET api/auth/me`, `POST api/auth/change-password { currentPassword, newPassword }` (400 + `errorCode: wrong_password`). The mock API rejects requests without a valid `mock-token-{username}` with 401, like the real backend will.
- **Forgot password** (`/forgot-password`, `[AllowAnonymous]`, linked from the login page; there is **no public registration** — only the owner creates accounts): email → `POST api/auth/forgot-password` (always 200, `resendAfterSeconds`) → 6-digit code → `POST api/auth/verify-reset-code` → `{ resetToken }` → `POST api/auth/reset-password { resetToken, newPassword }` (204). Errors come back as ProblemDetails `title` (Vietnamese) and are shown as-is. **Sending the code by Gmail is the backend's job** (SMTP with an App Password — never put mail credentials in this WASM app). The mock returns an extra `devCode` so the page can show the code in a "Chế độ giả lập" box; the real backend must never return it. Mock rules: code valid 10 min, 5 wrong attempts, resend after 60 s and max 5 code requests/hour **per email, applied identically whether or not the email exists** (no account enumeration), reset token single-use 15 min. Errors carry a machine-readable `errorCode` (`invalid_code`, `code_expired`, `too_many_attempts`, `token_expired`, `resend_too_soon`) and 429 carries a `Retry-After` header; the page uses both. `devCode` is only displayed when `Api:UseMock` is true.
- **`Components/InputTextLive`**: an `InputText` that updates on every keystroke (`oninput`). Use it for short forms submitted with Enter or browser autofill (login, forgot password) — plain `InputText` only updates on `change`, so submitting while the cursor is still in the field validates an empty model.
- **Testing note:** with the mock password `123456`, Chrome may show its native "password found in a data breach" dialog after login; it silently blocks Playwright clicks/keys (the app is fine — dispatch clicks via `browser_evaluate` or dismiss the dialog).
- **Routing/layout**: `App.razor` defaults to `Layout/MainLayout.razor` (sidebar nav + header). `/login` uses `AuthLayout`. The header title comes from a URL→title `switch` in `MainLayout` — when adding a page, add its nav link **and** its title mapping there.
- **Pages** live in `Pages/<Feature>/`. Bigger pages split logic into a `.razor.cs` partial class using `[Inject]` properties; others keep everything in an `@code` block (Orders.razor is >1000 lines).
- **Shared components** (`Components/`): `Icon` (inline SVG looked up by `Name` from an internal path dictionary — add new icons there), `Modal` (dialog with backdrop/Escape close), `Brand`.
- **Editing pattern**: `Utilities/ModelCopy` (`Clone`/`CopyTo`, JSON-based deep copy) is used to edit a copy of a model and commit it back on save.
- **Export**: `ReportExporter` builds CSV in C# and calls JS helpers in `wwwroot/js/app.js` (`window.appHelpers.downloadCsv` adds a UTF-8 BOM so Excel shows Vietnamese correctly; `printPage` for PDF via the print dialog). Formatting uses `vi-VN` culture.
- **Styling**: all styles are in one global stylesheet, `wwwroot/css/app.css` (no scoped `.razor.css` is loaded — `index.html` doesn't link the scoped bundle). It starts with a **design-token** `:root` block (`--coffee-50…800`, `--ink`/`--muted`, `--line`, status pairs like `--success`/`--success-bg`, `--r-sm/md/lg/pill`, `--shadow-*`, `--focus-ring`, `--fs-*`, `--ease`); use these tokens, never raw hex. The "TINH CHỈNH GIAO DIỆN" block at the end holds interaction states (hover/focus/disabled), badge/table/pagination polish and mobile/reduced-motion rules. Font: Be Vietnam Pro via Google Fonts (falls back to Segoe UI); the logo SVG keeps Segoe UI on purpose.
- **Cache busting**: `index.html` links `css/app.css?v=N` and `js/app.js?v=N` — bump `N` whenever that file changes, otherwise browsers keep the old copy (a stale `app.js` makes new JS-interop calls throw and crash the render with "Có lỗi xảy ra"). Wrap non-essential JS-interop calls in `try/catch (JSException)`.
- **Culture**: the csproj sets `BlazorWebAssemblyLoadAllGlobalizationData` so `vi-VN` formatting works (`29.000 đ`); without it WASM silently falls back to `29,000`.

## Coding rules

Follow these when writing code (existing code still violates some — fix them when touching that code):

- Never show `ex.Message` / `exception.Message` to users; show a fixed Vietnamese message and log details via `ILogger`.
- Components that subscribe to events (`NavigationManager.LocationChanged`, `AppState.OnChange`) or create `Timer`/`CancellationTokenSource` must implement `IDisposable`/`IAsyncDisposable` and unsubscribe/dispose.
- Load data in `OnInitializedAsync`; update UI from timers/background threads via `InvokeAsync(StateHasChanged)`; disable submit buttons while saving.
- Status strings must match exactly (Vietnamese diacritics) across pages, `Models/` and `Services/Mock/` — e.g. `Chờ tiếp nhận`, `Đang pha chế`, `Hoàn thành`, `Đã hủy`, `Chưa thanh toán`, `Đã thanh toán`.
- Put styles in `wwwroot/css/app.css` using the design tokens — not new inline `style="..."`, and not a `.razor.css` (the scoped-CSS bundle isn't linked in `index.html`, so it would have no effect). Bump `app.css?v=N` in `index.html` after editing.
- Any change to an `api/...` route or DTO shape: update `MockApiHandler` to match and flag that the API contract doc / backend team needs updating.

## Review workflow

Project subagents live in `.claude/agents/` (read-only reviewers; they report, the main agent fixes):

- **`code-reviewer`** — project-specific code review (API contract sync, Blazor patterns, status strings, conventions).
- **`ui-ux-reviewer`** — runs the app with Playwright, screenshots at 3 widths, reads the console, checks accessibility and the order flow.

The user has asked for this standing rule: after completing a **significant** change (wiring a page to services, a new feature, edits across several files), run `code-reviewer`; if the change touched UI or a user flow, also run `ui-ux-reviewer`. Then fix confirmed issues and report. For small edits, don't run them — just offer in one line. Don't run `ui-ux-reviewer` while the user is debugging on port 5101 unless they agree.

## Repo notes

- **Location:** this app lives in the team monorepo https://github.com/Maianh-26/Coffeshop (public) under `03-Web-Admin/`, next to `01-Backend-Blazor/` and `02-Flutter-Tablet/`. Local clone: `C:\dev\Coffeshop` — open `C:\dev\Coffeshop\03-Web-Admin\CoffeeShop.Admin.sln` in Visual Studio and run Claude Code from `C:\dev\Coffeshop\03-Web-Admin`. The old copy in OneDrive (`...\projectTT\CoffeeShop.Admin`) is an archive only — never edit it.
- **Git workflow:** never push straight to `main`; work on a branch (`web-admin` or `feature/...`), push, open a Pull Request for the team lead. Only touch files under `03-Web-Admin/` — other folders belong to the other teams. Run `git pull` before starting work. Commit/push only when the user asks.
- The repo is public: never commit secrets, real credentials or private screenshots.
- `.playwright-mcp/` holds Playwright screenshots/logs — scratch output, don't commit it.

- `server.out`/`_check-server.*` are stale logs.
