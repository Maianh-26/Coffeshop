# Kế hoạch triển khai: đồng bộ đơn hàng thời gian thực (Tablet → Backend → Web admin)

> **Bản 3.0, 25/11/2026.** Thu gọn phía web theo yêu cầu: bỏ âm báo (tạm chưa phát triển); chỉ làm phần lõi là nhận đơn và thay đổi đơn tức thời trên trang Đơn hàng. Mỗi file mới đều phải nêu lý do (mục 6.0): phía web chỉ còn **1 file mới** (`Services/OrderRealtime.cs`).
> Bản 2.0 (24/11/2026) đã thêm phần kiến thức nền (mục 2), chi tiết từng giai đoạn (mục 6), và bỏ mọi chỗ dựa vào API giả. Chế độ mock của web admin đã bị xóa ngày 24/11/2026, nên web luôn chạy với backend thật.
> **IP tĩnh / mạng LAN (mục 6.2.1) để sau**, khi nhóm thống nhất môi trường chạy (LAN ở quán, đường hầm hay cloud). Không việc nào phía web phụ thuộc vào nó, vì địa chỉ máy chủ nằm trong `Api:BaseUrl`.
> Đã đối chiếu với code hiện tại: `01-Backend-api` (nhánh `main`, commit `6f9fdfc`), nhánh `origin/api-tablet-thanh-toan`, `origin/flutter-tablet` và `03-Web-Admin`.
> **Phạm vi:** app tablet (Flutter), backend `01-Backend-api` (ASP.NET Core 8 + EF Core 8 + SQL Server, chạy Kestrel), web admin `03-Web-Admin` (Blazor WebAssembly .NET 8).
> **Ai sửa gì:** nhóm web admin chỉ sửa code trong `03-Web-Admin`. Phần backend (mục 6.2, 6.3) và tablet (mục 6.5) là **đề xuất kèm code mẫu** để nhóm backend / nhóm Flutter làm.

---

## 0. Đọc nhanh (1 phút)

**Mục tiêu.** Khách bấm "Đặt" trên tablet thì trang Đơn hàng ở máy quầy **hiện đơn trong vòng 1 giây**. Mọi thao tác trên đơn (thu tiền, hủy, phục vụ, số nhận món, sửa món, đổi hình thức thanh toán từ tablet) cũng hiện ở các máy admin khác trong vòng 1 giây. Không mất đơn, không ra 2 đơn, kể cả khi wifi chập hoặc máy chủ khởi động lại.

**Hiện tại.** Web admin cứ 10 giây hỏi máy chủ một lần ("có đơn mới không?"), và chỉ hỏi khi đang mở trang Đơn hàng. Nút "Tự cập nhật" còn cho phép tắt việc hỏi này. Tablet gửi đơn một lần, không có cách chống trùng.

**Cách làm, gói trong 3 ý:**

1. **Máy chủ chủ động báo.** Ngay khi lưu đơn xong, máy chủ gửi một tin rất ngắn "đơn #123 vừa đổi" tới mọi máy admin qua một đường kết nối luôn mở (SignalR).
2. **Tin báo chỉ là cái chuông, dữ liệu luôn đọc lại từ cơ sở dữ liệu.** Nghe chuông thì web hỏi "cho tôi mọi đơn đã đổi kể từ lần trước". Nhờ vậy chuông có lỡ rơi thì lần hỏi sau vẫn lấy đủ.
3. **Tablet gửi kèm một mã phiếu cố định cho mỗi giỏ hàng.** Gửi lại bao nhiêu lần, máy chủ cũng chỉ tạo một đơn.

**Lộ trình:** giai đoạn 0 là 2 sửa nhỏ web làm ngay (bỏ nút "Tự cập nhật", hỏi 3 giây), đưa độ trễ còn 0–3 giây. Các giai đoạn 1–2 cần backend, giai đoạn 3 web dùng kênh đẩy (dưới 1 giây), giai đoạn 4 cần tablet, giai đoạn 5 là kiểm thử chung (mục 9).

---

## 1. Bài toán nghiệp vụ

### 1.1. Luồng thực tế ở quán

```
 Khách (tablet)              Máy chủ                       Quầy (web admin)
 ──────────────              ───────                       ────────────────
 Chọn món, chọn hình thức ──POST don-hang──▶ lưu đơn PENDING
                                                           ⟵ phải biết NGAY: có đơn mới
 (tiền mặt) đi tới quầy                                    bốc số gỗ, nhập số nhận món
                                                           thu tiền mặt → PAID
 (chuyển khoản) chờ                    NV xác nhận → PAID
 tablet hiện "Đã thanh toán" ⟵──────── (tablet đang hỏi 3 giây/lần)
                                                           Bắt đầu chuẩn bị → Đã phục vụ
```

- Khách đặt xong thường **đi thẳng tới quầy** để lấy số gỗ hoặc trả tiền mặt. Quầy chậm vài giây là khách đứng chờ trước một màn hình chưa có đơn.
- Đơn chuyển khoản không cần khách tới quầy. Quầy không được báo thì đơn **bị bỏ quên**.
- Vì vậy "quầy nhận đơn mới" là **điều kiện bắt buộc** của luồng, không phải tùy chọn. Các phần mềm cùng loại (Toast, Square, KiotViet, Sapo, xem mục 10) đều đẩy đơn tức thời. Họ chỉ cho tắt **âm báo**, không cho tắt **việc nhận đơn**.
- Âm báo **tạm chưa làm** ở dự án này. Có thể thêm sau mà không phải đổi thiết kế, vì "đơn mới" đã được tính từ dữ liệu (mục 6.4.4).

### 1.2. Yêu cầu đo được

| Loại | Yêu cầu | Chỉ tiêu |
|---|---|---|
| Chức năng | Đơn mới từ tablet hiện trên **trang Đơn hàng** (bảng + banner "đơn mới") mà không cần bấm gì | 100% đơn hiện ra |
| Chức năng | Thay đổi của đơn (thu tiền, hủy, phục vụ, số nhận món, sửa món, đổi hình thức) hiện ở các máy admin khác đang mở trang Đơn hàng | 100% thay đổi hiện ra |
| Chức năng | Hai máy cùng thao tác một đơn: không ghi đè im lặng | máy đến sau nhận thông báo rõ ràng (409) |
| Tạm chưa làm | Âm báo; số đếm / "(n)" khi đang ở trang khác | — |
| Độ trễ | Tablet bấm Đặt → quầy được báo | **p95 < 1 giây** (xem "p95" ở mục 2.9) trong wifi quán |
| Độ trễ khi lỗi | Mất kết nối web ↔ máy chủ rồi có lại | đủ đơn **≤ 3 giây sau khi có mạng lại** (vòng nền 3 giây; kênh trực tiếp có thể nối lại chậm hơn, không ảnh hưởng dữ liệu) |
| Độ trễ xấu nhất | Chuông rơi mà kết nối không báo lỗi | **≤ 30 giây** |
| Độ trễ ở giai đoạn 0 | Chưa có kênh đẩy, chỉ hỏi 3 giây | ≤ 3 giây khi tab đang mở phía trước; ≤ ~60 giây khi tab bị ẩn lâu (mục 2.13). Giai đoạn 3 hết giới hạn này |
| Đúng đắn | Không mất đơn, không báo trùng, trạng thái cũ không đè trạng thái mới | 0 lỗi trong bộ test mục 9 |
| Đúng đắn | Tablet gửi lại (mạng chập, bấm 2 lần) | 0 đơn trùng |
| Bảo mật | Chỉ NhanVien / QuanLy đọc được dữ liệu đơn | như REST hiện nay |

### 1.3. Ràng buộc

- Quán gia đình: **1 máy chủ**, vài máy admin (máy quầy, điện thoại chủ quán), 1 đến vài tablet. Không cần Redis, message broker hay Azure SignalR.
- Stack có sẵn: ASP.NET Core 8, EF Core 8, SQL Server, JWT; Blazor WASM .NET 8; Flutter.
- Hợp đồng API là `openapi.yaml` của backend. Request có trường lạ bị trả **400**, nên mọi trường / header mới phải được ghi vào hợp đồng trước khi web hoặc tablet gửi.

---

## 2. Kiến thức nền: các thuật ngữ và áp dụng vào quán

Mỗi mục dưới đây có 3 phần: **là gì** (định nghĩa ngắn), **ví dụ trong quán**, và **dùng ở đâu trong kế hoạch**.

### 2.1. Hỏi định kỳ (polling) và đẩy (push)

- **Là gì.** *Polling*: máy khách cứ X giây hỏi máy chủ "có gì mới không?", kể cả khi không có gì. *Push*: máy khách mở sẵn một đường kết nối, máy chủ có tin thì gửi xuống ngay.
- **Ví dụ.** Polling giống nhân viên cứ 10 giây chạy ra cửa xem có khách không. Push giống gắn chuông cửa: khách tới thì chuông kêu.
- **Trong dự án.**
  - Hiện tại web polling 10 giây (`Orders.razor`, `RefreshSeconds = 10`). Đơn tới ngay sau một lượt hỏi thì phải chờ gần 10 giây; trung bình chờ khoảng 5 giây.
  - Giai đoạn 0 rút còn 3 giây. Giai đoạn 3 chuyển sang push.
  - Polling **vẫn được giữ lại** làm đường dự phòng (30 giây một lần), vì push có thể rơi tin.
- **Chi phí của polling 3 giây (giai đoạn 0):**
  - Mỗi máy đang mở trang Đơn hàng tải bảng 20 lần/phút. Mỗi lần là một `COUNT(*)` cộng một truy vấn 20 dòng, đọc 4 bảng (split query).
  - Vài nghìn đơn mà vài máy cùng hỏi vẫn nhẹ với SQL Server.
  - Giai đoạn 3: khi không có thay đổi, mỗi máy chỉ gọi `thay-doi` 1 lần / 30 giây. Truy vấn đó chỉ đọc các dòng có `PhienBan` lớn hơn con trỏ, có chỉ mục, và thường trả 0 dòng.

### 2.2. WebSocket

- **Là gì.** Một kết nối mạng hai chiều luôn mở giữa trình duyệt và máy chủ. Nó bắt đầu như một request HTTP, rồi "nâng cấp" thành kênh để hai bên gửi tin bất kỳ lúc nào.
- **Ví dụ.** Giống một cuộc gọi điện để mở máy, so với HTTP thường là gửi từng lá thư.
- **Trong dự án.** Đây là "đường dây" chở chuông từ máy chủ tới web admin. Backend chạy **Kestrel**, nên hỗ trợ WebSocket sẵn, không phải cấu hình thêm IIS.

### 2.3. SignalR, hub, client

- **Là gì.** SignalR là thư viện của Microsoft giúp dùng WebSocket dễ hơn. Nó tự làm những việc khó:
  - **negotiate**: client hỏi máy chủ "dùng cách kết nối nào?";
  - **tự lùi**: nếu WebSocket bị chặn thì dùng Server-Sent Events, rồi đến long polling;
  - **tự kết nối lại** khi rớt mạng.
- **Hub** là lớp ở máy chủ mà client kết nối vào, ví dụ `DonHangHub`. **`IHubContext<DonHangHub>`** cho phép code bất kỳ (ví dụ `DonHangService`) gửi tin tới các client đang kết nối vào hub.
- **Client** là thư viện phía web: gói NuGet `Microsoft.AspNetCore.SignalR.Client`, chạy được trong Blazor WASM.
- **Điều SignalR KHÔNG làm:** nó **không lưu** tin gửi trong lúc client mất kết nối. Tin gửi lúc đó **mất luôn**. Đây là lý do của mục 2.4.
- **Trong dự án.** Hub `/hubs/don-hang`. Sự kiện duy nhất là `DonHangThayDoi`. Client chỉ nghe, không gọi ngược lên máy chủ.

### 2.4. "Chuông" và "nguồn sự thật"

- **Là gì.** *Nguồn sự thật (source of truth)* là nơi duy nhất mà ta tin dữ liệu ở đó là đúng; ở đây là bảng `DonHang` trong SQL Server. Tin đẩy **chỉ là chuông**: nó báo "có thay đổi ở đơn #123", **không chở** dữ liệu đơn.
- **Ví dụ.** Chuông cửa reo không cho biết khách gọi món gì; nhân viên vẫn phải ra hỏi. Chuông có hỏng thì lần đi tuần kế tiếp vẫn thấy khách.
- **Vì sao làm vậy.**
  - Chuông rơi: lần đồng bộ sau vẫn đọc được từ DB.
  - Chuông trùng: đọc lại thì kết quả vẫn thế.
  - Chuông lộn thứ tự ("đã thu" đến trước "đã tạo"): web luôn đọc bản **mới nhất** trong DB.
- **Quy tắc trong dự án:** mọi quyết định hiển thị (đơn nào là mới, đơn nào đã đổi) đều tính từ **dữ liệu đọc về**, không tính từ nội dung chuông.

### 2.5. Transaction và commit

- **Là gì.** *Transaction* là một nhóm thao tác ghi DB được thực hiện "tất cả hoặc không gì". *Commit* là lúc chốt: từ lúc đó người khác mới **thấy** dữ liệu. Trước khi commit, dữ liệu như đang viết nháp.
- **Ví dụ.** `SuaMon` xóa các dòng món cũ, thêm món mới, cập nhật tổng tiền trong **một** transaction (`DonHangService.cs` dòng 131–159). Không ai thấy đơn ở trạng thái "đã xóa món cũ nhưng chưa có món mới".
- **Trong dự án.** Chỉ rung chuông **sau khi commit thành công**. Rung trước mà commit lỗi thì web đọc về sẽ không thấy gì, hoặc thấy dữ liệu cũ.

