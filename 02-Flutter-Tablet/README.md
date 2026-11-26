# Coffee Shop — App tablet (Flutter)

App khách tự đặt món trên tablet tại quán: xem menu → chọn size, đá, đường, topping → giỏ hàng → thanh toán **chuyển khoản QR** hoặc **tiền mặt tại quầy**. Khi nhân viên xác nhận đã thu tiền trên web admin, tablet tự hiện màn hình "Thanh toán thành công" / "Đã thanh toán bằng tiền mặt".

## Cần cài trước

| Công cụ | Ghi chú |
|---|---|
| [Flutter](https://docs.flutter.dev/get-started/install) **3.44** trở lên (Dart ≥ 3.12) | Kiểm tra: `flutter --version`, `flutter doctor` |
| Visual Studio có workload **"Desktop development with C++"** | Chỉ cần nếu chạy trên **Windows** |
| Android Studio + máy ảo hoặc tablet thật | Chỉ cần nếu chạy trên **Android** |
| Backend `01-Backend-api` đang chạy | Xem mục 2 |

## 1. Tải code

```bash
git clone https://github.com/Maianh-26/Coffeshop.git
cd Coffeshop
git pull origin main
```

> Khi PR tablet chưa được gộp vào `main`, lấy code ở nhánh `flutter-tablet`:
> `git switch flutter-tablet`

## 2. Chạy backend trước

Tablet gọi API tại `http://127.0.0.1:3000/api/v1` (máy ảo Android: `http://10.0.2.2:3000/api/v1`). Làm theo `01-Backend-api/README.md` để tạo CSDL và chạy API, rồi mở thử http://localhost:3000/api/v1/health.

Tablet cần 2 endpoint sau trong backend (có từ PR #17 — nhánh `api-tablet-thanh-toan`; nếu `main` chưa có thì chạy backend từ nhánh đó):

- `GET /don-hang/{id}/trang-thai` — tablet hỏi mỗi 3 giây xem đơn đã thanh toán chưa
- `PUT /don-hang/{id}/phuong-thuc-thanh-toan` — khách đổi QR ↔ tiền mặt

CSDL đã tạo từ trước thì chạy thêm các patch trong `01-Backend-api/database/` (luôn có `-I`):

```bash
sqlcmd -S .\SQLEXPRESS -E -C -I -f 65001 -i 01-Backend-api\database\DongBoWebAdmin_20261118.sql
sqlcmd -S .\SQLEXPRESS -E -C -I -f 65001 -i 01-Backend-api\database\ToiUuHieuNang_20261123.sql
```

## 3. Chạy app

```bash
cd 02-Flutter-Tablet
flutter pub get
flutter run -d windows        # chạy trên Windows
flutter run -d chrome         # hoặc trên trình duyệt
flutter run                   # hoặc trên máy ảo / tablet Android đang cắm
```

**Tablet thật** (cùng Wi-Fi với máy chạy API): lấy IPv4 của máy chạy API bằng `ipconfig`, rồi:

```bash
flutter run --dart-define=API_BASE_URL=http://192.168.1.10:3000/api/v1
```

Máy chạy API phải mở cổng 3000 trên Windows Firewall và API phải nghe trên mạng LAN (không chỉ `localhost`).

**Tài khoản nhận tiền QR** mặc định là tài khoản của quán (MB Bank). Đổi khi build:

```bash
flutter run --dart-define=BANK_BIN=970422 --dart-define=BANK_ACCOUNT=<số tài khoản>
```

## 4. Thử luồng thanh toán

1. Tablet: chọn món → **Giỏ hàng**.
2. Chọn cách trả:
   - **Chuyển khoản QR** → bấm **Thanh toán** → hiện mã VietQR (đã có số tiền + nội dung `DH{mã đơn}`).
   - **Tiền mặt** → bấm **Đặt món** → hiện "Mời bạn thanh toán tiền mặt tại quầy" + mã đơn.
3. Web admin (http://localhost:5101) → **Đơn hàng** → chọn đơn → **"Xác nhận đã thu tiền mặt"** / **"Xác nhận đã nhận chuyển khoản"**.
4. Trong ~3 giây tablet hiện màn hình thành công → bấm **X** để về Danh mục.

## 5. Kiểm tra code

```bash
flutter analyze
flutter test
```

## Cấu trúc thư mục

```
lib/
├── main.dart                 Khung app, điều hướng, luồng thanh toán
├── cart.dart                 Giỏ hàng + tạo đơn (CartStore)
├── config/api_config.dart    Địa chỉ backend
├── models/                   Món, size, topping, danh mục (từ API)
├── pages/                    Giới thiệu, Danh mục, Giỏ hàng
├── payment/vietqr.dart       Sinh mã VietQR động (chuẩn EMVCo/NAPAS)
├── providers/                Tải menu từ API
├── services/                 Gọi API (menu, đơn hàng)
└── widgets/                  Header, popup thanh toán, thành phần dùng chung
test/                         Test: giỏ hàng, VietQR, luồng QR + tiền mặt
```

## Lỗi thường gặp

| Hiện tượng | Cách xử lý |
|---|---|
| "Không kết nối được máy chủ" | Backend chưa chạy, sai IP/cổng, hoặc tường lửa chặn cổng 3000 |
| Tạo đơn được nhưng tablet chờ mãi không báo thành công | Backend thiếu `GET /don-hang/{id}/trang-thai` → chạy backend có PR #17 |
| `Unable to generate build files` khi chạy Windows | Cài workload "Desktop development with C++" trong Visual Studio; không để project trong thư mục Temp |
| App ngân hàng báo mã QR không hợp lệ | Kiểm tra `BANK_BIN` / `BANK_ACCOUNT` |
