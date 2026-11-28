# Kế hoạch sửa lỗi và hoàn thiện từng chức năng quản lý

> Lập: 28/11/2026, nhóm Web Admin. Cập nhật cùng ngày theo yêu cầu của chủ dự án: (1) được sửa luôn code backend để
> đồng bộ, (2) **chỉ sửa / hoàn thiện chức năng đang có, không thêm chức năng mới**, (3) rà soát code sạch + commit rõ nghĩa,
> (4) **xong mỗi đầu việc thì commit và push**, (5) **phát sinh vấn đề / buộc phải thêm luồng nghiệp vụ → dừng lại hỏi, không tự sửa** (mục 1.8).
> Căn cứ: bản rà soát toàn bộ web admin + đối chiếu mã `01-Backend-api` (mã B1–B14 lấy từ `docs/de-xuat-api-nghiep-vu-kho-bao-cao.md`).
>
> **Phiên Claude mới:** đọc file này trước. Xem **Bảng theo dõi** (mục 2) để biết module nào đang làm, chỉ làm module đó.
> Các quy tắc ở mục 1 và mục 3 **đã được chủ dự án duyệt sẵn** — làm theo, không cần hỏi lại.

---

## 1. Nguyên tắc (đã duyệt, không hỏi lại)

### 1.1. Một module một lúc ("khóa module")

1. Chỉ **một** module ở trạng thái `ĐANG LÀM` tại một thời điểm. Module sau chỉ mở khi module trước đạt **Tiêu chí hoàn thành** (mục 1.7).
2. **Mọi module làm thẳng trên nhánh `web-admin`** (không tạo nhánh riêng — người dùng quyết định 28/11/2026: chỉ một người làm web admin, các module làm lần lượt). Đóng module xong thì mở **một PR `web-admin` → `main`**. Ranh giới module ghi trong bảng theo dõi (cột "Commit cuối").
3. **Đóng băng phạm vi:** danh sách việc của module chốt lúc mở module. Ý tưởng mới phát sinh giữa chừng → ghi vào **Sổ việc phát sinh** (mục 13), không làm ngay.

### 1.2. Chỉ sửa cái đang có — không tạo chức năng mới

- Đợt này chỉ làm 2 loại việc: **(a) sửa logic sai** của chức năng đang có, **(b) hoàn thiện phần còn dở** của chức năng đang có (đã có nút / màn hình / API nhưng chạy chưa đúng hoặc chưa đủ).
- **Không** thêm màn hình, nút, tab, API, báo cáo, cơ chế mới mà chủ quán chưa yêu cầu. Danh sách các ý tưởng đã bị loại khỏi đợt này ở **mục 12**.
- Ý tưởng hay thêm **không cần** cho đầu việc đang làm → ghi vào Sổ việc phát sinh, không làm.
- Phân vân một việc là "sửa" hay "thêm mới", hoặc **muốn làm xong đầu việc thì buộc phải thêm luồng / nút / API / luật mới** → coi là thêm mới, **dừng và hỏi ngay** theo 1.8.
- Sửa luật nghiệp vụ (ví dụ: thời điểm trừ kho) phải theo câu trả lời của chủ quán ở mục 4.1, không tự quyết.

### 1.3. Gặp lỗi của module khác

| Loại lỗi | Cách xử lý |
|---|---|
| Làm **sai tiền / sai kho / mất dữ liệu**, hoặc **chặn** việc của module đang làm | **Dừng, báo và hỏi** theo 1.8. Chỉ sửa khi được đồng ý; khi sửa thì **commit riêng** (`<module-kia>(...): ...`) và thêm vào kịch bản kiểm thử của module kia |
| Lỗi giao diện, câu chữ, tiện ích của module khác | Ghi vào Sổ việc phát sinh, **không sửa** |
| Lỗi thuộc module **đã đóng** | Mở lại module đó **sau khi** đóng module hiện tại |

### 1.4. Đồng bộ backend: sửa luôn trong `01-Backend-api`

Máy này có sẵn project backend (`C:\dev\Coffeshop\01-Backend-api`, build được). Việc nào cần backend thì **nhóm web sửa luôn**, không chờ:

- Chỉ sửa trong `01-Backend-api/` (không dùng thư mục `C:\dev\Coffeshop\CoffeeShop.Api` — chỉ còn `bin/obj` cũ).
- Theo đúng quy ước của backend (`01-Backend-api/CLAUDE.md`): **`openapi/openapi.yaml` trước** → `Dtos/Dtos.cs` → Service + Controller → mẫu trong `CoffeeShop.Api.http`. Đổi CSDL: sửa `database/QuanCaPheDB.sql` **và** viết script vá riêng cho DB đang chạy (đặt tên `database/<NoiDung>_<yyyyMMdd>.sql`, chạy lại nhiều lần không hỏng), rồi sửa `Data/Entities.cs` / `AppDbContext`.
- Sửa backend xong thì sửa luôn `Models/` + `Services/` của web trong **cùng module** — không để một phía xong một phía dở.
- Commit backend **tách riêng** commit web (tiền tố `api`, xem 1.6). Mô tả PR có mục "Thay đổi backend" liệt kê route / DTO / script SQL để nhóm backend duyệt và gộp vào nhánh `API-HIHI`.
- Backend vẫn là nơi giữ luật nghiệp vụ: luật nào cũng phải chặn ở **backend**; web chỉ hiển thị và chặn sớm cho tiện.

### 1.5. Rà soát code và build sạch (mỗi đầu việc)

Tiêu chí: **code đơn giản, dễ hiểu, logic rõ, không có code rác.** Mỗi đầu việc, trước khi commit:

1. **Build sạch cả 2 phía** đã chạm: `dotnet build` ở `03-Web-Admin` và/hoặc `01-Backend-api` → **0 lỗi, 0 cảnh báo**.
2. **Tự rà phần vừa sửa** theo danh sách:
   - Mỗi hàm làm **một việc**, tên nói đúng việc đó. Hàm dài > ~40 dòng hoặc lồng > 3 tầng `if` → tách.
   - **Không** để lại code chết: biến / hàm / nhánh không ai gọi, nhánh "máy chủ chưa hỗ trợ" cho route backend đã có, code bị comment-out, `using` thừa.
   - **Không lặp**: logic giống nhau ở 2 nơi → đưa về một chỗ (ví dụ `Models/` hoặc `Utilities/`).
   - Chuỗi trạng thái / vai trò chỉ lấy từ lớp hằng (`TrangThaiDon`, `Roles`…), không viết chữ trực tiếp.
   - Mỗi lớp / hàm public mới có **1 dòng chú thích tiếng Việt** nói nó để làm gì; chỉ chú thích *vì sao* ở chỗ logic không tự giải thích được. Không chú thích lặp lại điều code đã nói rõ.
   - Không thêm thư viện, lớp trừu tượng, cấu hình khi chưa cần.
