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

- Nhánh `web-admin`. Commit `eb1d588` (phần đầu: số nhận món, trạng thái phục vụ, sửa đơn, API đề xuất, mock) và `aa66bbf` (làm lại giao diện, bước phục vụ tuần tự, đề xuất API v2.1, quy tắc CLAUDE.md, file này). **Chưa push, chưa mở PR.**
- `wwwroot/appsettings.json` đã commit với `UseMock: false`. Muốn demo bằng dữ liệu giả thì đổi tạm sang `true`, không commit.
- Chưa commit (cố ý): `.playwright-mcp/` (ảnh test, ở cả gốc repo lẫn `03-Web-Admin/`). 2 ảnh trong `docs/` (`Screenshot …png`, `1793106…jpg`) chưa rõ có nên commit không, cần hỏi người dùng.

## 3. Chức năng đã làm trên trang `/orders`

**Danh sách**
- Ô tìm kiếm chỉ nhận chữ số, khớp **mã đơn (tiền tố)** hoặc **số nhận món (chính xác)**. Trước đây tìm cả tên món/nhân viên không hiện trên bảng nên trông như "lọc sai".
- Cột: Mã đơn, Số nhận món, Trạng thái, Tổng tiền (căn phải), Thanh toán (nhãn + hình thức bên dưới), Nguồn, Thời gian tạo. Màn hình hẹp hoặc đang mở chi tiết: ẩn Nguồn/Thời gian. Điện thoại: ẩn thêm Tổng tiền, số tiền hiện dưới nhãn thanh toán.
- Bộ lọc trạng thái phục vụ + bộ lọc thanh toán. Màn hình ≤1000px gom vào nút "Bộ lọc (n)". "Xóa lọc" chỉ hiện khi đang lọc.
- **Đơn mới từ Tablet**: dò mỗi 10 giây trên **toàn bộ dữ liệu** (không phụ thuộc trang hay bộ lọc). Thông báo ở **đầu trang, màu tím**, có nút **Xem** (mở chi tiết, kể cả đơn không có trong trang đang xem). Dòng mới có vạch tím + chip "Mới" cho đến khi mở. Đơn tạo tại quầy **không** tính là đơn mới (cố ý).
- Thông báo kết quả thao tác nổi ở góc dưới trái (điện thoại: dưới cùng), thành công tự ẩn sau 6 giây. Thông báo có cảnh báo kho thì giữ lại.

**Màu (chủ quán chốt: mỗi màu một nghĩa, luôn kèm chữ)**

| Màu | Nghĩa |
|---|---|
| Tím | Đơn mới |
| Vàng | Chờ phục vụ, Chưa thu |
| Xanh dương | Đang chuẩn bị |
| Xanh lá | Đã phục vụ, Đã thu |
| Đỏ | Đã hủy, Đã hoàn tiền |
| Xám | Không thu |

Chuỗi và tông màu nằm một chỗ: `TrangThaiDon.Label`, `TrangThaiPhucVus.Label/Tone`, `PhuongThucThanhToans.ShortLabel/Tone/Summary` (`Models/DonHang.cs`). CSS: các class `.status-*` ở cuối `wwwroot/css/app.css`.

**Chi tiết đơn (khung bên phải)**
- Thứ tự: thông tin → **phiếu order** (số lượng ×, món, giá thẳng cột; topping có giá; ghi chú in nghiêng) → tổng → số nhận món + thanh tiến trình → thu tiền → In hóa đơn.
- **Số nhận món** (bảng gỗ 1–50): ô tròn như thiết kế cũ (chủ quán không thích kiểu "miếng gỗ"). Số đang được đơn chưa phục vụ xong giữ thì bị khóa (báo "Số 18 đang được dùng cho đơn #12"). Đã phục vụ hoặc đã hủy thì giải phóng nhưng vẫn lưu trên đơn.
- **Trạng thái phục vụ đi lần lượt, không nhảy cóc**: Chờ phục vụ → (nút xanh dương **Bắt đầu chuẩn bị**) → Đang chuẩn bị → (nút xanh lá **Xác nhận đã phục vụ**) → Đã phục vụ. Mock cũng chặn bỏ bước (409).
- Bắt đầu chuẩn bị khi chưa có số → hộp hỏi lại "Nhập số nhận món" / "Vẫn bắt đầu (khách mang đi)". Đã gõ số mà chưa bấm Lưu thì lưu luôn rồi bắt đầu.
- Phục vụ khi chưa thu tiền → hộp hỏi lại "Thu tiền trước" / "Vẫn xác nhận đã phục vụ".
- **Thu tiền**: nút cam chữ tối (chữ trắng trên cam không đủ tương phản), **không có ô tiền khách đưa** (chủ quán chọn đơn giản), có link đổi tiền mặt ↔ chuyển khoản.
- **Sửa đơn** (nút trên thanh công cụ): chỉ đơn chưa thu và chưa phục vụ. Dùng lại hộp thoại tạo đơn, gửi `PUT api/v1/don-hang/{id}`.
- **In hóa đơn**: chỉ in hóa đơn khổ 80mm qua iframe ẩn (`appHelpers.printReceipt` trong `wwwroot/js/app.js`, mẫu `#receipt-source`), không in cả trang.
- Bàn phím: đóng chi tiết thì focus về dòng vừa xem; đóng hộp Hủy đơn thì focus về nút Hủy đơn; bấm Xem thì focus vào tiêu đề chi tiết.