### 2.6. IDENTITY được cấp trước khi commit, nên "mã lớn nhất" không đủ để dò đơn mới

- **Là gì.** `MaDonHang` là cột IDENTITY: SQL Server cấp số **lúc INSERT**, chưa phải lúc commit.
- **Ví dụ theo thời gian** (xảy ra khi web đọc được dữ liệu mà không phải chờ dòng chưa commit, xem lưu ý bên dưới):

  ```
  t=0 ms    Tablet A: INSERT đơn + các dòng món → được cấp MaDonHang = 100 (transaction chưa commit)
  t=1 ms    Tablet B: INSERT → được cấp MaDonHang = 101, commit ngay
  t=2 ms    Web hỏi: thấy đơn 101 → ghi nhớ "mã lớn nhất đã thấy = 101"
  t=5 ms    Tablet A commit đơn 100
  t=3000 ms Web hỏi tiếp: thấy đơn 100, nhưng 100 < 101 → coi là CŨ → KHÔNG BÁO
  ```
- **Lưu ý với DB hiện tại.**
  - `QuanCaPheDB` **không bật** `READ_COMMITTED_SNAPSHOT`, nên câu `GET don-hang` sẽ **chờ** dòng chưa commit chứ không bỏ qua nó. Vì vậy lỗi trên hiện gần như chỉ là lý thuyết.
  - Nó sẽ thành thật khi DB bật snapshot (thường bật để giảm chờ khóa) hoặc khi có truy vấn `NOLOCK`.
  - Kiểm tra kho (`DungChiTiet`) chạy **trước** `SaveChanges`, nên transaction INSERT chỉ kéo dài vài ms.
- **Trong dự án.**
  - `CheckNewOrders` (`Orders.razor`, khoảng dòng 1802) đang dùng `o.MaDonHang > lastSeenOrderId` trên một lượt quét "trang 1 mới nhất", nên có thể dính lỗi này.
  - Giai đoạn 3 bỏ lượt quét đó. Mọi thay đổi đi qua **con trỏ `rowversion`** (mục 2.7, 2.8), và con trỏ không bỏ sót đơn commit muộn.
  - Mốc mã đơn (`mocMaDon`) chỉ còn dùng để phân biệt "đơn tạo sau khi mở trang" với "đơn cũ vừa được sửa" (mục 6.4.4).

### 2.7. rowversion và con trỏ đồng bộ

- **Là gì.**
  - `rowversion` là kiểu cột của SQL Server. Mỗi lần một dòng được INSERT hoặc UPDATE, SQL Server tự gán cho cột đó một số 8 byte **lớn hơn mọi số đã cấp trước đó trong cả DB**. Không ai phải viết code để tăng nó.
  - **Con trỏ đồng bộ (cursor)** là con số "tôi đã đọc tới đây". Web giữ con trỏ, mỗi lần hỏi "cho tôi mọi đơn có `PhienBan` lớn hơn con trỏ", rồi dời con trỏ lên.
- **Ví dụ.**

  ```
  Đơn 100 tạo        → PhienBan = 5001
  Đơn 101 tạo        → PhienBan = 5002
  Đơn 100 được thu   → PhienBan của đơn 100 đổi thành 5003
  Web đang giữ con trỏ = 5001 → hỏi "PhienBan > 5001" → nhận đơn 101 (5002) và đơn 100 (5003)
  → con trỏ mới = 5003
  ```
  Giống đánh dấu trang sách: lần sau đọc tiếp từ chỗ đánh dấu, không đọc lại từ đầu, cũng không bỏ trang nào.
  Các số ở ví dụ viết liền nhau cho dễ đọc. Thực tế số chỉ **luôn tăng** nhưng có thể **nhảy cóc**, vì cả DB (mọi bảng có cột rowversion) dùng chung một bộ đếm. Code chỉ được so sánh lớn / nhỏ, không được dựa vào việc số liền nhau.
- **So với hỏi "trang 1 mới nhất":** cách đó chỉ thấy **đơn mới tạo**. Con trỏ thấy **mọi thay đổi** (thu tiền, hủy, đổi số nhận món ở máy khác), kể cả đơn cũ nằm ở trang 5.
- **Trong dự án.** Thêm cột `PhienBan ROWVERSION` vào `DonHang` (giai đoạn 1). Endpoint `GET don-hang/thay-doi?sau=<con trỏ>` trả mọi đơn đã đổi.

### 2.8. MIN_ACTIVE_ROWVERSION: không để con trỏ vượt qua đơn chưa commit

- **Vấn đề.** `rowversion` cũng được cấp lúc ghi, không phải lúc commit, nên dính lỗi giống mục 2.6:

  ```
  Transaction X ghi đơn 100 → PhienBan 5004 (chưa commit)
  Transaction Y ghi đơn 101 → PhienBan 5005, commit
  Web hỏi "> 5003" → chỉ thấy 5005 → dời con trỏ lên 5005
  X commit → đơn 100 có PhienBan 5004 < 5005 → web không bao giờ hỏi tới nữa → MẤT
  ```
- **Giải pháp.** Hàm `MIN_ACTIVE_ROWVERSION()` của SQL Server trả về **số nhỏ nhất đang bị một transaction chưa commit giữ**. Nếu không có transaction nào đang mở, nó trả về số kế tiếp sẽ được cấp. Ở ví dụ trên, hàm trả về 5004. Máy chủ chỉ trả các dòng có `PhienBan < 5004` và đặt con trỏ tối đa là 5003. Đơn 101 sẽ được lấy ở lượt sau, khi X đã commit, cùng với đơn 100.
- **Thứ tự bắt buộc:** **đọc mốc trước, đọc dòng sau.** Làm ngược lại thì một transaction có thể commit vào giữa hai lần đọc.
- **Cái giá:** một transaction chậm sẽ làm các thay đổi khác chờ theo, nhưng chỉ chờ bằng thời gian của transaction đó. Các transaction ở quán chỉ vài chục ms.
- **Vì sao vẫn cần dù DB đang không bật snapshot (mục 2.6):** câu đọc theo con trỏ chỉ lấy các dòng **dưới mốc**, nên nó không phải đứng chờ một transaction đang mở. Nó cũng vẫn đúng nếu sau này DB bật snapshot.

### 2.9. Độ trễ, p95

- **Là gì.** *Độ trễ* ở đây là thời gian từ lúc tablet bấm Đặt (hoặc máy A bấm một thao tác) tới lúc đơn hiện ra / đổi trên trang Đơn hàng ở máy quầy. *p95 < 1 giây* nghĩa là xếp các lần đo từ nhanh đến chậm thì 95% số lần nhanh hơn 1 giây. Dùng p95 thay vì trung bình vì trung bình che mất những lần chậm bất thường.
- **Trong dự án.** Chuông mang `gioCommit` (giờ máy chủ lúc commit). Web ghi log `giờ hiện đơn − gioCommit`. Cách đo ở mục 9.1.

### 2.10. Khóa chống trùng (Idempotency-Key) và băm nội dung

- **Là gì.**
  - Một thao tác là *idempotent* nếu làm 1 lần hay 10 lần cũng cho cùng kết quả.
  - `POST` tạo đơn vốn **không** idempotent: gửi 2 lần ra 2 đơn. Cách chữa phổ biến (Stripe dùng) là client gửi kèm header `Idempotency-Key: <GUID>`. Máy chủ nhớ khóa này cùng với đơn đã tạo. Gặp lại khóa thì trả lại đơn cũ, không tạo mới.
  - **Băm nội dung** (SHA-256) là biến nội dung request thành một chuỗi 32 byte. Hai nội dung giống hệt nhau cho cùng một chuỗi băm. Lưu chuỗi băm để phát hiện trường hợp "cùng khóa mà nội dung khác", thường là lỗi lập trình ở tablet.
  - Máy chủ băm **đối tượng `DatMonRequest` đã đọc ra**, không băm chuỗi JSON thô. Nhờ vậy thứ tự trường hay khoảng trắng không ảnh hưởng, nhưng thứ tự các dòng món có ảnh hưởng: tablet gửi lại phải giữ đúng giỏ cũ.
  - Nếu nâng cấp backend thêm trường vào `DatMonRequest` đúng lúc tablet đang gửi lại, chuỗi băm sẽ khác và tablet nhận 422. Rất hiếm; chỉ cần tránh triển khai backend giữa giờ đông khách.
- **Ví dụ.**

  ```
  Tablet: POST don-hang, Idempotency-Key = 7f3a…, giỏ [2 bạc xỉu]
  Máy chủ: lưu đơn 100 (MaYeuCau = 7f3a…), gửi phản hồi 201 → wifi rớt, tablet không nhận được
  Tablet: chờ 1 giây, gửi lại y hệt, cùng khóa 7f3a…
  Máy chủ: thấy 7f3a… đã có → trả 200 + đơn 100. Không tạo đơn 101.
  ```
- **Trong dự án.** Cột `MaYeuCau` (GUID, có chỉ mục duy nhất) và `BamYeuCau` (32 byte) trên `DonHang`. Quy tắc chi tiết ở mục 6.2.4.

### 2.11. Tranh chấp ghi (race condition) và kiểm tra phiên bản (optimistic concurrency)

- **Là gì.**
  - *Race condition*: hai người cùng sửa một thứ cùng lúc, kết quả phụ thuộc ai tới sau.
  - *Optimistic concurrency*: không khóa trước. Lúc ghi thì kiểm tra "dòng còn đúng phiên bản tôi đã đọc không". Nếu đã bị người khác đổi thì từ chối, máy chủ trả **409 Conflict**.
- **Ví dụ.** Máy quầy và điện thoại chủ quán cùng mở đơn 100 và cùng bấm Hủy. Không kiểm tra thì cả hai đều "thành công", lần sau ghi đè lý do hủy của lần trước.
- **Trong dự án.**
  - Khai báo `PhienBan` bằng `.IsRowVersion()` thì khi `SaveChanges` gửi **UPDATE / DELETE** cho một `DonHang` đã đọc lên, EF Core tự thêm `WHERE PhienBan = <bản đã đọc>`. Nếu không còn dòng nào khớp, EF ném `DbUpdateConcurrencyException`.
  - Cơ chế này **không** áp dụng cho `ExecuteUpdate` / `ExecuteDelete` và stored procedure (`SuaMon`, `GanSoNhanMon`, `DoiTrangThaiPhucVu`, `sp_ThanhToanVaTruKho`). Muốn chặn ở các lệnh đó phải tự thêm điều kiện (mục 6.2.7, phần 1b).
  - Khi không có trường nào đổi (ví dụ `ThanhToan` giữ nguyên phương thức), EF không gửi UPDATE nên cũng không kiểm tra.
  - Exception này **phải** được map thành **409 `DON_DA_THAY_DOI`**. Hiện `ErrorHandlingMiddleware` (dòng 20) chỉ bắt lỗi trùng khóa, nên exception này đang thành **500**.

### 2.12. Thử lại có lùi dần (retry with backoff)

- **Là gì.** Gặp lỗi mạng thì thử lại, nhưng mỗi lần chờ lâu hơn (0, 2, 5, 10, 30 giây…). Thử dồn dập khi máy chủ đang khởi động chỉ làm nó chậm thêm.
- **Lưu ý SignalR.**
  - `WithAutomaticReconnect()` mặc định chỉ thử 4 lần (0, 2, 10, 30 giây) rồi **bỏ cuộc**. Ta phải tự viết một chính sách **thử mãi** (giữ 30 giây một lần).
  - `WithAutomaticReconnect` cũng **không** thử lại lần `StartAsync()` đầu tiên. Lần đầu phải tự bọc vòng lặp.

### 2.13. Sự kiện tab hiện lại (visibilitychange)

- **Là gì.** Trình duyệt bắn sự kiện `visibilitychange` khi người dùng chuyển qua lại giữa các tab, hoặc khi máy ngủ rồi thức. Tab bị ẩn thì Chrome làm chậm các bộ hẹn giờ (`setTimeout`/`Timer`, cả `PeriodicTimer` của Blazor WASM): sau vài phút ẩn, có thể chỉ còn **1 lần/phút**.
- **Trong dự án.**
  - **Giới hạn của giai đoạn 0:** tab bị ẩn lâu thì đơn mới có thể trễ tới ~60 giây, vì chỉ có hẹn giờ. Muốn không trễ thì để máy quầy mở sẵn tab web admin ở phía trước.
  - **Giai đoạn 3 hết giới hạn này** mà không cần nghe `visibilitychange`. Tin đẩy qua WebSocket vẫn tới ngay khi tab bị ẩn, và lời gọi `thay-doi` chạy ngay khi chuông tới, không chờ hẹn giờ.
  - Trường hợp máy ngủ rồi thức: kết nối cũ đã chết. SignalR phát hiện trong khoảng 30 giây (thời gian chờ mặc định) rồi tự kết nối lại và đồng bộ, nằm trong chỉ tiêu "xấu nhất ≤ 30 giây". Muốn nhanh hơn thì sau này có thể thêm nghe `visibilitychange` (tạm chưa làm).