3. Chạy `code-reviewer` (và `ui-ux-reviewer` nếu chạm giao diện) theo quy trình bắt buộc trong `CLAUDE.md`; sửa hết lỗi được xác nhận, **build lại** cho sạch.
4. Mỗi module có thêm một đầu việc **"Rà soát code của module"**: đọc lại toàn bộ file trong phạm vi module (cả phần không sửa), dọn code chết / trùng / khó hiểu đã liệt kê sẵn trong bảng của module.

### 1.6. Quy tắc commit

- **Mỗi đầu việc (một dòng trong bảng) = ít nhất một commit**, làm xong, build sạch, đúng yêu cầu thì **commit và push ngay** (đã được duyệt, không hỏi lại). Không gom nhiều đầu việc vào một commit; không trộn web và backend trong một commit.
- Mẫu:

  ```
  <module>(<web|api>): <động từ> <việc cụ thể>        ← dòng tiêu đề ≤ 72 ký tự

  Mục đích: <vì sao cần sửa — lỗi gì / thiếu gì, hậu quả cho quán>
  Thay đổi:
  - <file/lớp/hàm>: <thêm / sửa / xóa gì, khai báo gì mới>
  - ...
  Kiểm tra: <build sạch; kịch bản / test đã chạy>
  Mã việc: <ví dụ 5C.1, B4>

  Co-Authored-By: ...
  ```

- Tên module dùng trong tiêu đề: `nen-tang`, `tai-khoan`, `thuc-don`, `kho`, `khuyen-mai`, `don-hang`, `bao-cao`; việc chung nhiều module: `giao-dien` (chỉ `app.css` / `index.html`), `tai-lieu` (chỉ tài liệu).
- Ví dụ tốt: `don-hang(api): chặn thu tiền 2 lần trong sp_ThanhToanVaTruKho bằng UPDATE có điều kiện`.
- **Cấm** tiêu đề mơ hồ: "cập nhật", "sửa lỗi", "fix", "update code", "chỉnh sửa", "hoàn thiện", "tối ưu" đứng một mình không nói sửa gì, ở đâu.
- **Push sau mỗi đầu việc** lên `web-admin` (`git push origin web-admin`), không hỏi lại. **Không bao giờ** push lên `main` / `API-HIHI`, không `--force`, không bỏ qua hook.
- Mở PR `web-admin` → `main` **khi đóng module** (đạt 1.7), mô tả có mục "Thay đổi backend". Không tự merge PR.
- **Đồng bộ với `main`:** trước khi mở module mới và trước khi sửa backend, xem `main` có commit mới không (`git fetch`). Có thì **rà từng commit** (thêm chức năng gì, để làm gì, có đụng file đang sửa không): commit đã qua PR, không xung đột → gộp vào `web-admin`; commit xung đột hoặc có vấn đề → **tạm không gộp**, báo và hỏi người dùng (1.8).
- Push bị từ chối (xung đột, hook chặn, mất mạng) → không ép, dừng và báo theo 1.8.

### 1.7. Tiêu chí hoàn thành chung (mọi module)

- [ ] Mọi đầu việc trong bảng của module đã Xong (hoặc Hoãn có biên bản của chủ quán).
- [ ] Web và backend: `dotnet build` **0 lỗi, 0 cảnh báo**; test tự động của module chạy qua hết.
- [ ] Đã làm đầu việc **"Rà soát code của module"** (1.5 mục 4).
- [ ] Chạy hết **kịch bản kiểm thử tay** của module với backend thật (đã chạy bản vừa sửa), 3 khổ màn hình (375px, 768px, máy tính), 2 vai trò. Console trình duyệt không lỗi.
- [ ] `code-reviewer` / `ui-ux-reviewer`: 0 lỗi nghiêm trọng, lỗi "nên sửa" đã sửa hoặc ghi lý do.
- [ ] Chạy lại **kịch bản khói** (mục 11) → không hỏng gì của module đã đóng.
- [ ] Tài liệu đã cập nhật: `CLAUDE.md` (web), `01-Backend-api/openapi/openapi.yaml` + README nếu đổi API, `docs/huong-dan/*`, bảng theo dõi trong file này.
- [ ] Mọi commit theo mẫu 1.6 và **đã push**; PR đã mở, có mục "Thay đổi backend".
- [ ] Mọi vấn đề phát sinh trong module (1.8) đã có câu trả lời và được ghi vào Sổ việc phát sinh.

### 1.8. Phát sinh vấn đề → dừng lại hỏi, không tự sửa

**Phải dừng và hỏi người dùng** (không tự quyết, không tự sửa) khi gặp bất kỳ trường hợp nào:

- Muốn làm xong đầu việc thì **buộc phải thêm luồng nghiệp vụ** (bước mới, trạng thái mới, nút / màn hình / API / bảng / cột mới, luật mới).
- Code thật **khác** mô tả trong kế hoạch (lỗi không tồn tại, nguyên nhân khác, cách sửa trong kế hoạch không dùng được).
- Cách sửa làm **thay đổi hành vi người dùng đang quen** ngoài phạm vi đầu việc, hoặc ảnh hưởng tablet (`02-Flutter-Tablet`) / dữ liệu đang có trong DB thật.
- Phát hiện lỗi sai tiền / sai kho / mất dữ liệu ở module khác (1.3).
- Câu trả lời của chủ quán (4.1) chưa có, mâu thuẫn, hoặc không đủ để làm.
- Build / test / push không qua được mà không rõ nguyên nhân, hoặc phải đổi cấu hình, thư viện, hợp đồng API ngoài dự kiến.

**Cách hỏi:** một câu hỏi ngắn kèm (1) vấn đề gặp ở đâu (file / dòng / màn hình), (2) ảnh hưởng tới quán, (3) 2–3 phương án và phương án đề xuất. Ghi câu hỏi + câu trả lời vào **Sổ việc phát sinh** (mục 13).

**Trong lúc chờ:** đầu việc đó ở trạng thái "chờ trả lời", **không làm tạm**. Được làm tiếp các đầu việc khác **cùng module** không phụ thuộc vào câu trả lời; không mở module mới.

---

## 2. Bảng theo dõi

