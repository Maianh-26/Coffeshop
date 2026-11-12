# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Ngôn ngữ

**Mọi thông tin và câu trả lời trả về đều phải trả lời bằng tiếng Việt.**

## Repo layout & git workflow

This folder is the backend part of the team monorepo `https://github.com/Maianh-26/Coffeshop.git` (`01-Backend-API/`, `02-Flutter-Tablet/`, `03-Web-Admin/`). The local clone uses **sparse-checkout (cone)** so only `01-Backend-API/` (plus root files) exists on disk — other folders are hidden, not deleted, and commits never touch them.

- Never move backend files out of `01-Backend-API/` or recreate them at the repo root; commits must use paths `01-Backend-API/...`. A local `pre-commit` hook blocks commits on `main` and commits when the backend sits at the root; a `pre-push` hook blocks pushing to `main`.
- Work on branch `API-HIHI`; integrate via Pull Request `API-HIHI` → `main`.
- Daily: `git pull origin main` → code/test → `git add 01-Backend-API` → commit → `git push` → PR.
- To see other parts temporarily: `git sparse-checkout disable`; to go back: `git sparse-checkout set 01-Backend-API`.
- Machine-specific settings (connection string) go in **user-secrets**, not `appsettings.json`, to avoid conflicts between teammates' SQL Server instances (`.` vs `.\SQLEXPRESS`):
  `dotnet user-secrets set "ConnectionStrings:QuanCaPheDB" "Server=.;Database=QuanCaPheDB;Trusted_Connection=True;TrustServerCertificate=True"`

## Overview

ASP.NET Core 8 Web API for a coffee shop ordering system (customer tablet ordering, staff POS, owner admin). SQL Server database `QuanCaPheDB`, EF Core, JWT auth. All identifiers, routes, JSON fields, and messages are in Vietnamese (no diacritics in code). README.md (Vietnamese) has full setup steps, endpoint table, and sample JSON.

## Commands

Run from `01-Backend-API/`:

```powershell
dotnet build
dotnet run                      # http://localhost:3000 — Swagger UI at /docs, health at /api/v1/health

# Create/recreate the DB (DROPS and recreates QuanCaPheDB). -I is required (QUOTED_IDENTIFIER for filtered indexes)
sqlcmd -S . -E -C -I -f 65001 -i database\QuanCaPheDB.sql
```

- No test project exists. Manual testing is done with `CoffeeShop.Api.http` (REST Client; paste tokens into `@tokenQL` / `@tokenNV`) or Swagger `/docs`.
- Seed accounts (created on startup when `NguoiDung` is empty and `SeedSampleData` is true): `quanly` / `QuanLy@123` (QuanLy role), `nhanvien` / `NhanVien@123` (NhanVien role).
- Port 3000 is set in both `appsettings.json` (`Urls`) and `launchSettings.json`, and `servers.url` in `openapi.yaml` must match.

## Architecture

**API-first: `openapi/openapi.yaml` is the source of truth.** Swagger UI serves that file directly (`/openapi.yaml`); nothing is generated from code. When adding/changing an endpoint, the order is: edit `openapi.yaml` → DTOs in `Dtos/Dtos.cs` matching the schema → Service + Controller → add a sample to `CoffeeShop.Api.http`. Keep all four in sync — the Flutter tablet and Web Admin teams build against `openapi.yaml`.

**Database-first.** The schema is owned by `database/QuanCaPheDB.sql` (tables, constraints, and the stored procedure `sp_ThanhToanVaTruKho`). `Data/AppDbContext.cs` only maps entities to existing tables — no EF migrations, no `EnsureCreated`. Schema changes go in the SQL script (plus a standalone patch script in `database/` for existing DBs, like `BoSoBan_ThuTuHienThi.sql` / `FixCongThucUnique.sql`), then mirror them in `Data/Entities.cs` and `AppDbContext`. Note entity names differ from table names in places (e.g. entity `Mon` → table `TenMon`).

**Layers:**
- `Controllers/` — thin; route prefix `api/v1/<kebab-case>`. Authorization via `[AllowAnonymous]` (customer tablet endpoints) or `[Authorize(Roles = VaiTros.QuanLy)]` / `[Authorize(Roles = VaiTros.TatCa)]` (staff + manager). Current user id from `User.MaNguoiDung()` (`Common/Security.cs`; claims are not remapped — `role`/`name` claim types).
- `Services/` — business logic: `MenuService` (menu with computed promo price/availability), `DonHangService` (ordering, payment, cancel), `KhoService` (inventory transactions + depletion forecast).
- `Common/Mapping.cs` — entity → DTO extension methods; also holds derived business rules (`GiaSauGiam`, `DuNguyenLieu`, `coTheBan`). These `ToDto` methods expect specific navigation properties to be `Include`d (documented in their comments) — missing an Include silently produces wrong availability/prices.
- DTOs are C# records in a single file, `Dtos/Dtos.cs`. JSON field names are camelCase of the DB column names (`MaTenMon` → `maTenMon`).

**Error contract.** Every error response is `{ "error": { "code", "message", "details" } }`. Throw `ApiException` (factory helpers: `BadRequest`, `NotFound`, `Conflict`, `Unprocessable(code, …)`) from services; `ErrorHandlingMiddleware` serializes it. It also maps unique-constraint `DbUpdateException` → 409, model validation → 400 `VALIDATION_ERROR`, JWT challenge/forbid → 401/403, unknown routes → 404. Unknown JSON properties in request bodies are rejected (`UnmappedMemberHandling.Disallow`) to mirror `additionalProperties: false`.

**Key business rules** (see README §5 for full table):
- Promo price: applied when the dish's `KhuyenMai` is active by date range *and* optional time-of-day window; the discounted unit price is snapshotted into `ChiTietDonHang` at order time.
- Ordering checks recipe (`CongThucMon`) quantities for dish-size + toppings × qty against `NguyenLieu.SoLuongTon`; shortage → 422 `HET_NGUYEN_LIEU` with details.
- Payment calls `EXEC sp_ThanhToanVaTruKho` (marks PAID, deducts stock, writes `LichSuKho` rows of type `TieuHaoTheoDon`); the API then returns low-stock warnings.
- Deletes are soft: dishes/toppings set `NgungBan = 1`, ingredients set `DangSuDung = 0`.
- `CongThucMon` uses filtered unique indexes (not UNIQUE constraints) because a row references either a dish-size or a topping, with the other column NULL.
