# Bàn giao tiến độ: Quản lý đơn hàng (web admin)

> Cập nhật: 12/11/2026. File này để **phiên Claude mới đọc đầu tiên** và nắm được việc đã làm, lý do các quyết định, và việc còn lại.
> Đọc kèm `CLAUDE.md`, đặc biệt mục **"Quy trình bắt buộc khi sửa / nâng cấp chức năng"**.

## 1. Tài liệu liên quan

| File | Nội dung |
|---|---|
| `docs/yeu-cau-quan-ly-don-hang.md` | Yêu cầu gốc của chủ quán (chuyển từ file Word) |
| `docs/de-xuat-api-don-hang.md` + `.docx` | Đề xuất API cho nhóm backend, **v2.1** (bản .docx sinh từ .md, 17 trang) |
| `CLAUDE.md` | Quy ước dự án và quy trình bắt buộc (skill → làm → agent review) |

## 2. Trạng thái Git

- Nhánh `web-admin`. Commit `eb1d588` (phần đầu: số nhận món, trạng thái phục vụ, sửa đơn, API đề xuất, mock) và `aa66bbf` (làm lại giao diện, bước phục vụ tuần tự, đề xuất API v2.1, quy tắc CLAUDE.md, file này) **đã vào `main`** qua PR #3 và #4 của repo nhóm.
- Phần ở mục 7 (lọc thời gian, sắp xếp, ô chọn hình thức thanh toán, hóa đơn K80, `SortIcon.razor`, `DocSoTien.cs`) **chưa commit** tính đến 13/11/2026.
- `wwwroot/appsettings.json` chỉ còn `Api:BaseUrl` và mục `Receipt`. Chế độ dữ liệu giả (`Services/Mock`) đã xóa ngày 24/11/2026.
- Chưa commit (cố ý): `.playwright-mcp/` (ảnh test, ở cả gốc repo lẫn `03-Web-Admin/`). 2 ảnh trong `docs/` (`Screenshot …png`, `1793106…jpg`) chưa rõ có nên commit không, cần hỏi người dùng.

## 3. Chức năng đã làm trên trang `/orders`

**Danh sách**
- Ô tìm kiếm chỉ nhận chữ số, khớp **mã đơn (tiền tố)** hoặc **số nhận món (chính xác)**. Trước đây tìm cả tên món/nhân viên không hiện trên bảng nên trông như "lọc sai".
- Cột: Mã đơn, Số nhận món, Trạng thái, Tổng tiền (căn phải), Thanh toán (nhãn; hình thức bên dưới **chỉ khi đã thu / đã hoàn tiền**, xem mục 11), Thời gian tạo (đã bỏ cột Nguồn 14/11, xem mục 9). Màn hình hẹp hoặc đang mở chi tiết: ẩn Thời gian. Điện thoại: ẩn thêm Tổng tiền, số tiền hiện dưới nhãn thanh toán.
- Bộ lọc trạng thái phục vụ + bộ lọc thanh toán. Màn hình ≤1000px gom vào nút "Bộ lọc (n)". Nút **Làm mới** (biểu tượng) thay chữ "Xóa lọc" (xem mục 7).
- **Đơn mới từ Tablet**: dò mỗi **3 giây** (25/11: rút từ 10 giây và **bỏ nút "Tự cập nhật"** — trang luôn tự làm mới, không tắt được; xem `ke-hoach-dong-bo-thoi-gian-thuc.md` giai đoạn 0) trên **toàn bộ dữ liệu** (không phụ thuộc trang hay bộ lọc). Thông báo **màu cam** (12/11: chủ quán yêu cầu đổi từ tím sang màu thông dụng hơn; nút Xem không còn icon con mắt). 25/11 (chủ quán chốt): thông báo nằm **trong trang, ngay dưới tiêu đề "Đơn hàng", trên mục lọc** — chấp nhận đẩy phần dưới xuống khi hiện (đã thử đặt trong header / nổi góc dưới nhưng trông lạc nhịp). Không tự ẩn: chỉ mất khi mở xem hoặc bấm ×, có nút **Xem** (mở chi tiết, kể cả đơn không có trong trang đang xem). Dòng mới có vạch cam + chip "Mới" cho đến khi mở. Đơn tạo tại quầy **không** tính là đơn mới (cố ý).
- Thông báo kết quả thao tác (25/11, đồng bộ với các trang quản lý khác): **một dòng ngay dưới nhóm nút Tạo đơn / Sửa / Hủy**, không nổi ở góc, không tự ẩn — thao tác kế tiếp thay nó. Thu tiền mà có nguyên liệu sắp hết thì liệt kê ngay dưới (khung vàng). **Bấm Làm mới không báo gì** (giống trang Kho / Món — yêu cầu chủ quán). Đã bỏ ngăn thông báo nổi góc dưới (`.orders-toasts`). Màn ≤1000px đang mở khung chi tiết (khung nằm dưới danh sách): dòng kết quả + cảnh báo kho hiện ở **đầu khung chi tiết** (cùng một đoạn markup `ActionResult`, CSS chọn chỗ hiện) để không bị khuất; thu tiền xong tiêu điểm về tiêu đề "Đơn hàng #…". Mở Tạo / Sửa / Hủy đơn thì xóa thông báo cũ (như các trang khác).