### 2.14. JWT trên query string, CORS, địa chỉ 0.0.0.0

- **JWT qua query.** Trình duyệt **không cho** gắn header `Authorization` vào WebSocket. Vì vậy SignalR gửi token dưới dạng `?access_token=…` trên URL, và máy chủ phải đọc nó ra (`OnMessageReceived`), **chỉ cho đường dẫn `/hubs`**. URL có chứa token, nên **không được ghi log URL** của `/hubs`.
- **CORS.** Web admin (`localhost:5101`) và API (`:3000`) là hai "nguồn" khác nhau, nên trình duyệt chỉ cho gọi nếu máy chủ cho phép. Hiện backend dùng `AllowAnyOrigin()` (`Program.cs` dòng 84). Tài liệu SignalR khuyến nghị **liệt kê origin cụ thể** kèm `AllowCredentials()`; hai lựa chọn `AllowAnyOrigin` và `AllowCredentials` không dùng chung được.
- **0.0.0.0.** Máy chủ nghe `localhost:3000` nghĩa là **chỉ chính máy đó** gọi được. Tablet và điện thoại trong wifi quán bị từ chối. Đổi thành `http://0.0.0.0:3000` thì máy chủ nhận kết nối từ mọi máy trong mạng. Cần mở thêm cổng 3000 trên Windows Firewall.
  - Địa chỉ nghe được khai ở **hai chỗ**: `appsettings.json` dòng 2 (`Urls`) và `Properties/launchSettings.json` dòng 9 (`applicationUrl`).
  - Khi chạy bằng `dotnet run` hoặc Visual Studio, `launchSettings.json` được nạp thành biến môi trường `ASPNETCORE_URLS`, và biến này **đè** lên `appsettings.json`. Vì vậy phải sửa **cả hai** (mục 6.2.1).

---

## 3. Hiện trạng và vấn đề (đối chiếu code)

| # | Đoạn | Hiện tại (file, dòng) | Vấn đề |
|---|---|---|---|
| H1 | Mạng | `01-Backend-api/appsettings.json:2` `"Urls": "http://localhost:3000"` và `Properties/launchSettings.json:9` `"applicationUrl": "http://localhost:3000"` (bản này đè bản kia khi `dotnet run`) | Tablet và điện thoại trong LAN **không vào được**. Lúc phát triển (mỗi người chạy trên máy mình) thì không sao; phải sửa trước khi chạy chung / demo / triển khai ở quán (mục 6.2.1, **để sau**). |
| H2 | Tablet → máy chủ | `POST api/v1/don-hang` (`DonHangController.cs:21`, `AllowAnonymous`), không có khóa chống trùng | Mạng chập mà khách bấm lại thì ra **2 đơn**. Không bấm lại thì khách tưởng đã đặt, trong khi có thể **mất đơn**. |
| H3 | Tablet sửa giỏ | `cart.dart` (`checkout`, `_orderSig`): sửa giỏ sau khi đã có đơn thì tạo **đơn mới** | Đơn cũ **treo PENDING mãi** trên web. |
| H4 | Máy chủ → web | Không có kênh đẩy. Web hỏi `GET don-hang` mỗi 10 giây (`Orders.razor`: `RefreshSeconds = 10` dòng 1125; `Timer` dòng 1380; `AutoRefresh` dòng 1556) | Trễ đến ~10 giây. Lượt hỏi bị **bỏ qua** khi đang mở hộp thoại / đang thao tác (dòng 1559). Tab ẩn thì hẹn giờ chậm lại. **Chỉ trang Đơn hàng** mới dò đơn mới (bản 3.0 chấp nhận giữ như vậy, xem mục 6.0). |
| H5 | Dò đơn mới | `CheckNewOrders` dòng 1778–1827: mới khi `MaDonHang > lastSeenOrderId` | Có thể bỏ sót đơn commit muộn khi DB bật snapshot (mục 2.6). Với cấu hình hiện tại hiếm xảy ra, nhưng sửa luôn vì cách sửa rẻ. |
| H6 | Nút "Tự cập nhật" | `Orders.razor` dòng 285–297, `ToggleAutoRefresh` dòng 1727 | Tắt là **mất báo đơn mới**, không có cảnh báo. F5 thì bật lại. Lý do tồn tại (bảng nhảy khi đang thao tác) đã được `AutoRefresh` tự xử lý. |
| H7 | Máy chủ → tablet | Tablet hỏi `GET don-hang/{id}/trang-thai` mỗi 3 giây khi chờ thanh toán | Đủ dùng, **giữ nguyên**. |
| H8 | Tranh chấp | `DonHang` (`Data/Entities.cs:92`) chưa có cột phiên bản. `ThanhToan` (`DonHangService.cs:171`) `SaveChanges` phương thức (dòng 180) rồi mới `EXEC sp_ThanhToanVaTruKho` (dòng 183), không chung transaction. SP thiếu `WHERE TrangThaiThanhToan = 'PENDING'` | Hai máy cùng sửa thì ghi đè nhau. Thu tiền 2 lần có thể trừ kho 2 lần. |
| H9 | Lỗi tranh chấp | `ErrorHandlingMiddleware.cs:20` chỉ bắt `DbUpdateException` trùng khóa | Sau khi thêm `rowversion`, tranh chấp thành **500**. |

---

## 4. Các phương án đã cân nhắc

| Phương án | Độ trễ | Độ tin cậy | Công sức | Kết luận |
|---|---|---|---|---|
| A. Hỏi định kỳ nhanh hơn (3 giây) | 0–3 giây | Tốt (luôn đọc DB) | Rất ít, chỉ web | **Làm ngay (giai đoạn 0)**, giữ lại làm dự phòng |
| B. Long polling tự viết | < 1 giây | Trung bình | Vừa | Không chọn: SignalR đã có sẵn (và tự lùi về long polling khi cần) |
| C. Server-Sent Events tự viết | < 1 giây | Trung bình | Vừa | Không chọn: phải tự viết kết nối lại, xác thực |
| **D. SignalR đẩy chuông + con trỏ `rowversion` + dò dự phòng** | **< 1 giây** | **Cao**: chuông rơi vẫn bù được | Vừa | **Chọn** |
| E. Bảng outbox + message broker | < 1 giây | Rất cao | Lớn | Thừa với 1 máy chủ. Ý tưởng "đọc lại từ DB theo con trỏ" của E được giữ ở dạng nhẹ trong D |

---

## 5. Thiết kế được chọn

### 5.1. Sơ đồ thành phần

```
┌──────────────┐  POST don-hang                ┌────────────────────────────────────┐
│  TABLET      │  + Idempotency-Key (GUID)     │  BACKEND (Kestrel, 0.0.0.0:3000)   │
│  (Flutter)   │ ───────────────────────────▶  │                                    │
│  gửi lại khi │  ◀── 201 mới / 200 đơn cũ ─── │  DonHangService (7 hàm ghi)        │
│  lỗi mạng,   │                               │    ghi DonHang → PhienBan tự tăng  │
│  CÙNG khóa   │                               │    COMMIT → notifier.Bao(ma, pb)   │
└──────────────┘                               │                 │                  │
                                               │  DonHangHub /hubs/don-hang         │
                                               │  [Authorize NhanVien,QuanLy]       │
                                               │      sự kiện "DonHangThayDoi"      │
                                               │      {maDonHang, phienBan,         │
                                               │       gioCommit}                   │
                                               │  GET don-hang/thay-doi?sau=<con trỏ>│
                                               └─────────────────┼──────────────────┘
                                                 WebSocket (đẩy) │      ▲ REST (đọc)
                                                                 ▼      │
                                               ┌────────────────────────────────────┐
                                               │  WEB ADMIN (Blazor WASM)           │
                                               │  OrderService.GetChangesAsync      │
                                               │    (REST: GET thay-doi)            │
                                               │  OrderRealtime (1 bản / lần mở     │
                                               │  trang Đơn hàng)                   │
                                               │   ├─ chuông / kết nối lại / dự     │
                                               │   │  phòng 30 s (3 s khi mất mạng) │
                                               │   │     → ĐỒNG BỘ theo con trỏ     │
                                               │   └─ gộp trùng trong một lượt      │
                                               │  Orders.razor                      │
                                               │   ├─ "đơn mới" → banner            │
                                               │   ├─ khung chi tiết / cảnh báo     │
                                               │   └─ tải lại bảng (giữ bộ lọc)     │
                                               └────────────────────────────────────┘
```

### 5.2. Một đơn đi qua hệ thống (khi mọi thứ bình thường)

| Thời điểm (ước tính) | Bước | Ở đâu |
|---|---|---|
| 0 ms | Khách bấm "Đặt". Tablet sinh `Idempotency-Key` (nếu giỏ chưa có), khóa giỏ, hiện "Đang gửi đơn…" | Tablet |
| ~20 ms | `POST api/v1/don-hang` tới máy chủ qua wifi | Mạng |
| ~60 ms | `DatMon`: tìm theo khóa (chưa có), kiểm tra món / kho, `INSERT` (`PhienBan` tự cấp), **commit** | Backend |
| ~62 ms | `DonHangNotifier.Bao(100, 5004)` gửi chuông tới mọi máy admin; trả 201 cho tablet | Backend |
| ~80 ms | `OrderRealtime` nhận chuông → `GET don-hang/thay-doi?sau=5003` | Web |
| ~120 ms | Nhận đơn 100: `maNguoiDung == null`, mã lớn hơn `mocMaDon`, chưa báo → **đơn mới**, hiện banner | Web |
| ~170 ms | Trang tải lại bảng đang xem (1 request) → đơn 100 nằm đầu bảng | Web |

Tổng cộng khoảng 0,1–0,3 giây, còn dư nhiều so với chỉ tiêu 1 giây.

### 5.3. Nguyên tắc cốt lõi

1. **DB là sự thật, tin đẩy chỉ là chuông** (mục 2.4).
2. **Mọi thay đổi đơn đều tăng `PhienBan`**, và con trỏ **không bao giờ vượt qua một transaction chưa commit** (mục 2.7, 2.8).
3. **Chỉ rung chuông sau khi commit thành công, và chỉ khi thật sự có ghi.** Rung lỗi thì chỉ ghi log, không làm hỏng request.
4. **Mọi đường lỗi đều có đường bù:**
   - đồng bộ ngay khi vừa kết nối và khi kết nối lại;
   - dò dự phòng 30 giây cho trường hợp chuông rơi mà kết nối không báo lỗi;
   - dò 3 giây khi đang mất kết nối.
5. **Tablet gửi lại an toàn.** Khóa chống trùng được kiểm tra **trước** mọi kiểm tra khác (mục 6.2.4).

---

## 6. Triển khai chi tiết theo giai đoạn

Mỗi giai đoạn ghi: mục tiêu, **file cần sửa**, code mẫu, và điều kiện **"xong khi"**.

### 6.0. Phía web: đặt code ở đâu, có cần file mới không

**Phạm vi phía web (bản 3.0).** Chỉ làm phần lõi:

- **nhận đơn và mọi thay đổi của đơn gần như tức thời trên trang Đơn hàng**;
- áp đúng các thay đổi đó vào bảng, banner "đơn mới" và khung chi tiết;
- không ghi đè im lặng khi hai máy cùng thao tác một đơn.

**Tạm chưa làm** (có thể thêm sau mà không đổi thiết kế lõi):

- âm báo;
- số đếm đơn mới ở menu và "(n)" trên tiêu đề tab khi đang ở trang khác;
- đồng bộ ngay khi tab hiện lại (`visibilitychange`).

Nhân viên (`NhanVien`) vốn chỉ có trang Đơn hàng, nên phần lõi đã phủ đủ người trực quầy.

#### 6.0.1. Vì sao không gộp kết nối thời gian thực vào `OrderService`?

Ba chỗ có thể đặt code kết nối thời gian thực:

| Phương án | Cách làm | Được | Mất |
|---|---|---|---|
| A. Viết thẳng trong `Orders.razor` | Hub, con trỏ, vòng dự phòng nằm trong `@code` của trang | Không thêm file | Trang đã **2.564 dòng**; thêm ~150 dòng logic kết nối lẫn với logic giao diện, rất khó đọc và khó kiểm tra |
| B. Gộp vào `OrderService` | `OrderService` vừa gọi REST vừa giữ kết nối SignalR | Không thêm file | Xem 3 lý do bên dưới |
| **C. `OrderService` + 1 file `OrderRealtime`** | `OrderService` thêm **một hàm REST** `GetChangesAsync`; phần kết nối nằm ở `Services/OrderRealtime.cs` | Mỗi lớp một việc; trang chỉ nhận kết quả | Thêm **1 file** (~150 dòng) |

Lý do không chọn B:

1. **Hai loại việc khác nhau về bản chất.**
   - `OrderService` là "danh bạ" các lệnh REST: mỗi hàm gửi đúng một request, **không giữ trạng thái gì**. Đây cũng là quy ước của dự án (`CLAUDE.md`: "Services are thin wrappers over ApiClient").
   - Kết nối thời gian thực thì **có trạng thái**: kết nối đang mở, con trỏ, vòng dự phòng, thời điểm đồng bộ cuối.
