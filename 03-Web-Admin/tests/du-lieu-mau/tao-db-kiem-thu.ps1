<#
.SYNOPSIS
  Tạo (hoặc tạo lại) CSDL kiểm thử QuanCaPheDB_KiemThu từ script gốc của backend — KHÔNG đụng QuanCaPheDB thật.

.DESCRIPTION
  1. Đọc 01-Backend-api/database/QuanCaPheDB.sql, đổi tên CSDL thành $TenDb rồi chạy (script gốc tự DROP + CREATE).
  2. Chạy FixCongThucUnique.sql (cũng đổi tên CSDL) — thiếu bước này backend seed lỗi UQ_CongThuc_Topping.
  Sau đó chạy backend trỏ vào CSDL này: backend tự tạo tài khoản mẫu + thực đơn + kho + khuyến mãi khi CSDL trống.

.EXAMPLE
  ./tao-db-kiem-thu.ps1
  $env:ConnectionStrings__QuanCaPheDB = "Server=.;Database=QuanCaPheDB_KiemThu;Trusted_Connection=True;TrustServerCertificate=True"
  dotnet run --project ../../../01-Backend-api --urls http://localhost:3100
#>
param(
    [string]$MayChu = ".",
    [string]$TenDb = "QuanCaPheDB_KiemThu"
)

$ErrorActionPreference = "Stop"
# Chỉ nhận tên CSDL kiểm thử dạng QuanCaPheDB_<chữ/số/_> — chặn mọi cách gõ trùng / chèn lệnh vào tên CSDL thật
if ($TenDb -cnotmatch '^QuanCaPheDB_[A-Za-z0-9_]+$') { throw "Tên CSDL kiểm thử phải có dạng QuanCaPheDB_<tên> (chỉ chữ, số, _): '$TenDb'" }

$thuMucDb = Join-Path $PSScriptRoot "..\..\..\01-Backend-api\database"
$tam = Join-Path ([IO.Path]::GetTempPath()) "coffeeshop-kiem-thu"
New-Item -ItemType Directory -Force $tam | Out-Null

foreach ($tenFile in @("QuanCaPheDB.sql", "FixCongThucUnique.sql")) {
    # Đổi đúng tên CSDL (\b: không đụng tên khác bắt đầu bằng QuanCaPheDB)
    $noiDung = [IO.File]::ReadAllText((Join-Path $thuMucDb $tenFile), [Text.Encoding]::UTF8)
    $noiDung = [regex]::Replace($noiDung, '\bQuanCaPheDB\b', $TenDb)
    $fileTam = Join-Path $tam $tenFile
    [IO.File]::WriteAllText($fileTam, $noiDung, (New-Object Text.UTF8Encoding $false))

    Write-Host "Chạy $tenFile trên $TenDb ..."
    # -I: bật QUOTED_IDENTIFIER (index có lọc); -b: dừng khi lỗi; -f 65001: đọc file UTF-8
    $ketQua = & sqlcmd -S $MayChu -E -C -I -b -f 65001 -i $fileTam
    if ($LASTEXITCODE -ne 0) { $ketQua | Write-Host; throw "sqlcmd lỗi khi chạy $tenFile (xem thông báo phía trên)" }
}

Write-Host "Xong. Chuỗi kết nối cho backend:"
Write-Host "Server=$MayChu;Database=$TenDb;Trusted_Connection=True;TrustServerCertificate=True"
