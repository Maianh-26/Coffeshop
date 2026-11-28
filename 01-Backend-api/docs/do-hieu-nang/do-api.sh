#!/usr/bin/env bash
# Đo API trên DB thử 1 triệu đơn. Dùng: bench.sh <nhãn>  → ghi kết quả vào results_<nhãn>.tsv
H=http://localhost:3100/api/v1
DIR="$(cd "$(dirname "$0")" && pwd)"
OUT="$DIR/results_$1.tsv"
RUNS=${RUNS:-5}

TOKEN=$(curl -s -X POST "$H/auth/dang-nhap" -H "Content-Type: application/json" \
  -d '{"tenDangNhap":"quanly","matKhau":"QuanLy@123"}' | sed -E 's/.*"token":"([^"]+)".*/\1/')
[ ${#TOKEN} -lt 50 ] && { echo "Đăng nhập thất bại"; exit 1; }

NAM="tuNgay=2025-11-23T00:00:00&denNgay=2026-11-23T00:00:00"
HOMNAY="tuNgay=2026-11-22T00:00:00&denNgay=2026-11-22T23:59:59"
TUAN="tuNgay=2026-11-16T00:00:00&denNgay=2026-11-22T23:59:59"

CASES=(
  "Danh sách trang 1 (mặc định, mới nhất)|don-hang?page=1&limit=20"
  "Sắp xếp tổng tiền cao→thấp|don-hang?page=1&limit=20&sapXep=-tongTien"
  "Sắp xếp tổng tiền thấp→cao|don-hang?page=1&limit=20&sapXep=tongTien"
  "Sắp xếp mã đơn giảm dần|don-hang?page=1&limit=20&sapXep=-maDonHang"
  "Trang 25.000 (rất xa)|don-hang?page=25000&limit=20"
  "Lọc hôm nay|don-hang?page=1&limit=20&$HOMNAY"
  "Lọc PAID trong 7 ngày|don-hang?page=1&limit=20&trangThai=PAID&$TUAN"
  "Tìm mã đơn 523456|don-hang?page=1&limit=20&timKiem=523456"
  "Trang 100 đơn (limit tối đa)|don-hang?page=1&limit=100"
  "Chi tiết 1 đơn (#523456)|don-hang/523456"
  "Báo cáo doanh thu hôm nay|bao-cao/doanh-thu?$HOMNAY"
  "Báo cáo doanh thu cả năm|bao-cao/doanh-thu?$NAM"
  "Món bán chạy cả năm|bao-cao/mon-ban-chay?$NAM"
  "Topping bán chạy cả năm|bao-cao/topping-ban-chay?$NAM"
  "Giờ cao điểm cả năm|bao-cao/gio-cao-diem?$NAM"
)

printf "thao_tac\tms_trung_vi\tms_min\tms_max\tbyte\thttp\n" > "$OUT"
for c in "${CASES[@]}"; do
  name="${c%%|*}"; path="${c#*|}"
  curl -s -o /dev/null -m 180 -H "Authorization: Bearer $TOKEN" "$H/$path"   # làm nóng
  times=(); size=0; code=0
  for ((r=0; r<RUNS; r++)); do
    read -r t s code < <(curl -s -o /dev/null -m 180 -w "%{time_total} %{size_download} %{http_code}" \
      -H "Authorization: Bearer $TOKEN" "$H/$path")
    times+=("$(awk -v t="$t" 'BEGIN{printf "%.0f", t*1000}')"); size=$s
  done
  sorted=($(printf "%s\n" "${times[@]}" | sort -n))
  med=${sorted[$((RUNS/2))]}
  printf "%s\t%s\t%s\t%s\t%s\t%s\n" "$name" "$med" "${sorted[0]}" "${sorted[-1]}" "$size" "$code" | tee -a "$OUT"
done