2. **Vòng đời khác nhau.**
   - Trong Blazor WASM, `OrderService` đăng ký `Scoped`, nên thực chất là **một bản sống suốt cả phiên trình duyệt**. Đặt kết nối vào đó thì nó mở mãi, kể cả khi đang ở trang Món hay Kho. Ngoài ra phải tự nghe sự kiện đăng xuất để đóng, vì token đổi.
   - `OrderRealtime` được **trang Đơn hàng tự tạo khi mở và tự hủy khi rời trang**. Đăng xuất cũng là rời trang, nên không cần nghe thêm gì.
   - **Không đăng ký vào DI**, kể cả `Transient`. Trong WASM, container giữ mọi bản `Transient` có `Dispose` tới hết ứng dụng, nên mỗi lần mở trang sẽ để sót một kết nối. Trang tạo bằng `ActivatorUtilities.CreateInstance<OrderRealtime>(Services)`: vẫn được DI đưa vào `HttpClient`, `AppState`, `OrderService`, `ILogger`, nhưng do trang làm chủ.
3. **Lỗi khác nhau.** Lỗi REST là của từng lần bấm nút, trang báo ngay tại chỗ. Lỗi kết nối là một **trạng thái kéo dài** ("đang mất kết nối"). Tách ra thì trang hiển thị hai loại này rõ ràng, và kiểm tra `OrderRealtime` (rớt mạng, chuông rơi) không phải dựng cả trang 2.564 dòng.

Phần nào **thực sự là REST** thì vẫn **gộp vào `OrderService`**: hàm `GetChangesAsync` gọi `GET don-hang/thay-doi`. `OrderRealtime` gọi hàm này, không tự gọi `ApiClient`.

#### 6.0.2. Danh sách quyết định file

| Việc cần có | Đặt ở đâu | File mới? | Lý do |
|---|---|---|---|
| Gọi `GET don-hang/thay-doi` | `Services/OrderService.cs` (thêm `GetChangesAsync`) | Không | Đúng vai trò của `OrderService` |
| `PhienBan`, `ThayDoiResponse`, `ChuongDonHang` | `Models/DonHang.cs` | Không | Cùng nhóm model đơn hàng |
| Kết nối SignalR, con trỏ, dự phòng 3 / 30 giây, trạng thái kết nối | `Services/OrderRealtime.cs` | **Có — file mới duy nhất** | Mục 6.0.1 |
| Chính sách thử kết nối lại | Lớp lồng `private` trong `OrderRealtime.cs` | Không | ~5 dòng, chỉ `OrderRealtime` dùng |
| Áp thay đổi vào bảng, banner, khung chi tiết; chỉ báo kết nối; 409 | `Pages/Orders/Orders.razor` | Không | Bảng, banner, khung chi tiết đã nằm ở đây |
| Gói `Microsoft.AspNetCore.SignalR.Client` | `CoffeeShop.Admin.csproj` | Không | |
| Kiểu chỉ báo kết nối | `wwwroot/css/app.css` (sửa `.live-toggle`), `wwwroot/index.html` (tăng `?v=`) | Không | |
| Đăng ký DI | — | **Không cần** | Trang tự tạo `OrderRealtime` (mục 6.0.1 ý 2); `Program.cs` giữ nguyên |
| Hằng mã lỗi 409 | — | **Không cần** | Trang đã xử lý theo `ex.Status == 409` rồi tải lại (`Orders.razor` dòng 1972–1980); chỉ áp cùng mẫu cho các thao tác khác |
| Thông số 3 / 30 giây | Hằng số trong `OrderRealtime` | Không | Không cần mục `Realtime` trong `appsettings.json`; đổi thì sửa một dòng |
| `OrderWatcher`, `TieuDeTrang`, JS âm báo / `visibilitychange`, lưu sessionStorage (bản 2.0) | — | **Bỏ** | Thuộc phần "tạm chưa làm"; con trỏ đã lo trường hợp F5 mà không cần lưu trạng thái |

Tổng cộng phía web: **1 file mới**, sửa **6 file** có sẵn (`csproj`, `Models/DonHang.cs`, `Services/OrderService.cs`, `Pages/Orders/Orders.razor`, `app.css`, `index.html`), cộng tài liệu.

### 6.1. Giai đoạn 0: sửa nhỏ, làm ngay (không chờ backend)

**Trạng thái: đã làm ngày 25/11/2026.**

**Mục tiêu:** trễ còn 0–3 giây và không ai tắt nhầm được việc nhận đơn. Chỉ sửa **một file** `Pages/Orders/Orders.razor`, không thêm file. Giai đoạn 3 sẽ thay hẳn cơ chế hỏi định kỳ, nên ở đây **không** đầu tư thêm.

| # | Việc | Chỗ sửa trong `Orders.razor` (số dòng trước khi sửa) |
|---|---|---|
| 0.1 | Xóa nút "Tự cập nhật" | Khối `live-toggle` (dòng 285–297), biến `autoRefreshEnabled` (dòng 1229), hàm `ToggleAutoRefresh` (dòng 1727), điều kiện `!autoRefreshEnabled` trong `AutoRefresh` (dòng 1559) |
| 0.2 | Hỏi 3 giây thay cho 10 giây | `RefreshSeconds = 3` (dòng 1125) |
| 0.3 | Cập nhật tài liệu | `docs/huong-dan/03-don-hang.md` (mục 3.6), `docs/tien-do-quan-ly-don-hang.md`, dòng "auto-refresh every 10 s" trong `CLAUDE.md` |

- Giữ nguyên `CheckNewOrders`. Với DB hiện tại, lỗi "mã lớn nhất" chỉ là lý thuyết (mục 2.6), và giai đoạn 3 thay hẳn hàm này.
- Kiểu `.realtime-controls`, `.live-toggle`, `.live-indicator` trong `app.css` giữ lại (có ghi chú trong file), vì giai đoạn 3 dùng lại cho chỉ báo kết nối.

**Xong khi:**

- [ ] Không còn nút "Tự cập nhật"; `dotnet build` không có cảnh báo mới.
- [ ] Tạo đơn không kèm token (giống tablet, qua Swagger `/docs` hoặc `CoffeeShop.Api.http`) → banner đơn mới hiện trong ≤ 3 giây.
- [ ] Đang mở hộp sửa đơn thì bảng không nhảy; đóng hộp thì lượt kế tiếp cập nhật.
- [ ] Rà soát bằng code-reviewer + ui-ux-reviewer theo quy trình của nhóm.

### 6.2. Giai đoạn 1: nền tảng backend (đề xuất cho nhóm backend)

#### 6.2.1. Mở mạng LAN

- **Để sau**, khi nhóm thống nhất môi trường chạy. Lúc làm thì sửa **cả hai** chỗ (mục 2.14):
  - `appsettings.json` dòng 2: `"Urls": "http://0.0.0.0:3000"`. Dùng khi chạy bản đã publish.
  - `Properties/launchSettings.json` dòng 9: `"applicationUrl": "http://0.0.0.0:3000"`. Dùng khi `dotnet run` hoặc Visual Studio, và bản này đè `appsettings.json`.
  - Muốn thử nhanh mà không sửa file: `dotnet run --urls http://0.0.0.0:3000`.
- Mở cổng trên máy chủ (PowerShell chạy quyền Administrator):

  ```powershell
  New-NetFirewallRule -DisplayName "CoffeeShop API 3000" -Direction Inbound -Protocol TCP -LocalPort 3000 -Action Allow -Profile Private
  ```
- Đặt IP tĩnh cho máy chủ trong router (ví dụ `192.168.1.10`). Web admin đặt `Api:BaseUrl` = `http://192.168.1.10:3000/` khi chạy thật; tablet trỏ về cùng địa chỉ.
- **Xong khi:** điện thoại trong wifi quán mở được `http://192.168.1.10:3000/docs`.

#### 6.2.2. Cơ sở dữ liệu: script `database/DongBoThoiGianThuc.sql`

```sql
-- Chỉ mục có điều kiện (WHERE …) bắt buộc 2 thiết lập này — chạy bằng: sqlcmd -S . -d QuanCaPheDB -I -i DongBoThoiGianThuc.sql
-- (giống ghi chú đã có trong QuanCaPheDB.sql)
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. Con trỏ đồng bộ + kiểm tra tranh chấp (mục 2.7, 2.11)
ALTER TABLE DonHang ADD PhienBan ROWVERSION NOT NULL;
CREATE INDEX IX_DonHang_PhienBan ON DonHang(PhienBan);

-- 2. Chống trùng đơn từ tablet (mục 2.10)
ALTER TABLE DonHang ADD MaYeuCau UNIQUEIDENTIFIER NULL,
                        BamYeuCau BINARY(32) NULL;
CREATE UNIQUE INDEX UX_DonHang_MaYeuCau ON DonHang(MaYeuCau) WHERE MaYeuCau IS NOT NULL;
```

- Cột `ROWVERSION` được gán giá trị ngay cho mọi đơn cũ, không cần cập nhật dữ liệu.
- Script phải kiểm tra trước khi thêm (`IF COL_LENGTH('DonHang','PhienBan') IS NULL …`, như `DongBoWebAdmin_20261118.sql` đang làm), để chạy lại nhiều lần không lỗi.
- SP `sp_ThanhToanVaTruKho` hiện có **2 bản** định nghĩa: `QuanCaPheDB.sql` (dòng 206) và `DongBoWebAdmin_20261118.sql` (dòng 79). Bản mới viết bằng `CREATE OR ALTER PROCEDURE` trong script này, và nhớ cập nhật cả `QuanCaPheDB.sql` để DB tạo mới cũng có bản mới.
- Sửa kèm `sp_ThanhToanVaTruKho`: nhận thêm `@PhuongThucThanhToan`, và `UPDATE DonHang SET … WHERE MaDonHang = @MaDonHang AND TrangThaiThanhToan = 'PENDING'`, `IF @@ROWCOUNT = 0` thì `THROW` lỗi "Đơn đã được thanh toán / đã hủy". Cả lần thu tiền nằm trong **một** transaction của SP (sửa H8), và `ThanhToan` không cần `SaveChanges` riêng nữa.

#### 6.2.3. EF Core và DTO

```csharp
// Data/Entities.cs — class DonHang
public ulong PhienBan { get; set; }
public Guid? MaYeuCau { get; set; }
public byte[]? BamYeuCau { get; set; }

// Data/AppDbContext.cs — OnModelCreating
b.Entity<DonHang>().Property(d => d.PhienBan)
    .IsRowVersion()                 // SQL Server tự sinh + EF dùng làm concurrency token
    .HasConversion<byte[]>();       // ulong ⇄ 8 byte big-endian, đúng thứ tự so sánh của rowversion
```

- `DonHangDto` thêm `PhienBan` dạng **chuỗi** (`d.PhienBan.ToString()`). Số 64 bit vượt giới hạn số an toàn của JavaScript / Dart nên không gửi dạng số. **Không** trả `maYeuCau` / `bamYeuCau` ra ngoài.
- Cập nhật `openapi.yaml`.

#### 6.2.4. Khóa chống trùng trong `DatMon` (thứ tự bắt buộc)

Controller đọc header và truyền vào service:

```csharp
// DonHangController — POST (thay dòng 21–23 hiện tại)
[HttpPost, AllowAnonymous]
public async Task<IActionResult> DatMon(DatMonRequest req, [FromHeader(Name = "Idempotency-Key")] Guid? khoa)
{
    var (don, taoMoi) = await service.DatMon(req, User.MaNguoiDung(), khoa);
    return taoMoi ? StatusCode(201, don.ToDto()) : Ok(don.ToDto());
}
```

```csharp
// DonHangService.DatMon — cần: using System.Security.Cryptography; using System.Text.Json;
public async Task<(DonHang Don, bool TaoMoi)> DatMon(DatMonRequest req, int? maNhanVien, Guid? khoa)
{
    byte[]? bam = khoa is null ? null : SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(req));

    // BƯỚC 1 — tìm theo khóa TRƯỚC mọi kiểm tra món / kho (DungChiTiet)
    if (khoa is Guid k && await TimTheoKhoa(k, bam) is { } cu) return (cu, false);

    // BƯỚC 2 — như cũ: DungChiTiet (kiểm tra món, nguyên liệu, tính tiền) … rồi gắn khóa
    var don = /* … code hiện tại dòng 30–40 … */;
    don.MaYeuCau = khoa; don.BamYeuCau = bam;
    db.DonHangs.Add(don);
    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateException ex) when (khoa is not null && DbErrors.LaTrungKhoa(ex))
    {
        // BƯỚC 3 — hai request cùng khóa tới cùng lúc: request kia đã thắng → trả đơn của nó
        db.ChangeTracker.Clear();
        return (await TimTheoKhoa(khoa.Value, bam)
                ?? throw new InvalidOperationException("Trùng khóa nhưng không tìm thấy đơn"), false);
    }
    var daLuu = await Tim(don.MaDonHang);               // giữ như code hiện tại: đọc lại đủ món + topping cho DTO
    await notifier.Bao(daLuu.MaDonHang, daLuu.PhienBan);  // giai đoạn 2
    return (daLuu, true);
}

private async Task<DonHang?> TimTheoKhoa(Guid khoa, byte[]? bam)
{
    // Query() là hàm có sẵn (DonHangService dòng 11–15): Include NguoiDung, NguoiThu, ChiTiets → Toppings, AsSplitQuery
    var cu = await Query().FirstOrDefaultAsync(d => d.MaYeuCau == khoa);
    if (cu is not null && !cu.BamYeuCau!.SequenceEqual(bam!))
        throw new ApiException(422, "IDEMPOTENCY_MISMATCH",
            "Mã yêu cầu này đã dùng cho một giỏ hàng khác");
    return cu;
}
```