| Thứ tự | Module | Phụ thuộc | Cỡ | Trạng thái | Commit cuối / PR |
|---|---|---|---|---|---|
| 0 | Nền tảng: dọn việc dở, test, chốt câu hỏi, múi giờ | — | M | XONG việc 0.1–0.6 (28/11); kịch bản khói trên giao diện đã chạy (chưa thử bước Thêm món, Xuất Excel); **chờ PR** (vướng 5 commit "cap nhat" của `main` — Sổ việc phát sinh) | commit cuối `e638e83` |
| 1 | Đăng nhập & Tài khoản | 0 | S | XONG việc 1.1–1.4 (28/11), kịch bản tay đã chạy; **chờ PR** cùng Giai đoạn 0 | commit cuối `995b31e` |
| 2 | Thực đơn: Món · Danh mục · Topping · Công thức | 0 | M | CHƯA LÀM | |
| 3 | Kho nguyên liệu | 2 | S | CHƯA LÀM | |
| 4 | Khuyến mãi | 2 | S | CHƯA LÀM | |
| 5 | Đơn hàng | 2, 3, 4 | L | CHƯA LÀM | |
| 6 | Tổng quan & Báo cáo | 5 | M | CHƯA LÀM | |
| 7 | Hồi quy toàn hệ thống & bàn giao | 1–6 | S | CHƯA LÀM | |

Trạng thái: `CHƯA LÀM` · `ĐANG LÀM` · `TẠM DỪNG (biên bản)` · `XONG`.

```
Tài khoản (độc lập, nhỏ — chạy thử quy trình)
Thực đơn ──► Kho ──┐
    └─────► Khuyến mãi ──► Đơn hàng ──► Báo cáo
```

- **Thực đơn trước Kho:** công thức nối món ↔ nguyên liệu; công thức sai thì số kho sai.
- **Kho, Khuyến mãi trước Đơn hàng:** đơn hàng dùng giá sau khuyến mãi và trừ kho.
- **Đơn hàng trước Báo cáo:** báo cáo chỉ đọc dữ liệu đơn.
- Đồng bộ thời gian thực (SignalR) là **cơ chế mới** → không thuộc đợt này (mục 12).

---

## 3. Quy trình cho MỖI đầu việc (đã duyệt, không hỏi lại)

1. **Hiểu đúng yêu cầu:** đọc lại đầu việc + câu trả lời của chủ quán (mục 4.1), đọc code thật liên quan. Xác nhận đây là **sửa / hoàn thiện**, không phải thêm mới (1.2). Ghi ra (trong đầu việc hoặc mô tả commit) **logic đang thiếu / sai cụ thể là gì** và **kết quả đúng phải như thế nào**. Code khác mô tả hoặc cần luồng mới → dừng, hỏi (1.8).
2. Gọi skill liên quan (quy trình bắt buộc trong `CLAUDE.md`).
3. Nếu cần backend: sửa `01-Backend-api` theo 1.4 → build sạch → thử bằng `CoffeeShop.Api.http` / Swagger.
4. Sửa web (`Models/` → `Services/` → trang) → build sạch.
5. Tự rà code theo 1.5; chạy agent review; sửa lỗi được xác nhận → build lại sạch.
6. **Đối chiếu yêu cầu:** chạy kịch bản kiểm thử của đầu việc với backend thật (cả trường hợp lỗi / biên, cả gọi thẳng API khi luật nằm ở backend). Kiểm tra từng ý ở bước 1: logic thiếu đã bù đủ, kết quả đúng như mô tả, không làm hỏng hành vi khác. Chưa đạt → quay lại bước 3, không commit.
7. **Commit** (backend và web tách riêng, theo mẫu 1.6) → **push** lên `web-admin`.
8. Đánh dấu Xong trong bảng của module (kèm mã commit nếu tiện).

---

## 4. Giai đoạn 0 — Nền tảng (trước mọi module)

| Mã | Việc | Phía | Ghi chú |
|---|---|---|---|
| 0.1 | Rà ~30 file đang thay đổi chưa commit, build, chạy thử, **commit theo từng nhóm ý nghĩa** (phiên đăng nhập, Excel, tài liệu…) theo mẫu 1.6 | Web | Xong 28/11: 9 commit `aeb7bbe`…`e8c4a44` + gộp phần `main` đã qua PR (`788bc8d`). 5 commit "cap nhat" của `main` chưa gộp — xem Sổ việc phát sinh |
| 0.2 | Tạo project `CoffeeShop.Admin.Tests` cho logic thuần đang có: `DatePresets`, `SearchText`, `ColumnSort`, `DocSoTien`, `TokenClaims`, các lớp trạng thái, `XlsxWriter` | Web | Xong 28/11: `tests/CoffeeShop.Admin.Tests`, 84 test, `f17a565` |
| 0.3 | Viết **kịch bản khói** (mục 11) + script SQL **dữ liệu mẫu** cố định (món, topping, công thức, nguyên liệu, khuyến mãi, ~30 đơn đủ trạng thái), chạy trên DB bản sao | Web | Xong 28/11: `tests/du-lieu-mau/*.ps1` + `docs/kiem-thu/kich-ban-khoi.md` (đã chạy: 30 đơn đúng 8 nhóm) |
| 0.4 | Hỏi chủ quán **một lần** các câu ở 4.1, ghi câu trả lời vào bảng | Chủ quán | Xong 28/11 (mục 4.1); còn P1–P3 ở mục 4.2 — cần trước Module 2, 4, 5 |
| 0.5 | **Múi giờ (B6):** backend cố định giờ Việt Nam cho mọi mốc thời gian (`DateTime.Now`, `GETDATE()`), ghi rõ vào `openapi.yaml`; web gửi / đọc giờ theo cùng quy ước | API + Web | Xong 28/11: `deb753a` (api) + commit web kế tiếp — `GioViet` hai phía, script `GioVietNam_20261128.sql` |
| 0.6 | Sửa 3 chỗ lỗi thời trong `CLAUDE.md` web (mục 5 của `tien-do-quan-ly-don-hang.md`) | Web | Xong 28/11 |

### 4.1. Câu hỏi cần chủ quán chốt (chỉ hỏi về luật của chức năng đang có)