**Màu (chủ quán chốt: mỗi màu một nghĩa, luôn kèm chữ)**

| Màu | Nghĩa |
|---|---|
| Cam đậm `--orange` #b2470f | Đơn mới (banner, vạch dòng, chip "Mới" chữ trắng). Đổi từ tím theo yêu cầu 12/11. Nút Thu tiền là hổ phách #d97706 chữ tối: khác tông, đã kiểm tra phân biệt được; mở đơn là mất chip "Mới" nên chip và nút Thu tiền của cùng một đơn không hiện cùng lúc. Muốn đổi màu khác chỉ sửa khối "Thông báo đơn mới" trong app.css |
| Vàng | Chờ phục vụ, Chưa thu |
| Xanh dương | Đang chuẩn bị |
| Xanh lá | Đã phục vụ, Đã thu |
| Đỏ | Đã hủy, Đã hoàn tiền |
| Xám | Không thu |

Chuỗi và tông màu nằm một chỗ: `TrangThaiDon.Label`, `TrangThaiPhucVus.Label/Tone`, `PhuongThucThanhToans.ShortLabel/Tone/Summary` (`Models/DonHang.cs`). CSS: các class `.status-*` ở cuối `wwwroot/css/app.css`.

**Chi tiết đơn (khung bên phải)**
- Thứ tự: thông tin → **phiếu order** (số lượng ×, món, giá thẳng cột; topping có giá; ghi chú in nghiêng) → tổng → số nhận món + thanh tiến trình → thu tiền → In hóa đơn.
- **Số nhận món** (bảng gỗ 1–50): ô tròn như thiết kế cũ (chủ quán không thích kiểu "miếng gỗ"). Số đang được đơn chưa phục vụ xong giữ thì bị khóa (báo "Số 18 đang được dùng cho đơn #12"). Đã phục vụ hoặc đã hủy thì giải phóng nhưng vẫn lưu trên đơn.
- **Trạng thái phục vụ đi lần lượt, không nhảy cóc**: Chờ phục vụ → (nút xanh dương **Bắt đầu chuẩn bị**) → Đang chuẩn bị → (nút xanh lá **Xác nhận đã phục vụ**) → Đã phục vụ.
- Bắt đầu chuẩn bị khi chưa có số → hộp hỏi lại "Nhập số nhận món" / "Vẫn bắt đầu (khách mang đi)". Đã gõ số mà chưa bấm Lưu thì lưu luôn rồi bắt đầu.
- Phục vụ khi chưa thu tiền → hộp hỏi lại "Thu tiền trước" / "Vẫn xác nhận đã phục vụ".
- **Thu tiền**: nút cam chữ tối (chữ trắng trên cam không đủ tương phản), **không có ô tiền khách đưa** (chủ quán chọn đơn giản), có link đổi tiền mặt ↔ chuyển khoản.
- **Sửa đơn** (nút trên thanh công cụ): chỉ đơn chưa thu và chưa phục vụ. Dùng lại hộp thoại tạo đơn, gửi `PUT api/v1/don-hang/{id}`.
- **In hóa đơn**: chỉ in hóa đơn khổ 80mm qua iframe ẩn (`appHelpers.printReceipt` trong `wwwroot/js/app.js`, mẫu `#receipt-source`), không in cả trang.
- Bàn phím: đóng chi tiết thì focus về dòng vừa xem; đóng hộp Hủy đơn thì focus về nút Hủy đơn; bấm Xem thì focus vào tiêu đề chi tiết.

## 4. Backend: chưa hỗ trợ, đã gửi đề xuất