- Gặp lại khóa thì **không kiểm tra kho nữa**. Tablet gửi lại sau khi kho vừa hết vẫn nhận đúng đơn đã tạo, không nhận nhầm 422 hết nguyên liệu.
- Không có header thì `khoa = null`, giữ hành vi cũ, nên web tạo đơn tại quầy vẫn chạy.
- Lỗi trùng khóa phải được bắt **trong `DatMon`**. Để nó rơi xuống `ErrorHandlingMiddleware` thì sẽ thành 409 "Dữ liệu bị trùng".

#### 6.2.5. `GET api/v1/don-hang/thay-doi?sau={conTro}&limit=100`

Quyền: `Authorize(Roles = VaiTros.TatCa)`. Phản hồi: `{ data: DonHangDto[], conTro: string, conNua: bool }`.

```csharp
public async Task<(List<DonHang> Data, ulong ConTro, bool ConNua)> ThayDoi(ulong? sau, int limit)
{
    // 1. ĐỌC MỐC TRƯỚC (mục 2.8)
    ulong moc = (ulong)await db.Database
        .SqlQuery<long>($"SELECT CAST(MIN_ACTIVE_ROWVERSION() AS BIGINT) AS Value")
        .SingleAsync();

    if (sau is null) return ([], moc - 1, false);       // lần đầu: chỉ lấy mốc

    // 2. RỒI MỚI ĐỌC DÒNG: sau < PhienBan < moc, tăng dần, lấy dư 1 để biết còn nữa không
    var rows = await Query()                              // cùng Include như GET don-hang → DTO đủ món + topping
        .Where(d => d.PhienBan > sau.Value && d.PhienBan < moc)
        .OrderBy(d => d.PhienBan)
        .Take(limit + 1)
        .ToListAsync();

    bool conNua = rows.Count > limit;
    if (conNua) rows.RemoveAt(limit);
    ulong conTro = conNua ? rows[^1].PhienBan : moc - 1;  // còn nữa → con trỏ = dòng cuối, KHÔNG nhảy tới mốc
    return (rows, conTro, conNua);
}
```

- Nhờ `HasConversion<byte[]>()`, các phép so sánh `>` / `<` được dịch thành so sánh nhị phân trên cột `rowversion`, có dùng chỉ mục `IX_DonHang_PhienBan`. Cần kiểm tra câu SQL sinh ra (`ToQueryString()`). Nếu EF không dịch được thì viết bằng `FromSql` với tham số `byte[8]` big-endian.
- `sau` bỏ trống: chỉ trả con trỏ ban đầu, `data` rỗng.

#### 6.2.6. Lỗi tranh chấp → 409

```csharp
// Common/ErrorHandlingMiddleware.cs — thêm TRƯỚC nhánh DbUpdateException (dòng 20)
catch (DbUpdateConcurrencyException)
{
    await Write(ctx, 409, "DON_DA_THAY_DOI", "Đơn vừa được cập nhật ở máy khác, vui lòng tải lại");
}
```

(`DbUpdateConcurrencyException` kế thừa `DbUpdateException`, nên phải bắt trước.)

#### 6.2.7. Xong khi

- [ ] Mọi đơn có `phienBan`; mỗi lần sửa, `phienBan` lớn hơn lần trước.
- [ ] `thay-doi` trả đúng các đơn vừa đổi. Giữ một transaction mở thì con trỏ dừng trước nó.
- [ ] `POST` 2 lần cùng khóa → 1 đơn (201 rồi 200); cùng khóa khác giỏ → 422.
- [ ] Hai máy cùng hủy → một bên 200, một bên 409 (không phải 500).
- [ ] `openapi.yaml` đã cập nhật.

**1b (tùy chọn): chặn ghi đè hai máy.** Các lệnh `ExecuteUpdate` (`SuaMon`, `GanSoNhanMon`, `DoiTrangThaiPhucVu`) không đi qua concurrency token. Muốn chặn hẳn thì web gửi header `If-Match: <phienBan đang thấy>`, và backend thêm `.Where(d => d.PhienBan == p)`: không dòng nào bị đổi thì trả 409. Không làm 1b thì đồng bộ thời gian thực vẫn chạy, chỉ là chưa chặn được hai người sửa cùng lúc.

### 6.3. Giai đoạn 2: kênh đẩy SignalR (đề xuất cho nhóm backend)

#### 6.3.1. `Program.cs`

```csharp
builder.Services.AddSignalR();
builder.Services.AddSingleton<DonHangNotifier>();

// Trong khối o.Events = new JwtBearerEvents { … } ĐANG CÓ (dòng 53), cạnh OnTokenValidated.
// Gán ở chỗ khác sẽ bị khối này ghi đè.
OnMessageReceived = ctx =>
{
    var token = ctx.Request.Query["access_token"];
    if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
        ctx.Token = token;
    return Task.CompletedTask;
},

// Thay dòng 84 (AllowAnyOrigin) bằng danh sách origin cụ thể
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])  // thiếu cấu hình → không origin nào được gọi (ghi log cảnh báo lúc khởi động)
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// sau app.MapControllers()
app.MapHub<DonHangHub>("/hubs/don-hang", o => o.CloseOnAuthenticationExpiration = true);
```

`appsettings.json`:

```json
"Cors": { "Origins": [ "http://localhost:5101", "https://localhost:7220", "http://192.168.1.10:5101" ] }
```

Tablet chạy Flutter Web cũng gọi API từ trình duyệt, nên origin của nó cũng phải nằm trong danh sách này. Thiếu origin nào thì máy đó bị chặn CORS.

#### 6.3.2. Hub và notifier

```csharp
[Authorize(Roles = VaiTros.TatCa)]
public class DonHangHub : Hub { }   // chỉ để máy chủ đẩy xuống, client không gọi lên

public class DonHangNotifier(IHubContext<DonHangHub> hub, ILogger<DonHangNotifier> log)
{
    /// Gọi SAU commit và CHỈ khi thật sự có ghi. Lỗi gửi không làm hỏng request: web bù bằng con trỏ.
    public async Task Bao(int maDonHang, ulong phienBan)
    {
        try
        {
            await hub.Clients.All.SendAsync("DonHangThayDoi",
                new { maDonHang, phienBan = phienBan.ToString(), gioCommit = DateTimeOffset.UtcNow });  // UTC, có múi giờ → web tính trễ đúng
        }
        catch (Exception ex) { log.LogWarning(ex, "Không đẩy được DonHangThayDoi #{Id}", maDonHang); }
    }
}
```

`DonHangService` nhận `DonHangNotifier` qua constructor.

#### 6.3.3. Bảy chỗ rung chuông

| Hàm (`DonHangService.cs`) | Dòng hiện tại | Rung sau | Lấy `PhienBan` mới |
|---|---|---|---|
| `DatMon` | 28 | `SaveChangesAsync` (chỉ khi `TaoMoi`) | từ `await Tim(id)` (mục 6.2.4) |
| `SuaMon` | 131 | `tx.CommitAsync()` (dòng 159) | các hàm này đã `return await Tim(id)` sau khi ghi → lấy `PhienBan` từ entity đó, không cần truy vấn riêng |
| `ThanhToan` | 171 | `EXEC sp_ThanhToanVaTruKho` thành công | như trên |
| `GanSoNhanMon` | 216 | `ExecuteUpdateAsync` (dòng 235) | như trên |
| `DoiTrangThaiPhucVu` | 259 | `ExecuteUpdateAsync` (dòng 279) | như trên |
| `Huy` | 292 | `SaveChangesAsync` (dòng 303) | có sẵn trên entity sau `SaveChanges` |
| `DoiPhuongThuc` | (nhánh `api-tablet-thanh-toan`) | `SaveChangesAsync` | có sẵn trên entity |

Mẫu chung: `var kq = await Tim(id); await notifier.Bao(kq.MaDonHang, kq.PhienBan); return kq;`

- `XacNhanTienMat` (controller, nhánh `api-tablet-thanh-toan`) gọi lại `service.ThanhToan`, nên **không** rung thêm.
- Hàm return sớm vì không đổi gì thì **không** rung: `GanSoNhanMon` gửi đúng số cũ, `DoiPhuongThuc` cùng phương thức, `DatMon` trả đơn cũ theo khóa.
- Có làm R6 (mục 6.5) thì thêm chỗ thứ 8.

#### 6.3.4. Bảo mật

- Hub có `[Authorize]`, nên `OnTokenValidated` hiện có (kiểm tra tài khoản còn hiệu lực) chạy khi kết nối. `CloseOnAuthenticationExpiration` đóng kết nối khi token hết hạn (`Jwt:ExpiresInHours`).
- Tài khoản bị xóa giữa ca vẫn giữ kết nối tới khi token hết hạn. Nhưng chuông chỉ có mã đơn, và lần đọc `thay-doi` kế tiếp sẽ nhận **401**, web đăng xuất. Vậy là đủ cho quán.
- Token nằm trên URL của `/hubs`: **không** hạ log `Microsoft.AspNetCore` dưới `Warning`, **không** bật HttpLogging cho `/hubs`.
- 1 máy chủ: không cần backplane. Không dùng stateful reconnect, vì con trỏ đã bù đủ.

#### 6.3.5. Xong khi

- [ ] Mở `/hubs/don-hang` không token → 401; có token NhanVien/QuanLy → kết nối được.
- [ ] Mỗi thao tác trong bảng 6.3.3 → đúng **1** sự kiện `DonHangThayDoi` (kiểm bằng console của client thử).
- [ ] Gửi lại số nhận món cũ → không có sự kiện.

### 6.4. Giai đoạn 3: web nhận đơn và thay đổi tức thời (cần giai đoạn 1 + 2)

**Điều kiện:** backend đã có `phienBan`, `GET don-hang/thay-doi`, hub `/hubs/don-hang` và lỗi 409 khi tranh chấp.

- Không có API giả, nên chạy thử bằng backend ở nhánh đã làm xong giai đoạn 1–2.
- Phần web này **chỉ được gộp vào sau khi** backend giai đoạn 1 đã lên. Giai đoạn 2 lên sau cũng được (mục 6.4.6).

#### 6.4.1. Luồng hoạt động trên trang Đơn hàng

```
Mở trang Đơn hàng (thứ tự bắt buộc)
  1. mocMaDon = mã đơn lớn nhất hiện có (GetPageAsync(1, 1)); lỗi → null    → mốc nhận diện "đơn mới"
  2. realtime = tạo OrderRealtime; await realtime.StartAsync(OnDonThayDoi, OnTrangThai)
                → lấy con trỏ ban đầu, mở kết nối hub ở nền (không chờ kết nối xong)
  3. LoadOrders()  → tải bảng như hiện nay

Máy chủ rung chuông / kết nối lại / vòng nền
  → realtime.DongBoAsync() → GET thay-doi?sau=conTro (lặp khi conNua)
  → gộp các bản trùng trong một lượt
  → gọi OnDonThayDoi(danh sách đơn vừa đổi)

Trang nhận OnDonThayDoi(ds)
  a. ghi nhận đơn mới cho banner (quy tắc ở 6.4.4)
  b. đang mở hộp sửa / hủy của một đơn trong ds → cảnh báo trong hộp
  c. không đang thao tác → LoadOrders(quiet: true) ngay (khung chi tiết cập nhật theo)
     đang thao tác        → đánh dấu canTaiLai, tải lại ngay khi rảnh

Rời trang / đăng xuất → trang gọi realtime.DisposeAsync() → đóng kết nối, dừng vòng nền
```

**Vì sao trang tải lại bảng (bước c) thay vì tự chèn / sửa từng dòng:**

- Bảng có bộ lọc trạng thái, thời gian, tìm kiếm, sắp xếp, phân trang và tổng số đơn. Tự chèn đơn vào bảng thì phải viết lại toàn bộ logic lọc đó ở phía web, rất dễ lệch với máy chủ.
- Tải lại trang đang xem chỉ là **1 request** (~50 ms trong LAN), dùng lại `LoadOrders(quiet: true)` có sẵn. Request này không bật vòng xoay, luôn đúng bộ lọc và tổng số, và `RefreshSelected` bên trong nó đã cập nhật khung chi tiết cùng ô số nhận món.
- Dữ liệu từ `thay-doi` vẫn được dùng cho những việc không phụ thuộc bộ lọc: banner đơn mới và cảnh báo trong hộp thoại.
- Máy tự thao tác cũng nhận chuông của chính mình nên tải bảng thêm một lần. Như vậy thừa 1 request nhưng không sai, nên chấp nhận để code đơn giản.

#### 6.4.2. File và việc cụ thể

| # | File | Việc |
|---|---|---|
| 3.1 | `CoffeeShop.Admin.csproj` | Thêm gói `Microsoft.AspNetCore.SignalR.Client`, cùng đời .NET 8 với các gói đang có (đang là 8.0.33; kiểm tra bản 8.0.x mới nhất có trên NuGet) |
| 3.2 | `Models/DonHang.cs` | `DonHang.PhienBan` (`string?`); thêm `ThayDoiResponse`, `ChuongDonHang` |
| 3.3 | `Services/OrderService.cs` | Thêm `GetChangesAsync(string? sau, ct)` |
| 3.4 | `Services/OrderRealtime.cs` (**mới**) | Mục 6.4.3 |
| 3.5 | `Pages/Orders/Orders.razor` | Mục 6.4.4 |
| 3.6 | `wwwroot/css/app.css`, `wwwroot/index.html` | Chỉnh `.live-toggle` thành chỉ báo kết nối (không bấm được, 3 trạng thái); tăng `?v=` |
| 3.7 | Tài liệu | `docs/huong-dan/03-don-hang.md`, `docs/tien-do-quan-ly-don-hang.md`, `CLAUDE.md` (luồng "auto-refresh") |