| # | Câu hỏi | Dùng cho | Trả lời |
|---|---|---|---|
| Q1 | Món / topping **chưa có công thức**: vẫn cho bán (không trừ kho) hay không cho bán? | 2.3, 2.8, 2.9 | **28/11:** công thức phải nhập **ngay trong form Thêm món** (mỗi size một công thức) mới thêm được món; form **Sửa** cũng sửa được công thức từng size; nút **Công thức** chỉ còn **xem** (không thêm / sửa), hiện lần lượt công thức của mọi size của món đang chọn, **bỏ ô sổ xuống chọn món khác**. Còn hỏi: topping có áp dụng giống vậy không (P1, mục 4.2) |
| Q2 | Một món chỉ thuộc **một** khuyến mãi (như hiện tại) — giữ nguyên? | 4.1, 4.2 | **28/11:** **bỏ giờ vàng**, khuyến mãi chỉ theo ngày (ví dụ 20/10, 11/11); **1 ngày tối đa 1 khuyến mãi**; thêm khuyến mãi thì chọn các món được giảm. Còn hỏi: một món có được nằm trong nhiều khuyến mãi khác ngày không (P2, mục 4.2) |
| Q3 | Kho trừ **lúc nào**: lúc bắt đầu pha (Đang chuẩn bị), lúc Đã phục vụ, hay lúc thu tiền (như hiện tại)? | 5C.4 | **28/11: lúc bắt đầu pha** (bấm Đang chuẩn bị); thu tiền không trừ nữa |
| Q4 | Hủy đơn **đã thu nhưng chưa pha**: có trả lại nguyên liệu vào kho không? | 5C.5 | **28/11: tự trả lại kho.** Theo Q3 thì đơn chưa pha chưa bị trừ kho, nên chỉ còn áp dụng cho đơn đã bị trừ theo luật cũ (thu tiền trước khi pha) lúc chuyển đổi |
| Q5 | Có cho **hủy đơn đã phục vụ** không? Có **bắt buộc thu tiền trước khi phục vụ** không? | 5C.8 | **28/11: bắt buộc thu tiền trước khi bấm Đã phục vụ; đơn đã phục vụ không hủy được** |
| Q6 | Nút Thu tiền hiện bấm một lần là xong: có cần **hỏi lại xác nhận** trước khi ghi không? | 5C.9 | **28/11: thêm hộp xác nhận thu tiền.** Hướng xác nhận chuyển khoản (tự động hay bấm tay) — xem P3, mục 4.2 |
| Q7 | Báo cáo: tiền hoàn trừ vào **ngày hoàn tiền** (số của ngày đã chốt không đổi về sau) — đồng ý? | 6.1 | **28/11: trừ vào ngày hoàn** |
| Q8 | Giờ cao điểm tính theo giờ **khách đặt** hay giờ **thu tiền** (như hiện tại)? | 6.3 | **28/11: giữ giờ thu tiền** (6.3 không làm) |

### 4.2. Câu hỏi còn mở sau lượt trả lời 28/11

| # | Câu hỏi | Dùng cho | Đề xuất | Trả lời |
|---|---|---|---|---|
| P1 | Topping cũng bắt buộc nhập công thức trong form Thêm / Sửa topping và nút Công thức chỉ xem — giống món? | 2.8, 2.9 | Có, để cùng một luật (topping cũng trừ kho) | **28/11: Không.** Topping là nguyên liệu mua sẵn / nhập ngoài: chỉ quản lý **định lượng** (mỗi phần topping thêm vào món dùng bao nhiêu nguyên liệu) như hiện tại ở nút Công thức của tab Topping (vẫn thêm / sửa được); form Thêm / Sửa topping không có phần công thức |
| P2 | CSDL hiện gắn khuyến mãi **ngay trên món** (`TenMon.MaKhuyenMai`, mỗi món 1 ô) → món chỉ nằm trong **một** khuyến mãi tại một thời điểm. Tạo trước KM 11/11 có Cà phê sữa thì Cà phê sữa bị rút khỏi KM 20/10 chưa diễn ra. Chọn: (a) giữ CSDL, chặn thêm món đang thuộc KM **chưa kết thúc** khác; (b) đổi sang bảng nối khuyến mãi – món (một món nằm được trong nhiều KM khác ngày) | 4.2 | (b) đúng với cách quán dùng KM theo ngày; (a) ít sửa hơn nhưng phải tạo KM lần lượt | **28/11: chọn (b)** — bảng nối khuyến mãi – món (việc 4.7) |
| P3 | Xác nhận chuyển khoản: tự động qua ngân hàng hay nhân viên bấm tay? (phân tích ở mục 4.3) | 5C.9 | Bấm tay có hộp xác nhận; tự động để đợt sau | **28/11:** nhân viên bấm xác nhận cho **cả hai** hình thức (có hộp hỏi lại); **thiết kế lại** khối nút xác nhận thanh toán (hiện xấu, màu nhấn bị nhòe, lẫn với khung chi tiết; các nút xanh đậm không cân đối) — gọi skill thiết kế (việc 5C.13) |

### 4.3. Phân tích: xác nhận tiền mặt / chuyển khoản (Q6)

- **Hiện có:** tablet cho khách chọn QR hoặc tiền mặt (đổi được khi đơn còn chờ qua `PUT don-hang/{id}/phuong-thuc-thanh-toan`), tablet hỏi `GET don-hang/{id}/trang-thai` để biết đã thanh toán. Web có ô chọn Tiền mặt | Chuyển khoản + một nút xác nhận (nhãn đổi theo lựa chọn). **Chưa có** kết nối ngân hàng: hệ thống không tự biết tiền chuyển khoản đã vào tài khoản.
- **Tự động xác nhận chuyển khoản** cần dịch vụ trung gian đọc giao dịch ngân hàng và gọi về backend (webhook — ví dụ các dịch vụ VietQR / cổng thanh toán), kèm mã đơn trong nội dung chuyển khoản, API nhận webhook có xác thực, xử lý chuyển thiếu / thừa / sai nội dung. Đây là **chức năng mới**, cần tài khoản dịch vụ và chi phí → không thuộc đợt sửa lỗi này (mục 12).
- **Đề xuất cho đợt này:** giữ **một khối thanh toán** với ô chọn hình thức (mặc định = lựa chọn của khách), bấm xác nhận thì **mở hộp xác nhận**: tiền mặt → "Xác nhận đã thu X đ tiền mặt?"; chuyển khoản → "Đã kiểm tra tài khoản nhận được X đ?". Không tách 2 nút riêng vì ô chọn đã làm việc đó và tránh bấm nhầm nút. Tablet vẫn tự thấy "đã thanh toán" ngay sau khi nhân viên xác nhận.
- **Đợt sau (nếu chủ quán muốn):** tích hợp webhook ngân hàng → đơn QR tự chuyển PAID, nút xác nhận chuyển khoản chỉ còn dùng khi webhook lỗi.

**DoD Giai đoạn 0:** cây làm việc sạch; project test chạy được; kịch bản khói + dữ liệu mẫu có sẵn; Q1–Q8 đã trả lời; múi giờ đã sửa và kiểm tra; build sạch cả 2 phía.

---

## 5. Module 1 — Đăng nhập & Tài khoản

**Phạm vi:** web `Pages/Authentication/*`, `Pages/Accounts/Accounts.razor`, `Services/Auth/*`, `AuthenticationService`, `AccountService`, `Models/NguoiDung.cs`, `Layout/MainLayout.razor`; backend `AuthController`, `NguoiDungController`, `Common/Security.cs`.

