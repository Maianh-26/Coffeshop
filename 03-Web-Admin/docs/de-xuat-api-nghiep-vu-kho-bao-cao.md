# Đề xuất cho nhóm Backend: nghiệp vụ kho, hủy đơn, báo cáo, phiên đăng nhập

Ngày lập: 26/11/2026 — nhóm Web Admin. Nguồn: rà soát toàn bộ luồng web admin, đối chiếu mã `01-Backend-api`
(`DonHangService`, `BaoCaoController`, `KhoService`, `sp_ThanhToanVaTruKho`, `Common/Security.cs`).

Web đã tự xử lý phần hiển thị (xem cuối tài liệu). Các mục dưới đây **chỉ backend sửa được**. Mục ghi "(cần thử lại)"
là suy ra từ đọc mã, chưa tái hiện trên máy chạy thật.

## Mức cao

| # | Vấn đề | Hậu quả | Đề xuất |
|---|---|---|---|
| B1 | Kho chỉ bị trừ trong `sp_ThanhToanVaTruKho` (lúc **thu tiền**), trong khi đơn được phép pha / phục vụ trước khi thu | Đơn đã pha rồi bị hủy (khách bỏ về) → nguyên liệu **không bao giờ bị trừ**, kho cao hơn thực tế | Trừ kho khi đơn chuyển sang `PREPARING` (hoặc `SERVED`), thu tiền không trừ lại; hoặc khi hủy đơn đã `PREPARING`/`SERVED` mà chưa thu thì trừ kho. Cần chủ quán chốt thời điểm |
| B2 | Hủy đơn `PAID` không cộng lại kho, kể cả khi đơn **chưa pha** (`WAITING`) | Kho hụt "ảo" | Khi hủy đơn `PAID` + `WAITING`: cộng lại kho + ghi `LichSuKho` loại hoàn (ví dụ `HoanKhoHuyDon`) |
| B3 | Đặt món chỉ so với tồn hiện tại, không trừ phần của các đơn `PENDING` khác; SP trừ kho không kiểm tra tồn, cột `SoLuongTon` không có `CHECK >= 0` | Giờ đông nhận quá số ly pha được → **tồn âm** | Giữ chỗ nguyên liệu cho đơn đang chờ, hoặc kiểm tra lại tồn trong SP và trả 422 `HET_NGUYEN_LIEU` khi thu tiền |
| B4 | `Huy()` đọc đơn rồi `SaveChanges` không điều kiện (cần thử lại) | Thu tiền và hủy cùng lúc → đơn đã thu thành `CANCELLED` mà **`DaHoanTien = false`** | Cập nhật có điều kiện theo trạng thái đã đọc (như `SuaMon` / `DoiTrangThaiPhucVu`), 0 dòng → 409 |
| B5 | Hoàn tiền chuyển đơn sang `CANCELLED` nên doanh thu của **ngày đã thu bị giảm về sau**; `soDonHuy` tính theo giờ tạo, `soDonHoanTien` theo giờ hoàn | Báo cáo của ngày đã chốt tự thay đổi, không khớp sổ quỹ | Giữ doanh thu gốc ở ngày thu, ghi hoàn tiền là khoản trừ ở **ngày hoàn** (thêm `tienHoan` vào `bao-cao/doanh-thu`) |
| B6 | Mọi mốc giờ dùng `DateTime.Now` của máy chủ, JSON không kèm múi giờ; web gửi giờ máy khách | Máy chủ chạy UTC (cloud) → "Hôm nay", báo cáo lệch 7 giờ | Cố định múi giờ Việt Nam (UTC+7) phía máy chủ, hoặc trả/nhận ISO 8601 có offset; ghi vào `openapi.yaml` |

## Mức trung bình

| # | Vấn đề | Đề xuất |
|---|---|---|
| B7 | `gio-cao-diem` gom theo **giờ thu tiền**, không theo giờ khách đặt | Gom theo `ThoiGianTao` (hoặc thêm tham số `theo=tao|thu`) |
| B8 | Đặt lại mật khẩu không làm token cũ mất hiệu lực (chỉ kiểm tra `TrangThai`, `DaXoa`, `VaiTro`) | Thêm "phiên bản bảo mật" của tài khoản vào token, tăng khi đổi mật khẩu |
| B9 | `MonDto` không cho biết món-size **có công thức** hay không; món không có công thức được coi là đủ nguyên liệu và bán không trừ kho | Thêm `coCongThuc` cho từng size trong `kichThuoc[]` (và cho topping) để web gắn nhãn "Chưa có công thức" trên danh sách |
| B10 | Đơn `PENDING` không bao giờ thu tiền nằm mãi (giữ số nhận món, làm sai "chờ thanh toán") | Tùy chọn tự hủy đơn treo sau N giờ (lý do "Quá hạn chưa thu"), hoặc API chốt cuối ngày |

## Mức thấp

| # | Vấn đề | Đề xuất |
|---|---|---|
| B11 | `POST don-hang` cho phép gọi không cần đăng nhập và không giới hạn số lần | Giới hạn số request theo IP / mã tablet |
| B12 | `ThanhToan` đổi `SqlException.Message` thành 409, có thể lộ câu lỗi SQL thô (cần thử lại) | Chỉ dùng câu của `RAISERROR`, lỗi khác trả câu cố định |
| B13 | `xuat-huy` kiểm tra tồn rồi mới ghi, không khóa dòng | Cập nhật có điều kiện `SoLuongTon >= @soLuong` |
| B14 | Nguyên liệu "ngừng sử dụng" vẫn bị trừ theo công thức | Cảnh báo / chặn khi lưu công thức có nguyên liệu ngừng sử dụng |

## Web đã làm (26/11/2026, không đổi hợp đồng API)

- Hộp **Hủy đơn** nói rõ ảnh hưởng tới kho theo từng trường hợp (đã thu + chưa pha → gợi ý nhập lại kho; chưa thu + đã pha → gợi ý xuất hủy).
- **Đơn treo**: thông báo số đơn chưa thu từ trước hôm nay + nút lọc ra các đơn đó.
- Đơn chưa thu in **"PHIẾU TẠM TÍNH"** thay cho "Hóa đơn thanh toán".
- Sửa đơn đang chuẩn bị: nhắc báo lại pha chế; ghi chú tổng chính thức do hệ thống tính lại.
- Hộp **Công thức**: cảnh báo công thức trống (bán không trừ kho) và nguyên liệu đã ngừng sử dụng.
- Trang **Kho**: ghi chú "Tồn âm — cần kiểm kê lại".
- Trang **Tài khoản**: hiện tài khoản bị khóa (nhãn "Bị khóa") để mở khóa hoặc xóa.
- **Phiên đăng nhập**: vai trò và giờ hết hạn đọc từ token; báo trước 10 phút khi sắp hết hạn, hết hạn thì tự đăng xuất.
