<#
.SYNOPSIS
  Sinh 30 đơn mẫu đủ các trạng thái qua API thật, để mọi lần kiểm thử cùng xuất phát điểm.

.DESCRIPTION
  Chạy SAU tao-db-kiem-thu.ps1 và khi backend đang chạy trên CSDL kiểm thử (đã tự seed thực đơn).
  Mọi thao tác đi qua API (đặt món, gán số, thu tiền, đổi trạng thái phục vụ, hủy) nên dữ liệu
  đúng luật của backend (giá khuyến mãi, trừ kho, lịch sử kho). Thu tiền luôn làm TRƯỚC khi phục vụ.
  Riêng 3 đơn treo được lùi giờ tạo về hôm qua bằng SQL (API không cho đặt giờ tạo).

  Kết quả: 6 chờ thu · chờ phục vụ | 4 chờ thu · đang chuẩn bị | 4 đã thu · chờ phục vụ |
           6 đã thu · đã phục vụ | 2 đã thu · đang chuẩn bị | 3 hủy không thu | 2 hủy có hoàn tiền | 3 đơn treo.

.EXAMPLE
  ./tao-don-mau.ps1 -Api http://localhost:3100/api/v1/
#>
param(
    [string]$Api = "http://localhost:3100/api/v1/",
    [string]$TenDangNhap = "quanly",        # tài khoản mẫu backend tự tạo (Data/SeedData.cs)
    [string]$MatKhau = "QuanLy@123",
    [string]$MayChu = ".",
    [string]$TenDb = "QuanCaPheDB_KiemThu"
)

$ErrorActionPreference = "Stop"
# Chỉ nhận tên CSDL kiểm thử dạng QuanCaPheDB_<chữ/số/_> — chặn mọi cách gõ trùng / chèn lệnh vào tên CSDL thật
if ($TenDb -cnotmatch '^QuanCaPheDB_[A-Za-z0-9_]+$') { throw "Tên CSDL kiểm thử phải có dạng QuanCaPheDB_<tên> (chỉ chữ, số, _): '$TenDb'" }
$Api = $Api.TrimEnd('/') + '/'

# Chạy một câu SQL trên CSDL kiểm thử, trả về kết quả dạng chuỗi (-I: bảng DonHang có index có lọc cần QUOTED_IDENTIFIER)
function Invoke-Sql([string]$Cau) {
    $ketQua = & sqlcmd -S $MayChu -E -C -I -b -h -1 -W -d $TenDb -Q "SET NOCOUNT ON; $Cau"
    if ($LASTEXITCODE -ne 0) { $ketQua | Write-Host; throw "sqlcmd lỗi: $Cau" }
    "$ketQua".Trim()
}

# CSDL kiểm thử phải trống: chạy lại lần hai sẽ trùng số nhận món và sai "đơn treo mã 1, 2, 3"
if ((Invoke-Sql "SELECT COUNT(*) FROM DonHang") -ne "0") { throw "$TenDb đã có đơn — chạy lại tao-db-kiem-thu.ps1 trước." }

# Gửi JSON UTF-8 (PowerShell 5.1 mặc định không gửi UTF-8 cho tiếng Việt)
function Invoke-Api([string]$Method, [string]$Path, $Body = $null) {
    $tham = @{ Method = $Method; Uri = "$Api$Path"; Headers = $script:Headers }
    if ($null -ne $Body) {
        $tham.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 5 -Compress))
        $tham.ContentType = "application/json; charset=utf-8"
    }
    Invoke-RestMethod @tham
}

$script:Headers = @{}
$dangNhap = Invoke-Api POST "auth/dang-nhap" @{ tenDangNhap = $TenDangNhap; matKhau = $MatKhau }
$script:Headers = @{ Authorization = "Bearer $($dangNhap.token)" }

# Mã món-size theo tên món + size (thực đơn mẫu của backend)
$mon = (Invoke-Api GET "mon?limit=100").data
function Get-Size([string]$TenMon, [string]$Size) {
    $m = $mon | Where-Object { $_.tenMon -eq $TenMon }
    if (-not $m) { throw "Không thấy món '$TenMon' — CSDL chưa có thực đơn mẫu?" }
    $ma = ($m.kichThuoc | Where-Object { $_.maKichThuoc -eq $Size }).maChiTietMon
    if (-not $ma) { throw "Món '$TenMon' không có size $Size" }
    $ma
}
$topping = (Invoke-Api GET "topping") | Where-Object { $_.tenTopping -eq "Trân châu đen" } | Select-Object -First 1
if (-not $topping) { throw "Không thấy topping 'Trân châu đen' — CSDL chưa có thực đơn mẫu?" }