| Mã | Việc | Phía | Ghi chú |
|---|---|---|---|
| 1.1 | Chặn tự hạ quyền; luôn còn ≥ 1 Quản lý | Web | **Đối chiếu 28/11:** backend đã chặn từ trước (409 tự khóa / tự hạ quyền; xóa Quản lý cuối). Còn thiếu ở web: ô Vai trò vẫn chọn được khi sửa chính mình → đã khóa ô (`editingSelf`) |
| 1.2 | **B8:** token cũ mất hiệu lực khi đặt lại / đổi mật khẩu | API | **Đối chiếu 28/11:** xóa / khóa / đổi vai trò đã làm token 401 từ trước (`OnTokenValidated`). Còn thiếu đổi mật khẩu → claim `pv` (dấu vân mật khẩu) trong JWT, so với mật khẩu hiện tại; không đổi CSDL |
| 1.3 | Test tự động: `TokenClaims` (đã có ở 0.2), `Roles.AfterLogin` (tách khỏi `Login.razor.cs` để test được) | Web | |
| 1.4 | **Rà soát code của module:** bỏ `AuthenticationService.GetMyAccountAsync` / `MessageFor` / `IsLoggedIn` / phụ thuộc `AppState` và lớp `AuthResult<T>` (gọi API trùng `ApiClient`); tài khoản đang đăng nhập lấy qua `AccountService.GetMeAsync`; sửa chú thích `Roles.Staff` lỗi thời | Web | |

**Kịch bản kiểm thử tay:** đăng nhập đúng / sai / tài khoản bị khóa; nhân viên mở thẳng `/products` → "Không có quyền"; F5 giữ phiên, đóng tab mất phiên; token hết hạn → về `/login` giữ `returnUrl`; tạo nhân viên → đăng nhập được; đặt lại mật khẩu cho nhân viên đang đăng nhập ở máy khác → máy đó bị đăng xuất ở lần gọi kế tiếp; thử tự hạ quyền / hạ quyền Quản lý cuối cùng → bị chặn.

---

## 6. Module 2 — Thực đơn (Món · Danh mục · Topping · Công thức)

**Phạm vi:** web `Pages/Products/*`, `ProductService`, `CategoryService`, `ToppingService`, `Models/Menu.cs`; backend `MenuService`, `Common/Mapping.cs` (`GiaSauGiam`, `DuNguyenLieu`, `coTheBan`), controller món / topping / danh mục / công thức.

| Mã | Việc | Phía | Ghi chú |
|---|---|---|---|
| 2.1 | **B9:** API món / topping trả `coCongThuc` cho từng size và từng topping | API | Hoàn thiện cảnh báo công thức trống đã có trên web |
| 2.2 | Cột trạng thái món / topping hiện nhãn **"Chưa có công thức"** dựa trên 2.1 (hiện web chỉ biết khi mở hộp Công thức) | Web | Dùng nhãn trong cột đang có, không thêm cột / bộ lọc mới |
| 2.3 | Theo Q1: **món** mới luôn có công thức (2.8); backend chặn thêm món / thêm size không kèm công thức. Món cũ chưa có công thức: nhãn "Chưa có công thức" (2.2). Topping (P1): không bắt buộc; topping chưa có định lượng hiện nhãn "Chưa có định lượng" | API + Web | |
| 2.4 | **B14:** lưu công thức có nguyên liệu đã ngừng dùng → backend trả 422; web hiện lỗi ngay cạnh dòng (hiện web chỉ cảnh báo) | API + Web | |
| 2.5 | Rà các trường hợp **Xóa** bị chặn: danh mục còn món, món / topping đang trong đơn chưa xong, nguyên liệu đang trong công thức → backend chặn đúng, web `DeleteBlockedReason` nói đúng lý do | API + Web | |
| 2.6 | Test tự động: `TrangThaiMon.Of`, validate form giá theo size | Web | |
| 2.7 | **Rà soát code của module:** `Products.razor.cs` (778 dòng, 3 tab chung một lớp) — tách logic từng tab nếu đọc khó; xóa `Utilities/ModelCopy.cs` (không còn nơi nào dùng); backend: kiểm tra mọi `ToDto` có đủ `Include` (thiếu thì giá / tình trạng sai âm thầm) | Web + API | |
| 2.8 | **Q1:** form **Thêm món** có phần công thức cho **từng size** đã chọn giá (bắt buộc ≥ 1 dòng nguyên liệu mỗi size, không đủ thì không lưu); form **Sửa món** sửa được công thức từng size. Backend nhận món + giá + công thức trong một lần lưu (một transaction) để không có món "nửa vời" | API + Web | Sửa luồng Thêm / Sửa đang có |
| 2.9 | **Q1:** nút **Công thức** chỉ còn **xem**: hiện lần lượt công thức của mọi size của món đang chọn, không có nút thêm / sửa / lưu, **bỏ ô sổ xuống chọn món khác** | Web | Sửa hộp Công thức đang có |

**Kịch bản kiểm thử tay:** thêm món 3 size + công thức → hộp tạo đơn tại quầy hiện đúng giá; sửa giá → đơn cũ giữ giá cũ; ngừng bán / tạm hết → không đặt được, vẫn hiện trên bảng; món chưa có công thức → có nhãn và xử lý đúng theo Q1; công thức có nguyên liệu ngừng dùng → bị chặn; xóa danh mục còn món → bị chặn, nói rõ lý do.

---

## 7. Module 3 — Kho nguyên liệu

**Phạm vi:** web `Pages/Inventory/Inventory.razor`, `InventoryService`, `Models/Kho.cs`; backend `KhoService`, controller nguyên liệu / lịch sử kho, bảng `NguyenLieu`, `LichSuKho`.

| Mã | Việc | Phía | Ghi chú |
|---|---|---|---|
| 3.1 | **B13:** `xuat-huy` kiểm tra tồn rồi mới ghi, không khóa dòng → đổi thành cập nhật có điều kiện `SoLuongTon >= @soLuong`, không đủ → 422 | API | |
| 3.2 | **B3 (phần kho):** chặn tồn âm ở CSDL (`CHECK (SoLuongTon >= 0)` + script vá; trước khi thêm CHECK, báo các dòng đang âm để chủ quán nhập kho bù) | API + script SQL | Phần "đặt món khi giờ đông" làm ở 5C.7 |
| 3.3 | Sửa / thêm nguyên liệu: backend kiểm tra mức cảnh báo, tồn ban đầu không âm (web đã chặn, backend phải chặn được khi gọi thẳng API) | API | |
| 3.4 | Đối soát: script SQL so `SoLuongTon` với tổng `LichSuKho` từng nguyên liệu (phải khớp) → đưa vào kịch bản khói | Web (script kiểm thử) | Công cụ kiểm thử, không phải chức năng |
| 3.5 | Test tự động: `TinhTrangKho.Of`, `CanNhap`, validate form nhập / xuất | Web | |
| 3.6 | **Rà soát code của module:** `Inventory.razor` (1.159 dòng, nguyên liệu + lịch sử + hộp nhập/xuất trong một file) → tách ra `.razor.cs` / component nếu khó đọc; xem `KhoService` phần dự báo cạn kho có ai dùng không | Web + API | |