API **chưa có** ở `CoffeeShop.Api` (mã nguồn tại `C:\dev\CoffeeShop.Api`, chỉ đọc, không sửa):
`PUT don-hang/{id}`, `PUT don-hang/{id}/so-nhan-mon`, `PUT don-hang/{id}/trang-thai-phuc-vu`, lọc `trangThaiPhucVu`, trường `soNhanMon` / `trangThaiPhucVu`. Với backend thật, nếu response không có `trangThaiPhucVu` thì trang tự ẩn phần phục vụ và báo "Máy chủ chưa hỗ trợ". Chi tiết xem `docs/de-xuat-api-don-hang.md`.

**Lỗi tranh chấp phía backend đã phân tích (chưa sửa, chưa ghi vào đề xuất):**
1. `sp_ThanhToanVaTruKho` kiểm tra PENDING và UPDATE tách rời. Hai người thu tiền cùng lúc → **trừ kho 2 lần**. Sửa: `UPDATE … WHERE TrangThaiThanhToan='PENDING'` + `@@ROWCOUNT`.
2. `DonHangService.ThanhToan` lưu `PhuongThucThanhToan` trước khi gọi SP → request bị từ chối vẫn ghi đè hình thức thanh toán. Sửa: truyền vào SP.
3. Không có kiểm tra tranh chấp (rowversion) → hủy và thu tiền cùng lúc mất cờ hoàn tiền; hai người đổi số của cùng một đơn thì ghi đè nhau. Sửa: cột `RowVersion` + 409 `DON_DA_THAY_DOI`.
Người dùng đã được hỏi có ghi phần này vào tài liệu đề xuất và xử lý 409 thống nhất phía web không, nhưng **chưa trả lời**.

## 5. Việc còn mở / cần hỏi người dùng

- [ ] Push nhánh `web-admin` và mở PR cho trưởng nhóm (chỉ làm khi người dùng yêu cầu).
- [x] ~~`CLAUDE.md` có 3 chỗ lỗi thời~~ — đã sửa 28/11/2026 (dòng về trạng thái thanh toán và sửa đơn đã đúng từ trước; dòng `TrangThaiDon` `Label`/`Color` đổi thành `Label` + `Tone` của `PhuongThucThanhToans` / `TrangThaiPhucVus`). `TrangThaiDon.Color` không còn ai gọi — xóa ở việc 5B.1 của `ke-hoach-cai-tien-chuc-nang.md`.
- [ ] Thu tiền mặt một cú bấm, không hỏi lại, không hoàn tác: agent đề nghị thêm xác nhận hoặc hoàn tác vài giây. Chờ chủ quán quyết định.
- [ ] Có cho hủy đơn "Đã phục vụ" không? Có bắt buộc thu tiền trước khi phục vụ không? Chờ chủ quán.
- [x] ~~Trên điện thoại, thông báo nổi che nút In hóa đơn khoảng 6 giây~~ — hết, đã bỏ thông báo nổi (25/11).
- [ ] Chưa test dòng ghi chú món trên phiếu order (cần tạo một đơn có ghi chú món trên backend thật).
- [ ] Đề xuất "Hoàn tác / Recall" sau khi bấm Đã phục vụ: cần backend cho đi lùi trạng thái.
- [ ] Nâng cấp sau: kết nối thời gian thực (SignalR) thay cho tự làm mới 3 giây — kế hoạch chi tiết: `docs/ke-hoach-dong-bo-thoi-gian-thuc.md` (giai đoạn 0 đã làm 25/11; giai đoạn 3 chờ backend). Ghi lại ai thu tiền / ai chuẩn bị.

## 6. Cách test

- Test với backend thật (`CoffeeShop.Api` ở `http://localhost:3000` + DB `QuanCaPheDB`); đơn tablet tạo bằng `POST api/v1/don-hang` (Swagger hoặc app tablet).
- Chạy app: `dotnet run --launch-profile http` → http://localhost:5101 (đăng nhập `quanly` / `QuanLy@123`). **Kiểm tra cổng 5101 trước**: nếu đang có app (người dùng chạy từ Visual Studio) thì không tắt và hỏi trước khi chạy `ui-ux-reviewer`.
- Mở Claude **từ thư mục `03-Web-Admin`** để các agent `code-reviewer` / `ui-ux-reviewer` được nạp. Mở từ gốc repo thì dùng agent `general-purpose` và bảo nó đọc `.claude/agents/<tên>.md`.
- Mỗi lần sửa `app.css` / `app.js`: tăng `?v=` trong `wwwroot/index.html` (hiện `app.css?v=66`, `app.js?v=7`).

## 7. Cập nhật 12/11/2026 (các phiên sau)

### 7.1. Tìm kiếm, nút Làm mới, màu đơn mới