`Program.cs` **không sửa** (mục 6.0.1 ý 2).

Model (3.2) và service (3.3):

```csharp
// Models/DonHang.cs
public class DonHang { /* … */ public string? PhienBan { get; set; } }   // backend gửi dạng chuỗi số

/// <summary>GET api/v1/don-hang/thay-doi</summary>
public class ThayDoiResponse
{
    public List<DonHang> Data { get; set; } = [];
    public string ConTro { get; set; } = "0";
    public bool ConNua { get; set; }
}

/// <summary>Sự kiện SignalR "DonHangThayDoi" — chỉ là chuông, không chở dữ liệu đơn.</summary>
public record ChuongDonHang(int MaDonHang, string PhienBan, DateTimeOffset GioCommit);

// Services/OrderService.cs
/// <summary>Các đơn đã đổi sau con trỏ <paramref name="sau"/>; null = chỉ lấy con trỏ ban đầu.</summary>
public Task<ThayDoiResponse> GetChangesAsync(string? sau, CancellationToken ct = default) =>
    _api.GetAsync<ThayDoiResponse>(Query.Build("api/v1/don-hang/thay-doi", ("sau", sau), ("limit", 100)), ct);
```

#### 6.4.3. `Services/OrderRealtime.cs`

```csharp
using System.Net;
using CoffeeShop.Admin.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace CoffeeShop.Admin.Services;

public enum TrangThaiKetNoi { TrucTiep, DoDinhKy, MatKetNoi }

/// <summary>
/// Nhận thay đổi đơn hàng gần như tức thời: SignalR rung chuông → đọc lại từ DB theo con trỏ phienBan.
/// Trang Đơn hàng tự tạo (ActivatorUtilities) và tự DisposeAsync khi rời trang — KHÔNG đăng ký DI.
/// </summary>
public sealed class OrderRealtime(HttpClient http, AppState app, OrderService orders, ILogger<OrderRealtime> log)
    : IAsyncDisposable
{
    private static readonly TimeSpan NhipNen = TimeSpan.FromSeconds(3);      // vòng nền
    private static readonly TimeSpan DuPhong = TimeSpan.FromSeconds(30);     // có kết nối: bắt chuông bị rơi
    private static readonly TimeSpan ConTuoi = TimeSpan.FromSeconds(10);     // REST còn chạy nếu thành công trong 10 giây qua

    private readonly CancellationTokenSource cts = new();   // chỉ Cancel, không Dispose → không bao giờ ObjectDisposedException
    private HubConnection? hub;
    private Func<IReadOnlyList<DonHang>, Task>? onThayDoi;
    private Action? onTrangThai;
    private ulong? conTro;                                    // null = chưa lấy được con trỏ ban đầu
    private ulong chuongLonNhat;                              // phienBan lớn nhất mà chuông đã báo
    private int lanChoChuong;
    private DateTime lanThanhCong = DateTime.MinValue;        // lần đọc thay-doi thành công cuối
    private TrangThaiKetNoi daBao = TrangThaiKetNoi.MatKetNoi;
    private bool dangDongBo, canChayThem, dangDung;

    public TrangThaiKetNoi TrangThai =>
        hub?.State == HubConnectionState.Connected ? TrangThaiKetNoi.TrucTiep
        : DateTime.UtcNow - lanThanhCong < ConTuoi ? TrangThaiKetNoi.DoDinhKy
        : TrangThaiKetNoi.MatKetNoi;

    /// <param name="onThayDoi">Danh sách đơn vừa đổi. Danh sách RỖNG = "hãy tải lại bảng" (vừa lấy lại được con trỏ).</param>
    /// <param name="onTrangThai">Trạng thái kết nối vừa đổi (vẽ lại chỉ báo).</param>
    public async Task StartAsync(Func<IReadOnlyList<DonHang>, Task> onThayDoi, Action onTrangThai)
    {
        if (dangDung) return;
        (this.onThayDoi, this.onTrangThai) = (onThayDoi, onTrangThai);

        await LayConTroAsync();                               // lỗi mạng → vòng nền lấy lại sau
        if (dangDung) return;                                 // rời trang trong lúc chờ

        hub = new HubConnectionBuilder()
            .WithUrl(new Uri(http.BaseAddress!, "hubs/don-hang"),
                     o => o.AccessTokenProvider = () => Task.FromResult(app.Token))   // token HIỆN TẠI
            .WithAutomaticReconnect(new ThuMaiMai())
            .Build();

        // Handler trả về ngay: SignalR chờ handler xong mới đọc tin kế tiếp, nên không chờ đồng bộ ở đây
        hub.On<ChuongDonHang>("DonHangThayDoi", c =>
        {
            log.LogInformation("Chuông đơn #{Id}, trễ {Ms:F0} ms", c.MaDonHang, (DateTimeOffset.UtcNow - c.GioCommit).TotalMilliseconds);
            chuongLonNhat = Math.Max(chuongLonNhat, ulong.Parse(c.PhienBan));
            _ = DongBoAsync();
            return Task.CompletedTask;
        });
        hub.Reconnecting += _ => { BaoTrangThai(); return Task.CompletedTask; };
        hub.Reconnected  += _ => { BaoTrangThai(); _ = DongBoAsync(); return Task.CompletedTask; };
        hub.Closed       += _ =>
        {
            if (dangDung) return Task.CompletedTask;          // chính mình đóng (rời trang) → không mở lại
            BaoTrangThai();
            _ = KetNoiAsync();
            return Task.CompletedTask;
        };

        _ = KetNoiAsync();
        _ = VongNenAsync();
    }

    /// <summary>Đọc mọi thay đổi sau con trỏ. Chỉ 1 lượt chạy một lúc; gọi trong lúc đang chạy → chạy thêm đúng 1 lượt.</summary>
    public async Task DongBoAsync()
    {
        if (dangDung) return;
        if (dangDongBo) { canChayThem = true; return; }
        dangDongBo = true;
        try
        {
            do
            {
                canChayThem = false;
                if (conTro is null) { if (await LayConTroAsync()) await Phat([]); continue; }

                var doi = new Dictionary<int, DonHang>();     // cùng đơn xuất hiện 2 lần (lượt có conNua) → giữ bản sau, mới hơn
                ThayDoiResponse r;
                do
                {
                    r = await orders.GetChangesAsync(conTro.ToString(), cts.Token);
                    foreach (var d in r.Data) doi[d.MaDonHang] = d;
                    conTro = Math.Max(conTro.Value, ulong.Parse(r.ConTro));   // chỉ tăng, không lùi
                } while (r.ConNua);

                lanThanhCong = DateTime.UtcNow;
                if (doi.Count > 0) await Phat([.. doi.Values]);

                // Chuông báo bản mới hơn con trỏ: một transaction khác còn giữ mốc (mục 2.8) → đọc lại sau 300 ms, tối đa 3 lần
                if (chuongLonNhat > conTro && lanChoChuong < 3)
                {
                    lanChoChuong++;
                    await Task.Delay(300, cts.Token);
                    canChayThem = true;
                }
                else lanChoChuong = 0;                        // đã đủ, hoặc để vòng nền bắt
            } while (canChayThem && !dangDung);
        }
        catch (OperationCanceledException) { }
        catch (ApiException ex) { log.LogWarning("Không đồng bộ được đơn hàng: {Code} {Status}", ex.Code, ex.Status); }
        catch (Exception ex) { log.LogError(ex, "Lỗi khi đồng bộ đơn hàng"); }  // không để vòng nền chết im lặng
        finally { dangDongBo = false; BaoTrangThai(); }
    }

    // Vòng nền 3 giây: chưa có kết nối trực tiếp → đồng bộ mỗi nhịp; có kết nối → 30 giây một lần
    private async Task VongNenAsync()
    {
        using var timer = new PeriodicTimer(NhipNen);
        try
        {
            while (await timer.WaitForNextTickAsync(cts.Token))
            {
                if (TrangThai != TrangThaiKetNoi.TrucTiep || DateTime.UtcNow - lanThanhCong >= DuPhong)
                    await DongBoAsync();                      // DongBoAsync tự bắt mọi lỗi
                BaoTrangThai();
            }
        }
        catch (OperationCanceledException) { }
    }

    // Lần StartAsync đầu KHÔNG được WithAutomaticReconnect thử lại → tự thử, lùi dần, không bỏ cuộc.
    // 404 (backend chưa có hub) hay 401 đều chỉ thử lại chậm: dữ liệu vẫn về qua vòng nền,
    // còn 401 thì lời gọi REST thay-doi nhận 401 → AuthHeaderHandler đăng xuất → trang bị hủy → DisposeAsync.
    private async Task KetNoiAsync()
    {
        for (int lan = 0; !dangDung; lan++)
        {
            try
            {
                await hub!.StartAsync(cts.Token);
                BaoTrangThai();
                _ = DongBoAsync();                            // bù khoảng hở trước khi kết nối xong
                return;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                log.LogInformation("Chưa kết nối được kênh đơn hàng: {Message}", ex.Message);
                try { await Task.Delay(ThuMaiMai.Cho(lan), cts.Token); } catch (OperationCanceledException) { return; }
            }
        }
    }

    private async Task<bool> LayConTroAsync()
    {
        try { conTro = ulong.Parse((await orders.GetChangesAsync(null, cts.Token)).ConTro); return true; }
        catch (OperationCanceledException) { return false; }
        catch (ApiException ex) { log.LogWarning("Chưa lấy được con trỏ: {Code}", ex.Code); return false; }
    }

    private Task Phat(IReadOnlyList<DonHang> ds) => onThayDoi?.Invoke(ds) ?? Task.CompletedTask;

    private void BaoTrangThai()
    {
        if (dangDung || TrangThai == daBao) return;
        daBao = TrangThai;
        onTrangThai?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (dangDung) return;                                 // gọi nhiều lần vẫn an toàn
        dangDung = true;                                      // đặt TRƯỚC khi đóng → Closed không mở lại
        (onThayDoi, onTrangThai) = (null, null);              // không gọi vào trang đã hủy
        cts.Cancel();
        if (hub is not null) await hub.DisposeAsync();
    }

    /// <summary>0, 2, 5, 10, 30, 30… giây, không bỏ cuộc.</summary>
    private sealed class ThuMaiMai : IRetryPolicy
    {
        private static readonly int[] Giay = [0, 2, 5, 10, 30];
        public static TimeSpan Cho(int lan) => TimeSpan.FromSeconds(Giay[Math.Min(lan, Giay.Length - 1)]);
        public TimeSpan? NextRetryDelay(RetryContext ctx) => Cho((int)ctx.PreviousRetryCount);
    }
}
```

Ghi chú:

- Blazor WASM chạy một luồng, nên các cờ `dangDongBo` / `canChayThem` không cần khóa.
- **Không có nhánh xử lý 401 riêng.** Token hết hạn hay tài khoản bị xóa thì lời gọi REST `thay-doi` nhận 401, và `AuthHeaderHandler` đang có sẵn sẽ đăng xuất (có kiểm tra đúng phiên). Thời gian phát hiện: ≤ 3 giây khi không có kết nối trực tiếp; khi có thì ≤ 30 giây, và backend đóng hub lúc token hết hạn (`CloseOnAuthenticationExpiration`) nên thực tế cũng về mức 3 giây.
- **Không cần "bỏ bản cũ theo phienBan".** Máy chủ chỉ trả các dòng mới hơn con trỏ, và con trỏ chỉ tăng. Chỉ cần gộp trường hợp cùng một đơn xuất hiện 2 lần trong một lượt dài.
- **F5 không cần lưu gì.** Mở lại trang thì lấy mốc + con trỏ mới và tải lại bảng; đơn đã có trong bảng. Banner chỉ báo đơn tới sau lúc mở trang, giống hành vi hiện nay.
- Mức log mặc định của WASM là Information, nên dòng "trễ … ms" hiện trong Console để đo (mục 9.1).

#### 6.4.4. `Pages/Orders/Orders.razor`: thay đổi cụ thể

1. **Bỏ cơ chế hỏi định kỳ cũ:**
   - Xóa `CheckNewOrders`, `NewOrderScan`, `scanTask`, `unfilteredFirstPage`, đoạn `pageCoversNew` (dòng 1488–1494) và `newOrdersInitialized`.
   - Giữ `Timer` 3 giây, nhưng `AutoRefresh` **không gọi máy chủ** nữa. Nó chỉ làm một việc: `if (canTaiLai && !DangThaoTac) { canTaiLai = false; await LoadOrders(quiet: true); }`.
   - `DangThaoTac` gom đúng các điều kiện đang có ở dòng 1559 (đang tải, đang thu tiền, đang tạo, đang hủy, đang mở hộp thoại…).