**Kịch bản kiểm thử tay:** nhập 1000 → tồn +1000, có dòng lịch sử; xuất hủy quá tồn → bị chặn (kể cả 2 máy bấm cùng lúc); tồn dưới mức cảnh báo → nhãn "Sắp hết"; ngừng dùng → vẫn hiện, không chọn được trong công thức mới; **đối soát SQL khớp 100%**.

---

## 8. Module 4 — Khuyến mãi

**Phạm vi:** web `Pages/Promotions/Promotions.razor`, `PromotionService`, `Models/KhuyenMai.cs`; backend controller khuyến mãi, `Mapping.GiaSauGiam`.

| Mã | Việc | Phía | Ghi chú |
|---|---|---|---|
| 4.1 | Theo Q2: **bỏ giờ vàng** — web bỏ 2 ô giờ trong form, cột Giờ vàng, `OutsideGoldenHours`, ghi chú "Ngoài giờ vàng lúc này"; backend bỏ kiểm tra / áp khung giờ trong `GiaSauGiam`, không nhận `gioBatDau` / `gioKetThuc` nữa (cập nhật `openapi.yaml`), script vá đặt các giờ đang có về NULL | API + Web + script SQL | Báo nhóm tablet nếu tablet đọc 2 trường giờ |
| 4.2 | Theo Q2: **1 ngày tối đa 1 khuyến mãi** — backend chặn khoảng ngày chồng lên khuyến mãi khác (409, câu nói rõ trùng với mã nào); web hiện lỗi ngay ở ô ngày | API + Web | |
| 4.3 | Backend kiểm tra hợp lệ giống web: mã trùng, % ngoài 0,01–100, ngày kết thúc trước ngày bắt đầu | API | Web đã chặn; backend phải chặn khi gọi thẳng API |
| 4.4 | Xóa khuyến mãi đang áp dụng: câu hỏi xác nhận nói rõ các món về giá gốc ngay | Web | Sửa câu của hộp xác nhận đang có |
| 4.5 | Test tự động: `TrangThaiKhuyenMai.Of`, `KhuyenMaiForm.Validate` (ngày kết thúc trước ngày bắt đầu) | Web | |
| 4.6 | **Rà soát code của module:** logic "đang áp dụng" có ở cả web (`TrangThaiKhuyenMai`) lẫn backend (`DangApDung`) → web dùng `dangApDung` của backend làm chuẩn nếu được, tránh 2 nơi tính lệch nhau | Web + API | |
| 4.7 | **P2 (b):** bảng nối `KhuyenMai_Mon (MaKhuyenMai, MaTenMon)` thay cột `TenMon.MaKhuyenMai` để một món nằm được trong nhiều khuyến mãi khác ngày; giá sau giảm lấy khuyến mãi **đang áp dụng hôm nay** của món (luôn ≤ 1 nhờ 4.2); script vá chuyển liên kết cũ sang bảng nối rồi bỏ cột cũ; `MonDto.khuyenMai` giữ nguyên dạng (khuyến mãi đang áp dụng) để tablet không phải sửa; form khuyến mãi bỏ nhãn "đang thuộc khuyến mãi khác" | API + script SQL + Web | Làm trước 4.1–4.2 |

**Kịch bản kiểm thử tay:** KM 20% món A hôm nay → đặt đơn A đúng 80% (cả giờ nào trong ngày); KM hết hạn → giá gốc; tạo KM thứ hai trùng ngày → bị chặn; đơn đặt trước khi sửa KM giữ giá cũ; gọi thẳng API với dữ liệu sai → 400.

---

## 9. Module 5 — Đơn hàng (lớn nhất, chia 4 bước)

**Phạm vi:** web `Pages/Orders/*`, `OrderService`, `Models/DonHang.cs`, `wwwroot/js/app.js` (in); backend `DonHangService`, `DonHangController`, `sp_ThanhToanVaTruKho`, bảng `DonHang`, `ChiTietDonHang`.

Làm **đúng thứ tự 5A → 5D**.

### 5A. Khóa hành vi hiện tại (chưa sửa gì)

| Mã | Việc | Phía |
|---|---|---|
| 5A.1 | Viết đầy đủ kịch bản kiểm thử tay trang Đơn hàng (mọi nút, mọi trạng thái, 2 vai trò) — lưới an toàn cho 5B | Web |
| 5A.2 | Test tự động phần logic tách được: số nhận món (1–50), bước phục vụ hợp lệ, `PhuongThucThanhToans.DaChot` / `Summary`, chuỗi lọc thời gian | Web |

### 5B. Dọn và tách trang (không đổi hành vi)

| Mã | Việc | Phía |
|---|---|---|
| 5B.1 | **Xóa code chết:** `servingSupported`, `ProposedApiError` và mọi nhánh "Máy chủ chưa hỗ trợ…" (backend đã có đủ route từ 18/11/2026); `TrangThaiDon.Color` (không còn ai gọi) | Web |
| 5B.2 | Tách `Orders.razor` (2.626 dòng) thành component: danh sách + bộ lọc, khung chi tiết, khối số nhận món / phục vụ, khối thanh toán, hộp tạo / sửa đơn, hộp hủy, hóa đơn in, banner đơn mới / đơn treo. Mỗi component một việc, tham số rõ ràng | Web (skill `dotnet-blazor:plan-ui-change`) |
| 5B.3 | Chạy lại **toàn bộ** kịch bản 5A → phải giống hệt trước khi tách | Web |

> Dọn + tách trước rồi mới sửa nghiệp vụ: sửa trong file 2.600 dòng là nguyên nhân chính của lỗi dây chuyền.

### 5C. Sửa nghiệp vụ