- **Tìm kiếm**: ô nhập (`searchText`) tách khỏi giá trị lọc (`search`, chỉ chữ số) nên gõ chữ không làm bảng chớp rỗng; `searchVersion` chặn lượt xử lý cũ ghi đè khi gõ nhanh; gợi ý nổi "Chỉ nhập số" (ẩn khi rời ô). Chỉ tìm **trong trang đang xem** (placeholder và chân bảng nói rõ).
- **Nút Làm mới** (biểu tượng, thay chữ "Xóa lọc"): bỏ mọi lọc + tìm kiếm + sắp xếp, về trang 1, tải lại (không báo gì — 25/11). Chấm nâu khi đang lọc/tìm/sắp xếp; chỉ xoay khi người dùng bấm (lượt tự làm mới 3 giây không xoay).
- **Đơn mới màu cam** `--orange` #b2470f (banner, vạch dòng, chip "Mới"), nút Xem không icon. Nút Thu tiền là hổ phách #d97706 chữ tối, khác tông. Điện thoại: chip "Mới" nằm dưới mã đơn để nhãn trạng thái không bị ép xuống dòng ở 375px.

### 7.2. Lọc thời gian: 4 mốc nhanh + chọn khoảng ngày

- *(Cập nhật 25/11/2026 — đồng bộ với trang Tổng quan:)* **Hôm nay** (mặc định), **Trong 24 giờ qua**, **Trong 7 ngày qua**, **Trong 30 ngày qua** (7 / 30 ngày lịch, tính cả hôm nay), **Chọn khoảng ngày…**; đã bỏ "Tất cả thời gian". Nút Làm mới đưa về "Hôm nay".
- *(Cũ)* **Tất cả thời gian** (mặc định), **Trong 24 giờ qua**, **Trong 7 ngày qua**, **Trong 30 ngày qua** (áp dụng ngay) và **Chọn khoảng ngày…** (2 ô có nhãn Từ ngày / Đến ngày, chỉ lọc khi bấm **Áp dụng**). Đã bỏ Hôm nay / Hôm qua / Tháng này / Tháng trước theo yêu cầu; khoảng ngày tự chọn bị mất ở lần rút gọn trước nên đã làm lại (12/11/2026).
- Mốc nhanh: khoảng trượt tính lùi từ **thời điểm hiện tại**, không có mốc kết thúc, tính lại ở đầu mỗi lần tải. Khoảng tự chọn: từ 00:00 ngày bắt đầu đến hết 23:59:59 ngày kết thúc (`denNgay`); chỉ chọn một ngày = lọc đúng ngày đó; khoảng ngược / ngày tương lai → báo lỗi, giữ bộ lọc cũ.
- `OrderService.GetPageAsync` gửi `tuNgay` chính xác đến giờ phút. Giả định: trình duyệt và máy chủ cùng múi giờ (giờ VN).
- Dòng chú thích: "Đang xem đơn tạo từ HH:mm dd/MM/yyyy đến nay (…)" hoặc "Đang xem đơn tạo từ dd/MM/yyyy đến hết dd/MM/yyyy".

### 7.3. Sắp xếp theo cột Mã đơn và Tổng tiền

- Theo mẫu W3C APG "Sortable Table" / Carbon / eBay: tiêu đề là `<button class="sort-button">` + `Components/SortIcon.razor` (2 mũi tên mờ khi chưa sắp xếp, mũi tên đang dùng đậm lên); `aria-sort` chỉ đặt trên cột đang sắp xếp. Bấm cột mới → thấp đến cao; bấm lại → cao đến thấp ↔ thấp đến cao. Làm mới → về mặc định (mới nhất trước).
- Sắp xếp **ở máy chủ trên toàn bộ danh sách** (trang 2 nối tiếp trang 1): tham số `sapXep` = `maDonHang` | `-maDonHang` | `tongTien` | `-tongTien` (backend đã làm, `main` 18/11/2026).
- **23/11/2026:** đã **bỏ** nhánh web tự tải hết danh sách để sắp xếp (`LoadAllAndSort`, `serverSortSupported`, bộ nhớ đệm 60 giây, giới hạn 2.000 đơn). Web chỉ gửi bộ lọc + `sapXep` + `page` và nhận đúng 1 trang. Lý do: đo trên bản sao 1 triệu đơn, việc lọc / sắp xếp / tìm kiếm phải nằm ở máy chủ (có index) — xem `01-Backend-api/database/ToiUuHieuNang_20261123.sql`. Khi đang sắp xếp khác mặc định, việc dò đơn mới vẫn dùng request riêng (trang 1 không còn là đơn mới nhất).
- Điện thoại (≤650px): cột Tổng tiền bị ẩn (số tiền hiện dưới nhãn thanh toán) nên tiêu đề cột Thanh toán thành nút **"Tiền ⇅"** (`.sort-mobile-only`) để vẫn sắp xếp được theo tổng tiền; chữ "Thanh toán" vẫn có cho trình đọc màn hình.