$mau = @(
    @{ maChiTietMon = (Get-Size "Cà phê sữa đá" "M"); soLuong = 1 },
    @{ maChiTietMon = (Get-Size "Bạc xỉu" "L"); soLuong = 2 },
    @{ maChiTietMon = (Get-Size "Trà sữa" "M"); soLuong = 1; toppings = @($topping.maTopping); ghiChu = "Ít đá" },
    @{ maChiTietMon = (Get-Size "Cà phê đen" "M"); soLuong = 1 },
    @{ maChiTietMon = (Get-Size "Trà đào cam sả" "L"); soLuong = 1 }
)

$soNhan = 0   # số nhận món tăng dần, không trùng giữa các đơn đang giữ số
function New-Don([int]$i, [string]$PhuongThuc = "CASH") {
    $dong = $mau[$i % $mau.Count]
    $don = Invoke-Api POST "don-hang" @{ phuongThucThanhToan = $PhuongThuc; chiTiet = @($dong) }
    $don.maDonHang
}
function Set-So([int]$Id) { $script:soNhan++; Invoke-Api PUT "don-hang/$Id/so-nhan-mon" @{ soNhanMon = $script:soNhan } | Out-Null }
function Pay([int]$Id, [string]$PhuongThuc = "CASH") { Invoke-Api POST "don-hang/$Id/thanh-toan" @{ phuongThucThanhToan = $PhuongThuc } | Out-Null }
function Set-PhucVu([int]$Id, [string]$TrangThai) { Invoke-Api PUT "don-hang/$Id/trang-thai-phuc-vu" @{ trangThaiPhucVu = $TrangThai } | Out-Null }
function Cancel([int]$Id, [string]$LyDo) { Invoke-Api POST "don-hang/$Id/huy" @{ lyDoHuy = $LyDo } | Out-Null }

$n = 0
$treo = @()
# 3 đơn treo (chưa thu, sẽ lùi về hôm qua) — tạo trước để mã nhỏ nhất
1..3 | ForEach-Object { $treo += (New-Don ($n++)) }
# Backend phải đang ghi vào đúng CSDL kiểm thử (không phải QuanCaPheDB thật do quên đặt chuỗi kết nối / gọi nhầm cổng)
if ((Invoke-Sql "SELECT COUNT(*) FROM DonHang WHERE MaDonHang = $([int]$treo[0])") -ne "1") {
    throw "Backend ở $Api không ghi vào $TenDb — kiểm tra ConnectionStrings__QuanCaPheDB của backend. Đã dừng sau 3 đơn."
}
# 6 chờ thu · chờ phục vụ (3 đơn có số nhận món); 2 đơn khách chọn chuyển khoản
1..6 | ForEach-Object { $id = New-Don ($n++) ($(if ($_ -le 2) { "QR_TRANSFER" } else { "CASH" })); if ($_ -le 3) { Set-So $id } }
# 4 chờ thu · đang chuẩn bị
1..4 | ForEach-Object { $id = New-Don ($n++); Set-So $id; Set-PhucVu $id "PREPARING" }
# 4 đã thu · chờ phục vụ
1..4 | ForEach-Object { $id = New-Don ($n++); Pay $id }
# 2 đã thu · đang chuẩn bị
1..2 | ForEach-Object { $id = New-Don ($n++); Pay $id "QR_TRANSFER"; Set-So $id; Set-PhucVu $id "PREPARING" }
# 6 đã thu · đã phục vụ (thu tiền trước khi phục vụ)
1..6 | ForEach-Object { $pt = $(if ($_ % 2) { "CASH" } else { "QR_TRANSFER" }); $id = New-Don ($n++) $pt; Pay $id $pt; Set-So $id; Set-PhucVu $id "PREPARING"; Set-PhucVu $id "SERVED" }
# 3 hủy không thu
1..3 | ForEach-Object { $id = New-Don ($n++); Cancel $id "Khách đổi ý (dữ liệu mẫu)" }
# 2 hủy có hoàn tiền (đã thu, chưa pha)
1..2 | ForEach-Object { $id = New-Don ($n++); Pay $id; Cancel $id "Khách hủy sau khi trả tiền (dữ liệu mẫu)" }

# Lùi giờ tạo của 3 đơn treo về hôm qua (mã đơn ép kiểu số nguyên trước khi ghép vào câu SQL)
$dsMa = ($treo | ForEach-Object { [int]$_ }) -join ","
Invoke-Sql "UPDATE DonHang SET ThoiGianTao = DATEADD(DAY, -1, ThoiGianTao) WHERE MaDonHang IN ($dsMa);" | Out-Null

Write-Host "Đã tạo $n đơn mẫu (đơn treo: $dsMa)."