| Mã | Việc | Phía | Ghi chú |
|---|---|---|---|
| 5C.1 | **Thu tiền 2 lần:** `sp_ThanhToanVaTruKho` dùng `UPDATE … WHERE TrangThaiThanhToan='PENDING'` + kiểm tra `@@ROWCOUNT`; truyền hình thức thanh toán vào SP (không lưu trước khi gọi SP) | API + script SQL | Hiện có thể trừ kho 2 lần / ghi đè hình thức |
| 5C.2 | **B4:** `Huy()` và các thao tác đổi đơn cập nhật **có điều kiện** theo trạng thái đã đọc (như `SuaMon` đang làm); 0 dòng → 409 | API | Không thêm cột mới nếu cập nhật có điều kiện là đủ |
| 5C.3 | Web xử lý 409 **một chỗ duy nhất**: tải lại đơn, báo "Đơn vừa được thay đổi ở máy khác" (hiện mỗi thao tác tự xử lý riêng) | Web | |
| 5C.4 | **B1 / Q3:** trừ kho khi đơn chuyển sang **Đang chuẩn bị** (gọi thủ tục trừ kho trong `DoiTrangThaiPhucVu`); `sp_ThanhToanVaTruKho` chỉ còn đổi trạng thái thanh toán, không trừ kho; không trừ 2 lần | API + script SQL | |
| 5C.5 | **B2 / Q4:** hủy đơn đã bị trừ kho mà chưa pha (chỉ còn đơn trừ theo luật cũ) → trả lại kho, ghi `LichSuKho` (dùng loại giao dịch đang có hoặc thêm một giá trị loại, không thêm bảng) | API + Web (`LoaiGiaoDichKho`) | |
| 5C.6 | Hộp **Hủy đơn**: viết lại câu ảnh hưởng kho theo luật mới (bỏ câu "gợi ý nhập lại / xuất hủy" nếu hệ thống đã tự làm) | Web | |
| 5C.7 | **B3 (phần đơn):** bấm Đang chuẩn bị mà thiếu nguyên liệu → 422 `HET_NGUYEN_LIEU`, không đổi trạng thái, không tồn âm; web hiện danh sách thiếu (đã có khung hiển thị) | API + Web | |
| 5C.8 | **Q5:** chưa thu tiền thì **không** cho bấm Đã phục vụ (backend 409, web khóa nút + nói lý do, thay hộp hỏi lại hiện tại); đơn **Đã phục vụ không hủy được** (backend 409, web khóa nút Hủy đơn) | API + Web | |
| 5C.9 | **Q6 / P3:** thêm hộp xác nhận trước khi ghi thu tiền — tiền mặt: "Xác nhận đã thu X đ tiền mặt?"; chuyển khoản: "Đã kiểm tra tài khoản nhận được X đ?". Nhân viên xác nhận cả hai hình thức (không tự động qua ngân hàng) | Web | |
| 5C.10 | Đơn không chọn hình thức thanh toán → backend lưu `null` thay vì tự điền `CASH` (hiện không phân biệt được "khách chọn tiền mặt" với "chưa chọn") | API + script SQL | Web đã xử lý hiển thị |
| 5C.11 | **B12:** lỗi SQL thô không được lộ qua 409 — chỉ dùng câu của `RAISERROR`, lỗi khác trả câu cố định | API | |
| 5C.12 | **Rà soát code của module:** `DonHangService` (đặt món, sửa, thu, hủy dùng chung đoạn kiểm tra trạng thái → gom một hàm); mỗi component mới ở 5B có tên / tham số rõ; không còn code chết | Web + API | |
| 5C.13 | **P3:** thiết kế lại khối thanh toán của khung chi tiết đơn: ô chọn hình thức + nút xác nhận rõ ràng, màu nhấn không nhòe, không lẫn với nền khung chi tiết; rà lại các nút xanh đậm khác của trang Đơn hàng cho cân đối với bảng màu (`app.css` token). Gọi skill `ui-ux-pro-max` trước khi sửa, `ui-ux-reviewer` sau khi sửa | Web | Chỉ sửa giao diện nút đang có |

### 5D. Kiểm thử thực tế

| Mã | Việc | Phía |
|---|---|---|
| 5D.1 | In thử hóa đơn trên **máy in nhiệt thật** của quán (K80/K58), chỉnh `Receipt:PaperWidthMm` | Web + Chủ quán |
| 5D.2 | Kiểm thử dòng ghi chú món trên phiếu (chưa từng thử với dữ liệu thật) | Web |
| 5D.3 | **Tranh chấp:** 2 trình duyệt cùng thu tiền / cùng hủy / thu + hủy cùng lúc / cùng gán một số nhận món → chỉ một thao tác thắng, máy kia nhận 409 rõ ràng, kho chỉ trừ 1 lần | Web |
| 5D.4 | Giờ đông: tạo ~30 đơn liên tiếp khi nguyên liệu gần hết → không tồn âm | Web |

**DoD riêng Module 5:** ngoài 1.7, **đối soát SQL kho (3.4) khớp 100%** sau toàn bộ 5D; bảng dưới cho đúng kết quả mọi trường hợp.

| Trạng thái đơn khi hủy | Kho | Tiền |
|---|---|---|
| Chưa thu · Chờ | không đổi | không thu |
| Chưa thu · Đang chuẩn bị | đã trừ lúc bắt đầu pha (Q3) — không trả | không thu |
| Đã thu · Chờ | chưa trừ (Q3); đơn trừ theo luật cũ thì trả lại (Q4) | đánh dấu hoàn tiền |
| Đã thu · Đang chuẩn bị | đã tiêu hao — không trả | đánh dấu hoàn tiền |
| Đã phục vụ (luôn đã thu theo Q5) | **không hủy được** (Q5) | — |

---

## 10. Module 6 — Tổng quan & Báo cáo

**Phạm vi:** web `Pages/Dashboard/*`, `DashboardService`, `ReportExporter`, `Utilities/XlsxWriter.cs`, `Models/BaoCao.cs`; backend `BaoCaoController`.

| Mã | Việc | Phía | Ghi chú |
|---|---|---|---|
| 6.1 | **B5 / Q7:** `bao-cao/doanh-thu` giữ doanh thu gốc ở ngày thu, tiền hoàn tính ở **ngày hoàn**; `soDonHuy` và `soDonHoanTien` cùng một mốc thời gian | API | Sửa cách tính của báo cáo đang có |
| 6.2 | Thẻ "Doanh thu thuần" và sheet "Tổng hợp" của file Excel tính theo 6.1 (hiện số của ngày cũ tự giảm khi có hoàn tiền về sau) | Web | |
| 6.3 | ~~B7 / Q8: gom giờ cao điểm theo giờ đặt~~ — **không làm**: chủ quán giữ giờ thu tiền (Q8) | — | |
| 6.4 | Đối soát: script SQL tính doanh thu / số đơn / top món của một ngày mẫu → so với web và file Excel, khớp từng đồng → đưa vào kịch bản khói | Web (script kiểm thử) | |
| 6.5 | Test tự động: `ReportExporter.Build` (đủ 4 sheet, tổng khớp dữ liệu vào), `XlsxWriter` | Web | |
| 6.6 | **Rà soát code của module:** bỏ nhánh "máy chủ chưa hỗ trợ báo cáo topping" (`Dashboard.razor.cs`, `ReportExporter`, `Models/BaoCao.cs` — backend đã có route); `Dashboard.razor.cs` (539 dòng) tách phần dựng biểu đồ nếu khó đọc | Web | |

**Kịch bản kiểm thử tay:** đơn thu hôm qua, hoàn tiền hôm nay → báo cáo hôm qua **không đổi**, hôm nay có tiền hoàn; các mốc lọc (Hôm nay / 24 giờ / 7 / 30 ngày / tự chọn) khớp đối soát SQL; file Excel mở được, tổng khớp trang web.