### 7.4. Hình thức thanh toán (thay link "Khách đổi sang chuyển khoản")

- Phân tích: link chỉ đổi biến tạm `payMethod`; hình thức chỉ ghi vào đơn khi bấm xác nhận (`POST thanh-toan` gửi `phuongThucThanhToan`). **Cần giữ chức năng** (khách đặt chọn chuyển khoản nhưng trả tiền mặt hoặc ngược lại → không ghi đúng thì đối soát két / sao kê cuối ca và báo cáo theo hình thức bị lệch), nhưng link chữ khó thấy, không cho biết đang chọn gì và khách ban đầu chọn gì.
- Đã đổi thành ô chọn 2 lựa chọn **Tiền mặt | Chuyển khoản** (`fieldset.choice-picker` — trước 16/11/2026 tên là `.pay-picker`, đổi tên vì trang Kho dùng chung cho ô chọn trạng thái sử dụng; radio thật, dùng được bàn phím) ngay trên nút xác nhận, mặc định = hình thức khách chọn; khác lựa chọn ban đầu thì hiện ghi chú vàng "Khách đã chọn … khi đặt. Đơn sẽ được ghi là … khi xác nhận". Nhãn nút xác nhận đổi theo.

### 7.5. Hóa đơn in (làm lại 12/11/2026 theo mẫu chủ quán gửi)