2. **Khởi động** trong `OnInitializedAsync`, đúng thứ tự mục 6.4.1. Thêm `@inject IServiceProvider Services`; dòng 3 đã có `@using Microsoft.Extensions.DependencyInjection`.

   ```csharp
   mocMaDon = await LayMocMaDonAsync();                     // int?; lỗi → null
   if (pageCts.IsCancellationRequested) return;             // rời trang trong lúc chờ (giống dòng 1378)
   realtime = ActivatorUtilities.CreateInstance<OrderRealtime>(Services);
   await realtime.StartAsync(OnDonThayDoi, () => _ = InvokeAsync(StateHasChanged));
   if (pageCts.IsCancellationRequested) return;
   await LoadOrders();                                      // như hiện nay
   ```
3. **`OnDonThayDoi(ds)`** chạy trong `InvokeAsync`:

   ```csharp
   if (mocMaDon is null) mocMaDon = await LayMocMaDonAsync();   // máy chủ vừa có lại (thường đi kèm ds rỗng)
   foreach (var d in ds) GhiNhanDonMoi(d);
   if (DangMoHopCuaDon(ds)) donVuaDoiOMayKhac = true;           // hộp sửa / hủy: cảnh báo, KHÔNG thay dữ liệu đang sửa
   if (DangThaoTac) canTaiLai = true; else await LoadOrders(quiet: true);   // RefreshSelected cập nhật khung chi tiết + ô số nhận món
   StateHasChanged();
   ```
   Cảnh báo trong hộp: "Đơn vừa được cập nhật ở máy khác. Đóng hộp để xem bản mới."
4. **Quy tắc "đơn mới"** `GhiNhanDonMoi(d)`, áp cho dữ liệu từ `thay-doi` **và** cho danh sách mỗi lần `LoadOrders`, theo đúng thứ tự:
   1. Đơn **đang có trong banner** (`newOrderIds.Contains(id)`): cập nhật `newOrderCache[id] = d`. Nếu `!d.DangPhucVu` (đã phục vụ / hủy ở máy khác) thì bỏ khỏi banner.
   2. Ngược lại, đơn mới khi đủ cả 4 điều kiện:
      - `mocMaDon is not null`: chưa có mốc thì không báo gì, tránh báo nhầm hàng loạt khi máy chủ vừa có lại;
      - `d.MaNguoiDung is null`: đơn từ tablet;
      - `d.MaDonHang > mocMaDon`: tạo sau lúc mở trang;
      - `d.DangPhucVu && daBao.Add(id)`: còn cần xử lý và chưa từng báo.
      
      Đủ điều kiện thì thêm vào `newOrderIds`, `newOrderCache`, và đặt `newBannerHidden = false`.
   - Mở xem đơn (`SelectOrder`) vẫn bỏ đơn khỏi banner như hiện nay. `daBao` giữ mã đó nên đơn không quay lại banner.
   - Vì sao `mocMaDon` là đủ, không cần "tập mã đã thấy" của bản 2.0: con trỏ đã bảo đảm **không bỏ sót thay đổi nào**. `mocMaDon` chỉ để phân biệt "đơn tạo sau khi mở trang" với "đơn cũ vừa được sửa". Đơn tại quầy có `MaNguoiDung` nên không bao giờ bị báo.
   - Khe hở còn lại: một đơn được tạo **đúng trong vài mili giây** giữa bước lấy `mocMaDon` và bước lấy con trỏ. Đơn đó vẫn hiện trong bảng (bước `LoadOrders`) và vẫn được báo nếu đang xem trang 1, nên chấp nhận được.
5. **Chỉ báo kết nối** đặt ở chỗ nút "Tự cập nhật" cũ. Dùng lại kiểu `.live-toggle` nhưng là `<span role="status">`, không phải nút, và **không có nút "Thử lại"** vì vòng nền đã tự thử 3 giây một lần:

   | `realtime.TrangThai` | Chữ hiển thị |
   |---|---|
   | `TrucTiep` | "● Đang nhận đơn trực tiếp" |
   | `DoDinhKy` | "Đang cập nhật mỗi 3 giây" (REST chạy, chưa có kênh trực tiếp) |
   | `MatKetNoi` | "Mất kết nối máy chủ, đang thử lại…" |
6. **Lỗi 409:** áp cùng mẫu đang có ở thu tiền (dòng 1972–1980: hiện `ex.Message`, nếu `ex.Status == 409` thì `LoadOrders(quiet: true)`) cho hủy, sửa món, đổi phục vụ và số nhận món. `ApiClient` đã dùng câu tiếng Việt của máy chủ cho 409.
7. **Giải phóng:** đổi `@implements IDisposable` thành `@implements IAsyncDisposable`. Trong `DisposeAsync`: `if (realtime is not null) await realtime.DisposeAsync();`, rồi dispose `Timer` / `CancellationTokenSource` như hiện nay. Callback được truyền vào `StartAsync`, nên không có sự kiện nào phải hủy đăng ký.
8. (Tùy chọn) Nếu thấy bảng nháy khi tải lại, thêm `@key="order.MaDonHang"` cho dòng bảng.

#### 6.4.5. Một thao tác ở máy A hiện ở máy B (ví dụ thu tiền)

| Thời điểm | Bước |
|---|---|
| 0 ms | Máy A bấm "Thu tiền" → `POST thanh-toan` |
| ~40 ms | Backend: SP thu tiền + trừ kho, commit → chuông `DonHangThayDoi` |
| ~60 ms | Máy B nhận chuông → `GET thay-doi` → có đơn với `phienBan` mới |
| ~110 ms | Máy B tải lại trang bảng đang xem (1 request) → bảng và khung chi tiết hiện "Đã thu" |

Nếu máy B đang mở hộp sửa đơn đó thì hộp hiện cảnh báo. Bấm Lưu thì nhận lỗi từ máy chủ (409, hoặc "đơn đã thanh toán"), không ghi đè.

#### 6.4.6. Khi backend thiếu một phần

| Backend | Web chạy thế nào |
|---|---|
| Có cả `thay-doi` và hub | Trễ < 1 giây; dự phòng 30 giây; chỉ báo "● Đang nhận đơn trực tiếp" |
| Có `thay-doi`, chưa có hub (404) | Vẫn chạy: vòng nền đồng bộ **3 giây** một lần, chỉ báo "Đang cập nhật mỗi 3 giây". Không cần code riêng. Chrome vẫn in đỏ request `negotiate` 404 (khoảng 30 giây một lần) trong tab Network / Console; điều này bình thường, không phải lỗi .NET |
| Chưa có `thay-doi` | **Không hỗ trợ.** Chỉ gộp giai đoạn 3 sau khi backend giai đoạn 1 đã lên |

#### 6.4.7. Xong khi

- [ ] Tablet đặt đơn → banner + bảng ở máy quầy cập nhật < 1 giây (Console có dòng "Chuông đơn #…, trễ … ms").
- [ ] Máy A thu tiền / hủy / đổi phục vụ / đổi số nhận món / sửa món → máy B thấy < 1 giây.
- [ ] Đang mở hộp sửa đơn #X ở máy B, máy A thu tiền #X → hộp ở B hiện cảnh báo; bảng không nhảy dưới tay.
- [ ] Hai máy cùng hủy một đơn → một máy thành công, máy kia hiện thông báo 409 rõ ràng.
- [ ] Tắt backend 2 phút rồi bật → chỉ báo chuyển "Mất kết nối…" rồi tự trở lại "Đang nhận đơn trực tiếp", không cần F5, không mất đơn, không báo nhầm đơn cũ là đơn mới.
- [ ] Mở trang khi backend đang tắt, rồi bật backend → bảng tự tải, banner không báo nhầm.
- [ ] Rời trang Đơn hàng / đăng xuất → tab Network không còn request `thay-doi` hay kết nối hub nào.
- [ ] Backend chỉ có `thay-doi` (tắt hub) → web vẫn cập nhật trong ≤ 3 giây, không có thanh lỗi Blazor.
- [ ] Rà soát bằng code-reviewer + ui-ux-reviewer.

### 6.5. Giai đoạn 4: tablet (đề xuất cho nhóm Flutter, kèm R6, R7 cho backend)

- **Khóa chống trùng:**
  - Mỗi giỏ sinh **một** GUID ở lần bấm "Đặt" đầu tiên (gói `uuid`), giữ nguyên qua mọi lần gửi lại.
  - Lưu khóa vào `shared_preferences`, để app bị tắt giữa chừng vẫn gửi lại đúng khóa.
  - **Khóa giỏ** (không cho sửa) khi kết quả chưa rõ.
- **Gửi lại có lùi dần:**
  - Khi hết thời gian chờ (10 giây) hoặc lỗi mạng: thử lại sau 1, 2, 4, 8 giây…, tối đa ~30 giây giữa hai lần.
  - Lỗi 4xx (hết nguyên liệu, món ngừng bán, 422 `IDEMPOTENCY_MISMATCH`): **không** gửi lại, báo khách.
- **Giao diện:**
  - Nút Đặt bị khóa khi đang gửi, hiện "Đang gửi đơn…".
  - Chỉ hiện "Đặt thành công" khi đã có `maDonHang`.
  - Mất mạng quá 60 giây: hiện "Chưa gửi được, vui lòng gọi nhân viên" và giữ giỏ.
- **R6 — sửa giỏ sau khi đã có đơn** (backend thêm `PUT api/v1/don-hang/{id}/thay-the`): endpoint công khai, bắt buộc header `Idempotency-Key` bằng khóa của đơn đó, đơn phải còn PENDING và chưa phục vụ. Nó thay món của chính đơn đó, dùng lại logic `SuaMon`, thay vì tạo đơn mới. Chưa có R6 thì tối thiểu tablet phải hủy đơn cũ trước khi tạo đơn mới.
- **R7 — bảo vệ endpoint công khai:** `PUT …/phuong-thuc-thanh-toan`, `GET …/trang-thai` và R6 **bắt buộc** header `Idempotency-Key` khớp `MaYeuCau` của đơn. Hiện mã đơn tăng dần, nên ai trong LAN cũng đổi được đơn người khác.
- Giữ nguyên cách hỏi `GET trang-thai` 3 giây. Đẩy xuống tablet để ngoài phạm vi, vì gói `signalr_netcore` không chạy trên Flutter Web.

**Xong khi:** T4, T4b, T4c, T13 (mục 9) đạt.

### 6.6. Bảng tổng hợp giai đoạn

| Giai đoạn | Ai làm | Phụ thuộc | Kết quả |
|---|---|---|---|
| **0. Sửa nhỏ phía web** | Web admin (chỉ `Orders.razor`) | Không | Trễ 0–3 giây, không còn tắt nhầm việc nhận đơn |
| **1. Nền tảng backend** | Backend | Duyệt mục 6.2 | Có con trỏ, chống trùng, 409 thay cho 500. Phần mở mạng LAN / IP tĩnh để sau |
| 1b. Chặn ghi đè (tùy chọn) | Backend + Web | 1 | Hai máy không ghi đè nhau |
| **2. Kênh đẩy** | Backend | 1 | Chuông < 100 ms sau commit |
| **3. Web dùng kênh đẩy** | Web admin (1 file mới + 6 file sửa) | 1 (chạy dò 3 giây), 2 (dưới 1 giây) | Đơn mới và mọi thao tác hiện ở máy khác < 1 giây |
| **4. Tablet chống trùng** | Flutter (+ backend R6, R7) | 1 | Không mất, không trùng, không đơn treo |
| **5. Kiểm thử + đo** | Cả 3 nhóm | 1–4 | Đạt chỉ tiêu mục 1.2 |

Thứ tự: **0 làm ngay**. Đường chính là 1 → 2 → 3. Giai đoạn 4 làm song song với 2–3, sau khi xong giai đoạn 1.

---

## 7. Phân tích các tình huống lỗi