---

## 11. Module 7 — Hồi quy toàn hệ thống & bàn giao

- Chạy **toàn bộ** kịch bản tay của Module 1–6 trên dữ liệu mẫu sạch, 3 khổ màn hình, 2 vai trò.
- Đối soát SQL kho + báo cáo khớp 100%.
- Build sạch cả web và backend; rà lại toàn bộ commit của đợt có đúng mẫu 1.6.
- Cập nhật `docs/huong-dan/*` theo luật nghiệp vụ mới; cập nhật `de-xuat-api-nghiep-vu-kho-bao-cao.md` (mục nào đã sửa).

### Kịch bản khói (chạy sau mỗi module, ~10 phút)

Bản chạy được (kèm cách tạo CSDL kiểm thử và 30 đơn mẫu): **`docs/kiem-thu/kich-ban-khoi.md`**. Tóm tắt:

1. Đăng nhập Quản lý → đủ 6 mục menu; Nhân viên → chỉ Đơn hàng.
2. Thêm 1 món có công thức → hiện trong hộp tạo đơn tại quầy, đúng giá (kể cả khuyến mãi).
3. Tạo đơn → gán số nhận món → Đang chuẩn bị → Thu tiền mặt → Đã phục vụ → xem trước hóa đơn (thu tiền trước khi phục vụ — luật Q5).
4. Tạo đơn thứ 2 → hủy kèm lý do.
5. Kho: nguyên liệu của món bước 2 giảm đúng định mức; đối soát SQL kho khớp.
6. Tổng quan "Hôm nay": doanh thu = tổng đơn bước 3; xuất Excel mở được.
7. Console trình duyệt không lỗi; `dotnet build` 2 phía sạch.

---

## 12. Ngoài phạm vi đợt này (chức năng MỚI — chỉ làm khi chủ quán yêu cầu)

Các ý tưởng dưới đây từng được nêu nhưng là **thêm chức năng mới**, nên **không làm** trong đợt sửa lỗi này:

| Ý tưởng | Lý do loại |
|---|---|
| Đồng bộ thời gian thực SignalR (`ke-hoach-dong-bo-thoi-gian-thuc.md` giai đoạn 1–3) | Cơ chế mới; tự làm mới 3 giây đang chạy đúng |
| Tự xác nhận chuyển khoản QR qua webhook ngân hàng | Tích hợp dịch vụ mới |
| Kiểm kê kho (nhập số đếm thực tế, tự ghi chênh lệch) | Chức năng + API mới; tồn sai hiện chỉnh bằng nhập kho / xuất hủy đang có |
| Chấm đếm nguyên liệu sắp hết trên menu | Thành phần giao diện mới |
| Tab quản lý Kích thước ly (S/M/L) | Màn hình mới; kích thước có sẵn trong CSDL |
| Chủ quán tự đổi mật khẩu của mình trên web | Màn hình đã bỏ theo quyết định 18/11/2026 |
| Giới hạn số lần đăng nhập sai / số lần `POST don-hang` (B11) | Cơ chế bảo mật mới |
| Hoàn tác "Đã phục vụ" / hoàn tác Thu tiền | Luồng mới (đi lùi trạng thái) |
| Tự hủy đơn treo sau N giờ / chốt cuối ngày (B10) | Cơ chế mới; banner đơn treo + Hủy đơn đang có |
| Báo cáo theo hình thức thanh toán; xem trước giá sau giảm; giá gốc gạch ngang trên hóa đơn | Báo cáo / hiển thị mới |
| Xem trước ảnh món | Thành phần giao diện mới |

---

## 13. Sổ việc phát sinh

Ghi mọi lỗi / ý tưởng **ngoài phạm vi module đang làm**. Chỉ xử lý khi tới module tương ứng, ở Module 7, hoặc khi chủ quán yêu cầu (nếu là chức năng mới).

| Ngày | Phát hiện khi làm | Thuộc module | Mô tả | Loại (sửa lỗi / chức năng mới / luồng mới) | Mức (chặn / thường) | Câu hỏi đã đặt → trả lời | Xử lý |
|---|---|---|---|---|---|---|---|
| 28/11/2026 | 0.1 | Đơn hàng | 5 commit "cap nhat" (82e2e34, 3969bf5, 2afbdc4, 63393eb, e1a87d7) push thẳng lên `main`, không qua PR: sửa hóa đơn in trong `Orders.razor` bằng `style="..."` viết trong thẻ (trái quy tắc dùng `app.css`), đánh số dòng món, `@inject AppState` không dùng; bỏ `UQ_CongThuc_Topping` trong `QuanCaPheDB.sql`; thêm `03-Web-Admin/package-lock.json` rỗng | Sửa lỗi / giao diện | Thường (xung đột với phần hóa đơn của web-admin) | Gộp, lấy cách đánh số món và giãn dòng nhưng chuyển vào CSS in (`app.js`), bỏ `@inject AppState` và `package-lock.json`; ràng buộc công thức sửa đủ ở Module 2 | Đang gộp |
| 28/11/2026 | Kịch bản khói | Kho / dữ liệu mẫu backend | `Data/SeedData.cs` ghi đơn vị nguyên liệu không dấu ("mieng") — hiện trong cảnh báo kho "Bánh tiramisu chỉ còn 3 mieng" | Sửa lỗi (dữ liệu mẫu) | Thường | — | Làm ở Module 3 |
| 28/11/2026 | 1.2 | Tài khoản | Sau 1.2, chủ quán tự đặt lại mật khẩu của chính mình ở trang Tài khoản thì phiên hiện tại hết hiệu lực ngay, lần gọi API kế tiếp bị đăng xuất (đúng luật bảo mật). Xem review Module 1 về cách trang xử lý | Sửa lỗi / trải nghiệm | Thường | — | Đã xử lý ở 1.1 (`995b31e`): lưu mật khẩu mới của chính mình thì đăng xuất gọn, ô mật khẩu có title báo trước |
| 28/11/2026 | Rà commit "cap nhat" | Đơn hàng | Commit `a558fe6` (phần việc dở của phiên trước) đã thêm lại "PHIẾU TẠM TÍNH" / "TỔNG CỘNG" cho đơn chưa thu và nút "In phiếu tạm tính" — trái quyết định của chủ quán ghi ở `tien-do-quan-ly-don-hang.md` (tiêu đề luôn là "HÓA ĐƠN THANH TOÁN") | Sửa lỗi (lệch quyết định) | Thường | Chủ quán xác nhận: bỏ phiếu tạm tính | Đã trả về "HÓA ĐƠN THANH TOÁN" cho mọi đơn, sửa kịch bản khói |
| | | | | | | | |