## 4. Backend: chưa hỗ trợ, đã gửi đề xuất

API **chưa có** ở `CoffeeShop.Api` (mã nguồn tại `C:\dev\CoffeeShop.Api`, chỉ đọc, không sửa):
`PUT don-hang/{id}`, `PUT don-hang/{id}/so-nhan-mon`, `PUT don-hang/{id}/trang-thai-phuc-vu`, lọc `trangThaiPhucVu`, trường `soNhanMon` / `trangThaiPhucVu`. Web chạy đủ ở mock. Với backend thật, nếu response không có `trangThaiPhucVu` thì trang tự ẩn phần phục vụ và báo "Máy chủ chưa hỗ trợ". Chi tiết xem `docs/de-xuat-api-don-hang.md`.

**Lỗi tranh chấp phía backend đã phân tích (chưa sửa, chưa ghi vào đề xuất):**
1. `sp_ThanhToanVaTruKho` kiểm tra PENDING và UPDATE tách rời. Hai người thu tiền cùng lúc → **trừ kho 2 lần**. Sửa: `UPDATE … WHERE TrangThaiThanhToan='PENDING'` + `@@ROWCOUNT`.
2. `DonHangService.ThanhToan` lưu `PhuongThucThanhToan` trước khi gọi SP → request bị từ chối vẫn ghi đè hình thức thanh toán. Sửa: truyền vào SP.
3. Không có kiểm tra tranh chấp (rowversion) → hủy và thu tiền cùng lúc mất cờ hoàn tiền; hai người đổi số của cùng một đơn thì ghi đè nhau. Sửa: cột `RowVersion` + 409 `DON_DA_THAY_DOI`.
Người dùng đã được hỏi có ghi phần này vào tài liệu đề xuất và xử lý 409 thống nhất phía web không, nhưng **chưa trả lời**.

## 5. Việc còn mở / cần hỏi người dùng

- [ ] Push nhánh `web-admin` và mở PR cho trưởng nhóm (chỉ làm khi người dùng yêu cầu).
- [ ] `CLAUDE.md` có 3 chỗ đã lỗi thời: dòng 21 ("backend has only payment statuses"), dòng 49 ("orders cannot be edited"), dòng 70 (`TrangThaiDon` "`Label`/`Color`", nay dùng `Tone`; `TrangThaiDon.Color` không còn ai gọi). Hỏi người dùng trước khi sửa.
- [ ] Thu tiền mặt một cú bấm, không hỏi lại, không hoàn tác: agent đề nghị thêm xác nhận hoặc hoàn tác vài giây. Chờ chủ quán quyết định.
- [ ] Có cho hủy đơn "Đã phục vụ" không? Có bắt buộc thu tiền trước khi phục vụ không? Chờ chủ quán.
- [ ] Trên điện thoại, thông báo nổi che nút In hóa đơn khoảng 6 giây (nhỏ).
- [ ] Chưa test dòng ghi chú món trên phiếu order (mock không có đơn có ghi chú).
- [ ] Đề xuất "Hoàn tác / Recall" sau khi bấm Đã phục vụ: cần backend cho đi lùi trạng thái.
- [ ] Nâng cấp sau: kết nối thời gian thực (SignalR) thay cho tự làm mới 10 giây. Ghi lại ai thu tiền / ai chuẩn bị.

## 6. Cách test

- Dữ liệu giả: `wwwroot/appsettings.json` → `Api:UseMock: true`, **xong phải trả `false`**. Mock tự sinh đơn tablet mỗi 60 giây, nút "Mô phỏng đơn từ Tablet" sinh ngay (đơn chuyển khoản tự sang "Đã thu").
- Dữ liệu mẫu: đơn #20 "Đang chuẩn bị" giữ số 18, #19 đã phục vụ số 18, #21 chờ phục vụ chưa có số.
- Chạy app: `dotnet run --launch-profile http` → http://localhost:5101 (tự đăng nhập `quanly` ở mock). **Kiểm tra cổng 5101 trước**: nếu đang có app (người dùng chạy từ Visual Studio) thì không tắt và hỏi trước khi chạy `ui-ux-reviewer`.
- Mở Claude **từ thư mục `03-Web-Admin`** để các agent `code-reviewer` / `ui-ux-reviewer` được nạp. Mở từ gốc repo thì dùng agent `general-purpose` và bảo nó đọc `.claude/agents/<tên>.md`.
- Mỗi lần sửa `app.css` / `app.js`: tăng `?v=` trong `wwwroot/index.html` (hiện `app.css?v=43`, `app.js?v=3`).