| Tình huống | Điều xảy ra | Vì sao vẫn đúng |
|---|---|---|
| Tablet gửi, mạng rớt trước khi nhận phản hồi | Tablet gửi lại cùng khóa | Máy chủ tìm theo khóa **trước** khi kiểm tra kho → 200 + đơn cũ |
| Hai lần gửi cùng khóa đến cùng lúc | Lần sau đụng chỉ mục duy nhất | `DatMon` bắt lỗi trùng khóa, đọc lại đơn → 200 |
| Cùng khóa nhưng giỏ khác | Băm nội dung khác | 422 `IDEMPOTENCY_MISMATCH` |
| Khách sửa giỏ sau khi đã đặt | R6 thay món của đơn cũ | Không có đơn treo PENDING |
| Commit xong, rung chuông lỗi | Ghi log, request vẫn thành công | Web bắt được trong ≤ 30 giây (dự phòng) hoặc ở lượt đồng bộ khác; đơn mới tính từ dữ liệu nên vẫn lên banner |
| Hai transaction chồng nhau, cái lấy số nhỏ commit sau | Mốc `MIN_ACTIVE_ROWVERSION` đọc trước → con trỏ dừng trước nó | Lượt sau lấy được cả hai (mục 2.8) |
| Chuông đến khi mốc còn bị giữ | `phienBanChuong > conTro` | Đọc lại sau 300 ms, rồi để vòng nền bắt |
| Thay đổi xảy ra giữa lúc lấy con trỏ đầu và lúc hub kết nối xong | Không có chuông cho nó | `DongBoAsync()` chạy ngay sau `StartAsync` |
| Mở trang khi máy chủ đang tắt | Chưa lấy được mốc mã đơn và con trỏ | Vòng nền thử lại 3 giây một lần; lấy được con trỏ thì trang lấy lại mốc rồi tải lại bảng. Chưa có mốc thì banner không báo gì, nên không báo nhầm đơn cũ |
| Máy A thao tác khi máy B đang mở hộp sửa cùng đơn | B nhận thay đổi | Hộp ở B hiện cảnh báo, không thay dữ liệu dưới tay; B bấm Lưu thì nhận 409 / lỗi trạng thái |
| Wifi máy quầy mất 1 phút | Chỉ báo chuyển sang "Mất kết nối", dò 3 giây, hub thử lại mãi | Có mạng lại → `Reconnected` → đồng bộ theo con trỏ |
| Máy chủ khởi động lại | Kết nối rớt; lâu quá thì `Closed` → tự khởi động lại | Con trỏ là số trong DB, không mất |
| Tin lộn thứ tự / trùng | Ví dụ "Đã thu" đến trước "Tạo" | Chuông chỉ kích hoạt việc đọc; máy chủ chỉ trả bản mới hơn con trỏ và con trỏ chỉ tăng; dữ liệu luôn đọc từ DB |
| Hai máy cùng thu tiền một đơn | SP có `WHERE … PENDING` → một máy thành công | Máy kia nhận lỗi và thấy "Đã thu" < 1 giây |
| Hai máy cùng hủy / đổi phương thức | `DbUpdateConcurrencyException` | 409 `DON_DA_THAY_DOI`, web tải lại |
| Tab admin bị ẩn lâu | Hẹn giờ bị làm chậm | WebSocket vẫn nhận chuông, và lời gọi đồng bộ chạy ngay khi chuông tới |
| Máy admin ngủ rồi thức | Kết nối cũ đã chết | SignalR phát hiện trong ~30 giây → kết nối lại → đồng bộ theo con trỏ |
| Token hết hạn | Hub đóng kết nối; REST trả 401 | Web đăng xuất như hiện nay |
| Tài khoản bị xóa giữa ca | Kết nối hub còn tới khi token hết hạn | Chuông chỉ có mã đơn; lần đọc `thay-doi` kế tiếp nhận 401 → đăng xuất |
| Mạng chặn WebSocket | SignalR tự dùng SSE / long polling | Vẫn khoảng 1–2 giây |
| F5 / rời trang rồi quay lại | `OrderRealtime` cũ bị hủy, bản mới lấy con trỏ mới | Bảng được tải lại từ DB nên có đủ đơn; không cần lưu trạng thái nào |
| Rời trang / đăng xuất | `DisposeAsync` đóng hub → `Closed` | Cờ `dangDung` đặt trước nên không tự mở lại |

---

## 8. Rủi ro và cách giảm

| Rủi ro | Mức | Cách giảm |
|---|---|---|
| Backend chậm làm giai đoạn 1–2 | Cao | Giai đoạn 0 đã đưa trễ về ≤ 3 giây và không cần backend |
| EF không dịch được phép so sánh `ulong` rowversion | Thấp | Kiểm `ToQueryString()`; dự phòng bằng `FromSql` |
| Chủ quán đang ở trang khác (Tổng quan, Kho…) không thấy đơn mới | Thấp | Nhân viên trực quầy chỉ có trang Đơn hàng. Số đếm ở menu có thể thêm sau (đưa `OrderRealtime` lên `MainLayout`) |
| Sửa `Urls` mà quên `launchSettings.json` | Trung bình | Mục 6.2.1 ghi rõ cả hai chỗ; kiểm tra bằng điện thoại trong wifi |
| Đổi CORS làm hỏng máy đang chạy | Trung bình | Liệt kê đủ origin (localhost, IP LAN, tablet) trước khi bỏ `AllowAnyOrigin` |
| Token lộ qua log URL `/hubs` | Thấp | Giữ log `Warning`, không bật HttpLogging cho `/hubs` |

---

## 9. Kiểm thử và tiêu chí nghiệm thu

### 9.1. Đo độ trễ

- Chuông mang `gioCommit` (giờ UTC của máy chủ). Khi nhận chuông, `OrderRealtime` ghi log `DateTimeOffset.UtcNow − gioCommit`. Cộng thêm thời gian tải lại bảng (xem tab Network) thì ra độ trễ tới lúc hiển thị. Máy chủ và máy quầy phải đồng bộ giờ Windows (`w32tm /resync`).
- Đo thêm phía tablet: từ lúc bấm Đặt tới lúc nhận phản hồi.
- Chạy 50 đơn trong mạng quán. Yêu cầu: **p95 tổng (tablet bấm → quầy báo) < 1 giây**.

### 9.2. Kịch bản

| # | Kịch bản | Kỳ vọng | Giai đoạn |
|---|---|---|---|
| T1 | Tablet đặt 1 đơn; 2 máy admin cùng mở trang Đơn hàng (một máy đang lọc "Đã phục vụ") | Cả 2 có banner đơn mới (≤ 3 giây ở GĐ 0, < 1 giây ở GĐ 3); máy đang lọc không bị chèn đơn sai bộ lọc vào bảng | 0, 3 |
| T2 | Ngắt mạng máy admin 60 giây; trong lúc đó tablet đặt 3 đơn; nối lại | Chỉ báo "Mất kết nối"; nối lại ≤ 2–3 giây có đủ 3 đơn trên banner + bảng, không trùng | 0, 3 |
| T3 | Tắt backend 2 phút khi admin đang mở | Tự kết nối lại không cần F5, không mất đơn | 0, 3 |
| T4 | Chặn phản hồi `POST` (tắt mạng ngay sau khi gửi), tablet gửi lại | Chỉ 1 đơn trong DB | 4 |
| T4b | Như T4, nhưng trong lúc đó làm hết nguyên liệu của món | Lần gửi lại nhận 200 + đơn cũ, không nhận 422 | 4 |
| T4c | Bắn 2 request cùng khóa đồng thời | 1 đơn; một request nhận 201, request kia 200 | 1 |
| T5 | Tắt gửi trong `DonHangNotifier` (giả lập lỗi) | Đơn vẫn hiện trên banner + bảng ≤ 30 giây | 3 |
| T6 | Giữ một transaction mở 5 giây trong SSMS (`BEGIN TRAN; INSERT DonHang (MaNguoiDung) VALUES (NULL); WAITFOR DELAY '00:00:05'; COMMIT;` — mọi cột NOT NULL khác có giá trị mặc định); trong lúc đó tablet đặt đơn | Không mất đơn nào: đơn của tablet hiện sau khi transaction kia commit (con trỏ dừng trước transaction đang mở, mục 2.8). Hủy đơn thử 0 đ sau khi test | 3 |
| T7 | Máy A thu tiền đơn #X, máy B đang mở chi tiết #X | B thấy "Đã thu" < 1 giây; đang mở hộp sửa / hủy #X thì được cảnh báo | 3 |
| T8 | Máy A và B cùng hủy đơn #X | Một bên thành công, bên kia 409 với thông báo rõ | 1 |
| T9 | Xóa tài khoản nhân viên đang đăng nhập | Lượt đọc kế tiếp nhận 401 → đăng xuất | 3 |
| T10 | Tab admin ẩn 10 phút, tablet đặt đơn, rồi mở lại tab | Đơn đã có sẵn trên banner + bảng: GĐ 0 trong ≤ ~60 giây (Chrome làm chậm hẹn giờ, mục 2.13), GĐ 3 < 1 giây | 0, 3 |
| T11 | Backend có `thay-doi` nhưng tắt hub | Web vẫn cập nhật trong ≤ 3 giây, chỉ báo "Đang cập nhật mỗi 3 giây", không có thanh lỗi Blazor (request `negotiate` 404 màu đỏ trong Network là bình thường) | 3 |
| T12 | 20 đơn trong 1 phút | Không đơ, không mất, thứ tự đúng | 0, 3 |
| T13 | Khách sửa giỏ sau khi đã đặt | Vẫn 1 đơn, món đã cập nhật | 4 |
| T14 | Điện thoại chủ quán vào web qua wifi quán | Kết nối được (địa chỉ LAN, CORS đúng origin) | 1, 2 |
| T15 | Đơn cũ (tạo trước lúc mở trang) được máy khác thu tiền | Đơn đó cập nhật trong bảng, **không** bị báo là đơn mới | 3 |
| T16 | Vào / ra trang Đơn hàng 5 lần, rồi đăng xuất | Mỗi lúc chỉ có 1 kết nối hub; ra khỏi trang hoặc đăng xuất thì không còn request `thay-doi` nào | 3 |
| T17 | Hai máy cùng sửa món một đơn chưa thu | Không có bản nào bị ghi đè im lặng (cần 1b), hoặc ít nhất máy kia thấy bản mới < 1 giây | 1b, 3 |

---

## 10. Nguồn tham khảo

**Phần mềm cùng loại (cách họ xử lý đơn mới)**

- Toast — KDS nhận đơn theo thời gian thực; mất mạng thì đồng bộ nội bộ qua một thiết bị trung tâm: [Kitchen display system overview](https://doc.toasttab.com/doc/platformguide/platformKDSOverview.html), [Offline mode with local sync](https://doc.toasttab.com/doc/platformguide/platformOfflineModeLocalSync.html)
- Square KDS — đơn tự hiện, chỉ bật/tắt âm báo: [KDS notification sound](https://community.squareup.com/t5/Feature-Requests/KDS-notification-sound/idi-p/721882), [Audible alerts for new orders](https://community.squareup.com/t5/Square-for-Restaurants/Audible-Alerts-for-new-online-store-orders/m-p/137331)
- KiotViet — menu điện tử gửi yêu cầu, thu ngân thấy chấm đỏ kèm số: [Menu điện tử](https://www.kiotviet.vn/menu-dien-tu-la-gi-loi-ich-khi-su-dung-menu-dien-tu-trong-nha-hang-quan-cafe)
- Sapo FnB — gọi món qua QR nối thẳng bếp + thu ngân: [Ra mắt Sapo FnB](https://www.sapo.vn/blog/ra-mat-sapo-fnb)

**Kỹ thuật**

- Chuyển từ hỏi định kỳ sang SignalR: [Replace client-side polling with SignalR (Microsoft Learn)](https://learn.microsoft.com/en-us/training/modules/aspnet-core-signalr-polling-fix)
- Hub, `IHubContext`: [Use hubs in SignalR for ASP.NET Core](https://learn.microsoft.com/aspnet/core/signalr/hubs), [Send messages from outside a hub](https://learn.microsoft.com/aspnet/core/signalr/hubcontext)
- JWT qua `access_token`, rủi ro ghi log URL: [SignalR authentication and authorization](https://learn.microsoft.com/aspnet/core/signalr/authn-and-authz), [SignalR security](https://learn.microsoft.com/aspnet/core/signalr/security)
- SignalR trong Blazor WASM: [Blazor SignalR tutorial](https://learn.microsoft.com/aspnet/core/blazor/tutorials/signalr-blazor)
- Client .NET, tự kết nối lại, `IRetryPolicy`: [ASP.NET Core SignalR .NET Client](https://learn.microsoft.com/aspnet/core/signalr/dotnet-client)
- Tin rơi khi mất kết nối, ứng dụng phải tự bù: [Client disconnections (Azure SignalR)](https://docs.azure.cn/en-us/azure-signalr/signalr-concept-client-disconnections)
- `rowversion` và `MIN_ACTIVE_ROWVERSION`: [rowversion (Transact-SQL)](https://learn.microsoft.com/sql/t-sql/data-types/rowversion-transact-sql), [MIN_ACTIVE_ROWVERSION](https://learn.microsoft.com/sql/t-sql/functions/min-active-rowversion-transact-sql)
- Concurrency token trong EF Core: [Handling concurrency conflicts](https://learn.microsoft.com/ef/core/saving/concurrency)
- Đồng bộ lại sau khi kết nối lại: [WebSocket Reconnection: State Sync and Recovery](https://websocket.org/guides/reconnection/)
- Đọc lại từ DB thay vì tin vào sự kiện (outbox / polling publisher): [Transactional outbox](https://microservices.io/patterns/data/transactional-outbox), [Polling publisher](https://microservices.io/patterns/data/polling-publisher.html)
- Khóa chống trùng: [Stripe: Idempotent requests](https://docs.stripe.com/api/idempotent_requests), [Designing robust APIs with idempotency](https://stripe.com/gb/blog/idempotency)
- Hẹn giờ bị làm chậm ở tab ẩn: [Page Visibility API (MDN)](https://developer.mozilla.org/docs/Web/API/Page_Visibility_API)
- Client SignalR cho Flutter (không hỗ trợ Flutter Web): [signalr_netcore](https://pub.dev/packages/signalr_netcore)

---

## 11. Cần xem lại khi hệ thống lớn lên

- Nhiều máy chủ: thêm Redis backplane, hoặc Azure SignalR Service.
- Nhiều chi nhánh: chia nhóm hub theo chi nhánh, mỗi chi nhánh một con trỏ.
- Cần lịch sử sự kiện hoặc tích hợp bếp / kế toán: nâng con trỏ `PhienBan` thành bảng outbox thật sự.
- Webhook ngân hàng cho chuyển khoản: đi qua cùng `ThanhToan` (tự có chuông), chống trùng theo mã giao dịch ngân hàng.
- Đẩy trạng thái xuống tablet (thay cách hỏi 3 giây) nếu tablet chạy bản Android/iOS.