- **Tên luôn là "HÓA ĐƠN THANH TOÁN"** (bỏ "PHIẾU TẠM TÍNH"). **Không in** tên nhân viên / tài khoản, không in "Thanh toán: chưa thu".
- **Bố cục** (theo mẫu hóa đơn quán trà sữa / cà phê, cá nhân hóa): tên quán (+ địa chỉ, ĐT nếu cấu hình) → HÓA ĐƠN THANH TOÁN → `HĐ: 0021` + Số nhận món → Ngày + Giờ → bảng **TÊN HÀNG | SL | Đ.GIÁ | T.TIỀN** (món đánh số 1, 2, 3…, SL căn giữa, dòng topping thụt vào, dòng ghi chú in nghiêng) → **T.CỘNG** (tổng số lượng + tổng tiền) → **TỔNG THANH TOÁN** + **số tiền bằng chữ** (`Utilities/DocSoTien.cs`, vd. 480.000 → "Bốn trăm tám mươi nghìn đồng") → hình thức + số tiền (chỉ khi đã thu) → lời cảm ơn.
- **Khổ giấy chuẩn K80**: rộng 80mm, vùng in 72mm (lề trái/phải 4mm), cột số cố định (SL 7mm, Đ.GIÁ 15mm, T.TIỀN 16mm), tên món lấy phần còn lại và tự xuống dòng. Dài theo nội dung: đo chiều cao rồi đặt `@page { size: 80mm <cao>mm; margin: 0 }` → 1 trang liền (đơn 2 món ≈ 85–90mm; thử ~40 dòng: 80×492mm). `Receipt:PaperWidthMm = 58` cho máy K58 (vùng in 48mm, cột nhỏ hơn).
- **Lỗi "trên đầu có ngày + Hóa đơn, cuối có đường dẫn https"**: đó là header/footer Chrome tự in vào LỀ trang (tiêu đề tài liệu "Hóa đơn", ngày giờ, URL) — xảy ra khi in ra máy in / khổ không phải 80mm (vd. PDF, A4) với lề mặc định. Sửa: `@page { margin: 0 }` (không còn chỗ in header/footer; lề thật chuyển vào padding của body) + `<title>` rỗng. Đã tái hiện lỗi với bản cũ (A4, lề mặc định, bật header/footer → có ngày, "Hóa đơn", file:///…). Nếu máy in của quán vẫn hiện, bỏ tick "Đầu trang và chân trang" trong hộp thoại in một lần (Chrome nhớ lựa chọn) hoặc chạy Chrome ở chế độ `--kiosk-printing` trên máy thu ngân.
- Cấu hình `wwwroot/appsettings.json` mục `Receipt`: `ShopName`, `Address`, `Phone`, `FooterNote`, `PaperWidthMm`.
- Kiểm thử không cần hộp thoại in: `appHelpers.buildReceiptFrame("#receipt-source", 80)` trả `{ frame, heightMm, … }` (nhớ `frame.remove()`).

### 7.6. Kiểm tra

- Đã chạy thử bằng dữ liệu giả: 4 mốc thời gian khớp dữ liệu (theo giờ chính xác); sắp xếp đúng trên toàn bộ 21 đơn qua các trang ở cả 2 cột và 2 chiều; dự phòng khi máy chủ bỏ qua `sapXep` hiện ghi chú và sắp xếp trong trang; ô chọn hình thức đổi nhãn nút và ghi chú; hóa đơn chưa thu/đã thu, K80/K58, 40 món.
- `code-reviewer` đợt 3: 0 nghiêm trọng, 4 nên sửa — đã sửa hết (lượt tải cũ làm sai cờ sắp xếp, trùng tên lớp `.pay-method` → `.pay-picker`, nhãn "Thu ngân" → "Nhân viên", tài liệu này).
- `ui-ux-reviewer` đợt 3: 0 nghiêm trọng, 1 nên sửa (không sắp xếp được Tổng tiền trên điện thoại) — đã sửa; gợi ý đã làm: ghi chú chuyển khoản chỉ nói "tự cập nhật" khi chính khách chọn chuyển khoản trên tablet, tiêu đề cột không xuống dòng. Đã sửa thêm: thông báo thanh toán có cảnh báo kho không còn che vùng thanh toán — sau 6 giây tự thu gọn còn 1 dòng "Cảnh báo kho: N nguyên liệu sắp hết · Xem" (bấm để mở lại; ở 375px cao 71px thay vì 115px). Nút Xem / Thu gọn cố định một chỗ, có `aria-expanded` + `aria-controls`, giữ tiêu điểm bàn phím.
- Chưa commit.

### 7.7. Kiểm tra với backend THẬT và in thật ra PDF (12/11/2026)

- Backend thật chạy được trên máy này với DB `QuanCaPheDB` ở instance mặc định: `$env:ConnectionStrings__QuanCaPheDB = "Server=.;Database=QuanCaPheDB;Trusted_Connection=True;TrustServerCertificate=True"` rồi `dotnet run` trong `CoffeeShop.Api` (không có `.\SQLEXPRESS`). Không sửa code backend.
- *(Các mục dưới đây về chế độ web tự sắp xếp / bộ nhớ đệm sắp xếp là nhật ký trước 23/11/2026 — chế độ đó đã bỏ, xem mục 7.3.)*
- Kết quả (10 đơn thật, 1 trang): backend nhận `tuNgay` chính xác đến mili-giây và bỏ qua `sapXep` (200, không lỗi). 24 giờ qua → 2 đơn đúng; 7/30 ngày → 10. Sắp xếp Mã đơn / Tổng tiền đúng cả 2 chiều (dữ liệu chỉ 1 trang nên sắp xếp trong trang = sắp xếp toàn bộ, không hiện ghi chú). Đã thử lại nhánh web tự sắp xếp toàn bộ với backend thật (request `limit=100` → 200, kết quả đúng) và với mock giả lập backend bỏ qua `sapXep` (21 đơn / 2 trang: thứ tự đúng qua cả 2 trang, chuyển trang ~10ms nhờ bộ nhớ đệm). Mục 5.3b vẫn nên làm để không phải tải hết danh sách khi dữ liệu lớn.
- In thật bằng bộ máy in của Chrome (Playwright `page.pdf`, `preferCSSPageSize`): hóa đơn thường → PDF **1 trang 80×97mm**; hóa đơn 40 món → **1 trang 80×842mm** (không bị chia A4). Chưa in trên máy in nhiệt thật — cần chủ quán in thử 1 tờ trên máy K80/K58 của quán (chỉnh `Receipt:PaperWidthMm` nếu dùng K58).

- **Backend thật nhiều trang (12/11/2026):** tạo bản sao `QuanCaPheDB_KiemThu` (BACKUP COPY_ONLY + RESTORE trên instance `.`), chạy backend thật trên bản sao, thêm 25 đơn thử qua `POST api/v1/don-hang` → 35 đơn / 2 trang. Kết quả: sắp xếp Mã đơn / Tổng tiền đúng qua cả 2 trang, cả 2 chiều, đủ 35 đơn (bấm sắp xếp 80–230ms, chuyển trang 4–17ms nhờ bộ nhớ đệm); kết hợp "24 giờ qua" → 27 đơn đúng. Đã xóa bản sao + file .bak; DB gốc không đổi (10 đơn).
- **Máy in:** máy này không cài máy in nhiệt (chỉ máy in ảo PDF/OneNote) nên chưa in được trên giấy thật. Việc còn lại: in thử 1 tờ trên máy in K80/K58 của quán (đổi `Receipt:PaperWidthMm` nếu là K58).

## 8. Cập nhật 13/11/2026: review phần chưa commit

- Đã trả `Api:UseMock` về `false` (phiên trước để quên `true`). Build: 0 lỗi, 0 cảnh báo.
- `code-reviewer` đợt 4 (trên toàn bộ phần chưa commit): 0 nghiêm trọng, 4 nên sửa, đã sửa hết:
  1. **Bộ nhớ đệm sắp xếp ở web ghi đè dữ liệu mới bằng dữ liệu cũ**: lượt tải toàn bộ đang chạy dở khi thu tiền / sửa đơn sẽ bỏ kết quả (`clientSortGeneration`). Nhánh 409 xóa bộ nhớ đệm trước khi tải lại.
  2. **Đơn đang xem bị cũ tới 60 giây** khi danh sách lấy từ bộ nhớ đệm: nay đơn đang xem luôn được hỏi lại máy chủ (`listFromCache`), nên vẫn đúng sau ≤ 3 giây (trước 25/11 là 10 giây).
  3. **Tạo đơn xong, khung chi tiết trống** (đang sắp xếp, đơn mới ở trang khác): xóa bộ nhớ đệm và mở chi tiết bằng bản máy chủ trả về.
  4. **Dòng cảnh báo kho bị mất khi có thông báo khác**: nay dòng cảnh báo (đã thu gọn) nằm lại dưới thông báo mới, chỉ mất khi bấm Đóng (`stockMessageActive`). Làm mới vẫn báo "Đã tải lại…" khi cảnh báo đang thu gọn.
- Gợi ý đã làm: một lô lỗi thì hủy ngay các lô còn lại; lỗi của request dò đơn mới không bị bỏ rơi; `<ul id="stock-warning-list">` luôn có trong DOM (dùng `hidden`) để `aria-controls` trỏ đúng.
- `code-reviewer` kiểm tra lại các bản sửa: đúng; sửa thêm 2 điểm: (a) lượt tải toàn bộ bị thao tác chen vào thì **tự tải lại** (tối đa 2 lần) thay vì bỏ, nên bảng không giữ dữ liệu cũ và lượt tải lại sau 409 không bị mất; (b) mở danh sách cảnh báo kho dưới một thông báo khác thì có tiêu đề "Cảnh báo kho: N nguyên liệu sắp hết" (`.stock-heading`), dòng cảnh báo đã thu gọn luôn viền màu cảnh báo. `app.css?v=57`.
- Gợi ý chưa làm (cố ý): mở lại cảnh báo kho bằng "Xem" thì không tự thu gọn lại (người dùng chủ động mở để đọc).
- Chưa chạy `ui-ux-reviewer` cho đợt sửa này (chỉ đổi logic thông báo, không đổi bố cục).

## 9. Cập nhật 14/11/2026: bỏ Nguồn

- **Quyết định của chủ quán:** bỏ cột **Nguồn** ở bảng danh sách và dòng **Nguồn** ở chi tiết đơn (yêu cầu gốc có cột này, nay đổi). Lý do: quán gia đình, tablet cắm điện thẳng nên ít hỏng, đơn tại quầy chỉ phát sinh khi khách không muốn hoặc không biết dùng tablet, nên cột gần như luôn là "Tablet". Chỉ chủ quán, nhân viên nhận món và nhân viên pha chế tạo đơn tại quầy, cùng đứng quầy, nên tên người tạo không giúp đối soát. Đối soát tiền theo hình thức thanh toán (két tiền mặt, sao kê chuyển khoản). Hủy đơn chỉ chủ quán làm được.
- **Vẫn giữ bên trong:** phân biệt đơn tablet (`MaNguoiDung` rỗng) để báo đơn mới và ghi chú chuyển khoản. Backend vẫn lưu tài khoản tạo đơn (`maNguoiDung`, `tenNhanVien` trong DTO), chỉ không hiển thị. Đã bỏ thuộc tính hiển thị `DonHang.Nguon`.
- **Nếu giảng viên hỏi** "nhân viên nghỉ thì ai làm": hệ thống phân quyền theo vai trò (Nhân viên / Quản lý), không theo người; nhân viên nào cũng làm được mọi việc trên trang Đơn hàng, chủ quán làm được tất cả. "Làm sao biết ai tạo đơn": dữ liệu vẫn được lưu, chỉ ẩn khỏi giao diện; mỗi người nên đăng nhập tài khoản riêng.
- CSS: các luật ẩn cột theo `nth-child` của `.orders-table` đã chỉnh cho bảng 6 cột (Thời gian tạo là cột 6). `app.css?v=58`.
- **Mở rộng 18/11/2026 (chủ quán):** web **không hiển thị tên người ở bất kỳ đâu**: người tạo đơn, **người thu tiền** (chi tiết đơn, hóa đơn K80) và **người thực hiện** ở lịch sử kho. Backend đã có `maNguoiThu`/`tenNguoiThu` (đơn) và `tenNguoiDung` (`lich-su-kho`): giữ nguyên phía backend, web không đưa vào model, không hiển thị. Từ backend mới chỉ dùng `thoiGianThanhToan` ("Thu lúc …", không kèm tên). Đã đính chính trong `hop-dong-api-bo-sung.md` (A6, C3, mục 12).


## 10. Cập nhật 16/11/2026: bỏ chữ giải thích trên toàn web admin

- **Quyết định của chủ quán:** bỏ hết các dòng chữ nhỏ giải thích (ghi chú dưới ô nhập, câu hướng dẫn trong hộp thoại, gợi ý ở chân bảng / thông báo). Giữ: lỗi, validation, trạng thái trống ngắn, dữ liệu, câu hỏi xác nhận chính.
- Trang Đơn hàng đã bỏ: ghi chú sắp xếp chỉ 2.000 đơn mới nhất (`sort-note`), "Đơn này không nằm trong danh sách…", dòng "máy chủ chưa hỗ trợ số nhận món" (nay ẩn cả khối), ghi chú chuyển khoản QR (kể cả nội dung CK "COFFEE {mã}"), hộp vàng "Khách đã chọn … khi đặt" khi đổi hình thức thanh toán (thay mục 7.4 — ô chọn Tiền mặt | Chuyển khoản vẫn giữ), ghi chú tổng tiền khi tạo đơn, phần ngoặc trong nhãn "Số nhận món" / "Topping thêm", câu gợi ý trong vài thông báo.
- Ô Chọn khoảng ngày (Đơn hàng + Lịch sử kho) chỉ hiện dòng dưới khi có lỗi.
- Trang Khuyến mãi: mã khuyến mãi mà món đang thuộc hiện thành nhãn xám cạnh tên món (dữ liệu, không phải câu giải thích) để tránh chuyển món âm thầm.
- Lớp CSS `.pay-picker` đã đổi tên thành `.choice-picker` (trang Đơn hàng vẫn dùng).

## 11. Cập nhật 23/11/2026: đơn chưa thu KHÔNG hiện hình thức thanh toán

- **Lỗi:** bảng danh sách ghi "Chưa thu" + "Tiền mặt" dưới nhãn, chi tiết ghi "Chưa thu (tiền mặt)" — như thể đã biết khách trả bằng gì. Phiên 15/11 đã phân tích và đề xuất nhưng **chưa từng sửa trong code** (chưa chốt hướng, không ghi vào file này), nên đợt đồng bộ backend sau đó không biết. Với backend thật lỗi lộ rõ vì backend tự điền `CASH` (`DatMon`: `req.PhuongThucThanhToan ?? "CASH"`, DB `DEFAULT 'CASH'`) → hầu hết đơn chưa thu đều hiện "Tiền mặt".
- **Nguyên tắc (giữ cố định):** `PhuongThucThanhToan` của đơn `PENDING` chỉ là lựa chọn lúc đặt (tablet còn đổi được QR ↔ tiền mặt) hoặc giá trị mặc định → **không hiển thị như sự thật**. Chỉ hiện hình thức khi `PhuongThucThanhToans.DaChot(d)`: đơn `PAID`, hoặc đã hủy có hoàn tiền.
- **Đã sửa:** `PhuongThucThanhToans.DaChot` + `Summary` (đơn chưa thu / hủy không thu → chỉ "Chưa thu" / "Không thu") trong `Models/DonHang.cs`; ô Thanh toán trên bảng `Orders.razor` dùng `DaChot`.
- **Vẫn giữ:** ô chọn Tiền mặt | Chuyển khoản lúc thu tiền, mặc định = lựa chọn của khách (gợi ý để bấm nhanh); hình thức chỉ ghi vào đơn khi bấm xác nhận (`POST thanh-toan`). Hóa đơn K80 chỉ in hình thức khi đã thu.
- **Cần báo nhóm backend (chưa gửi):** đơn không chọn hình thức nên lưu `null` thay vì tự điền `CASH`, để phân biệt "khách chọn tiền mặt" với "chưa chọn".
