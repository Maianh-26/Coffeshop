# So kết quả API (cổng 3100, DB thử) với SQL chạy thẳng trên QuanCaPheDB_Bench
import json, subprocess, urllib.request

H = "http://localhost:3100/api/v1"

def req(path, body=None, token=None):
    r = urllib.request.Request(H + path, data=json.dumps(body).encode() if body else None,
                               headers={"Content-Type": "application/json", **({"Authorization": "Bearer " + token} if token else {})})
    with urllib.request.urlopen(r, timeout=120) as f:
        return json.load(f)

def sql(q):
    out = subprocess.run(["sqlcmd", "-S", ".", "-E", "-C", "-I", "-d", "QuanCaPheDB_Bench", "-h", "-1", "-W", "-s", "|",
                          "-Q", "SET NOCOUNT ON; " + q], capture_output=True, text=True, encoding="utf-8").stdout
    return [l.split("|") for l in out.strip().splitlines() if l.strip()]

tok = req("/auth/dang-nhap", {"tenDangNhap": "quanly", "matKhau": "QuanLy@123"})["token"]
get = lambda p: req(p, token=tok)
ok = True
def check(name, a, b):
    global ok
    good = a == b
    ok &= good
    print(("ĐÚNG " if good else "SAI  ") + name + ("" if good else f"\n   API: {a}\n   SQL: {b}"))

# 1. Tìm kiếm: tiền tố mã đơn + số nhận món
for tk in ["5234", "523456", "7", "1", "0", "0123", "2147483647", "9999999999"]:
    r = get(f"/don-hang?page=1&limit=20&timKiem={tk}")
    so = int(tk) if tk.isdigit() and 1 <= int(tk) <= 50 else None
    cond = f"CONVERT(varchar(11), MaDonHang) LIKE '{tk}%'" + (f" OR SoNhanMon = {so}" if so else "")
    total = int(sql(f"SELECT COUNT(*) FROM DonHang WHERE {cond}")[0][0])
    ids = [int(x[0]) for x in sql(f"SELECT TOP 20 MaDonHang FROM DonHang WHERE {cond} ORDER BY ThoiGianTao DESC, MaDonHang DESC")]
    check(f"tìm '{tk}': tổng {r['meta']['total']}", (r["meta"]["total"], [d["maDonHang"] for d in r["data"]]), (total, ids))

# 2. Sắp xếp + phân trang
orders = {
    "tongTien": "TongTien ASC, MaDonHang DESC", "-tongTien": "TongTien DESC, MaDonHang DESC",
    "maDonHang": "MaDonHang ASC", "-maDonHang": "MaDonHang DESC", "": "ThoiGianTao DESC, MaDonHang DESC"}
for sx, ob in orders.items():
    for page in [1, 2, 25000]:
        r = get(f"/don-hang?page={page}&limit=20" + (f"&sapXep={sx}" if sx else ""))
        ids = [int(x[0]) for x in sql(f"SELECT MaDonHang FROM DonHang ORDER BY {ob} OFFSET {(page-1)*20} ROWS FETCH NEXT 20 ROWS ONLY")]
        check(f"sắp xếp '{sx or 'mặc định'}' trang {page}", [d["maDonHang"] for d in r["data"]], ids)

# 3. Món + topping của các đơn trong trang khớp DB
r = get("/don-hang?page=1&limit=20&sapXep=-tongTien")
for d in r["data"][:5]:
    n = int(sql(f"SELECT COUNT(*) FROM ChiTietDonHang WHERE MaDonHang = {d['maDonHang']}")[0][0])
    nt = int(sql(f"SELECT COUNT(*) FROM ChiTietDonHang_Topping t JOIN ChiTietDonHang c ON c.MaChiTietDon = t.MaChiTietDon WHERE c.MaDonHang = {d['maDonHang']}")[0][0])
    api_t = sum(len(c.get("toppings") or c.get("topping") or []) for c in d["chiTiet"])
    check(f"đơn #{d['maDonHang']}: {len(d['chiTiet'])} món, {api_t} topping", (len(d["chiTiet"]), api_t), (n, nt))

# 4. Báo cáo doanh thu (cả năm + 1 ngày)
for tu, den in [("2025-11-23T00:00:00", "2026-11-23T00:00:00"), ("2026-11-22T00:00:00", "2026-11-22T23:59:59")]:
    b = get(f"/bao-cao/doanh-thu?tuNgay={tu}&denNgay={den}")
    w = f"TrangThaiThanhToan='PAID' AND ThoiGianThanhToan IS NOT NULL AND ThoiGianThanhToan >= '{tu}' AND ThoiGianThanhToan <= '{den}'"
    s = sql(f"SELECT COUNT(*), SUM(TongTien) FROM DonHang WHERE {w}")[0]
    ngay = sql(f"SELECT CONVERT(varchar(10), CONVERT(date, ThoiGianThanhToan), 23), COUNT(*), SUM(TongTien) FROM DonHang WHERE {w} GROUP BY CONVERT(date, ThoiGianThanhToan) ORDER BY 1")
    pt = dict((k, float(v)) for k, v in sql(f"SELECT PhuongThucThanhToan, SUM(TongTien) FROM DonHang WHERE {w} GROUP BY PhuongThucThanhToan"))
    hoan = int(sql(f"SELECT COUNT(*) FROM DonHang WHERE DaHoanTien = 1 AND GioHoanTien IS NOT NULL AND GioHoanTien >= '{tu}' AND GioHoanTien <= '{den}'")[0][0])
    keys = list(b.keys())
    api = (b["soDonDaThanhToan"] if "soDonDaThanhToan" in b else None, float(b["doanhThu"]),
           [(x["ngay"], x["soDon"], float(x["doanhThu"])) for x in b["theoNgay"]] if "theoNgay" in b else None,
           {k: float(v) for k, v in b["theoPhuongThuc"].items()} if "theoPhuongThuc" in b else None, b.get("soDonHoanTien"))
    exp = (int(s[0]), float(s[1] or 0), [(a, int(c), float(d)) for a, c, d in ngay], pt, hoan)
    check(f"doanh thu {tu[:10]}→{den[:10]} ({int(s[0])} đơn)", api, exp)
    if api[0] is None or api[2] is None or api[3] is None: print("   khóa JSON:", keys)

print("\nTẤT CẢ ĐÚNG" if ok else "\nCÓ SAI KHÁC")
