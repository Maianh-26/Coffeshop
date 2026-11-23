# Đo hiệu năng với 1 triệu đơn hàng (23/11/2026)

Mục đích: kiểm chứng hệ thống chạy thế nào khi quán hoạt động khoảng 1 năm. Đo trên **bản sao** `QuanCaPheDB_Bench`, không đụng DB thật.

## Dữ liệu thử
- **1.000.010 đơn**, trải đều 365 ngày (khoảng 2.740 đơn mỗi ngày, 07:00–21:30). 95% đã thu tiền, 5% đã hủy.
- **2.500.012 dòng món** (1–4 món mỗi đơn) và **1.000.007 dòng topping** (khoảng 40% số ly có topping).
- Dung lượng: DonHang 129 MB, ChiTietDonHang 164 MB, ChiTietDonHang_Topping 77 MB. Tổng khoảng 480 MB tính cả index.
- Máy: laptop, SQL Server 2022 Developer, API chạy Release. Mỗi thao tác gọi 1 lần làm nóng rồi 5 lần, lấy trung vị.

## Kết quả (ms, trung vị)

| Thao tác | Ban đầu | + index | + index + sửa code |
|---|---:|---:|---:|
| Danh sách trang 1 (mới nhất) | 2.550 | 255 | 276 |
| Sắp xếp tổng tiền cao → thấp | 4.152 | 249 | 266 |
| Sắp xếp tổng tiền thấp → cao | 4.267 | 2.917 | 265 |
| Sắp xếp mã đơn giảm dần | 1.103 | 165 | 265 |
| Trang 25.000 (rất xa) | 3.663 | 3.296 | 898 |
| Lọc hôm nay | 1.308 | 10 | 11 |
| Lọc đã thu trong 7 ngày | 1.281 | 13 | 19 |
| Tìm mã đơn 523456 | 2.088 | **12.979** | **22** |
| Trang 100 đơn | 2.461 | 353 | 302 |
| Chi tiết 1 đơn | 284 | 17 | 6 |
| Báo cáo doanh thu hôm nay | 169 | 294 | 197 |
| Báo cáo doanh thu cả năm | 4.734 | 5.280 | 882 |
| Món bán chạy cả năm | 1.645 | 1.851 | 1.559 |
| Topping bán chạy cả năm | 838 | 887 | 826 |
| Giờ cao điểm cả năm | 291 | 567 | 219 |

Dữ liệu trả về web luôn chỉ khoảng 0,5–82 KB mỗi lần gọi; không có request nào lỗi.

## Nguyên nhân và cách sửa
1. **Thiếu index** (sửa bằng `database/ToiUuHieuNang_20261123.sql`). SQL Server không tự tạo index cho khóa ngoại `ChiTietDonHang.MaDonHang`, nên mỗi lần nạp món của một đơn phải quét 2,5 triệu dòng. Lọc theo thời gian và sắp xếp theo tổng tiền cũng quét cả bảng.
2. **Tìm kiếm `MaDonHang.ToString().StartsWith()`**: không dùng được index. Thêm index còn làm kế hoạch thực thi tệ đi (2 giây lên 13 giây). Viết lại thành các khoảng số (`Common/TimKiemDonHang.cs`), giữ nguyên nghĩa "mã bắt đầu bằng".
3. **Sắp xếp hai cột ngược chiều** (`TongTien ASC, MaDonHang DESC`) cần index riêng, `IX_DonHang_TongTien_MaGiam`.
4. **`Include` + `AsSplitQuery` lặp lại lọc / sắp xếp / OFFSET ở mỗi bảng con.** Sửa thành 2 bước: lấy mã đơn của trang trước, rồi mới nạp chi tiết theo khóa chính.
5. **Báo cáo doanh thu tải mọi đơn đã thu về RAM của API.** Sửa thành `GROUP BY` ngày và hình thức thanh toán ngay trong SQL.
6. **Web Admin** bỏ nhánh tự tải tới 2.000 đơn để sắp xếp ở trình duyệt.

## Giới hạn còn lại
- Không lọc gì thì `COUNT(*)` trên 1 triệu dòng mất khoảng 190 ms trên máy này, nên danh sách mặc định vẫn ở khoảng 250 ms.
- Báo cáo cả năm, món và topping bán chạy phải cộng gần 1 triệu dòng mỗi lần mở (0,8–1,6 giây). Muốn nhanh hơn thì cần bảng tổng hợp theo ngày, cập nhật khi thanh toán.
- Trang rất xa (OFFSET lớn) khoảng 0,9 giây; phân trang keyset sẽ xử lý, nhưng đổi hợp đồng API (không nhảy thẳng được tới trang N).

## Làm lại phép đo
1. Chép DB thật sang bản thử: `BACKUP DATABASE QuanCaPheDB ... WITH COPY_ONLY`, rồi `RESTORE ... AS QuanCaPheDB_Bench WITH MOVE ...`.
2. `sqlcmd -S . -E -C -I -f 65001 -i docs\do-hieu-nang\sinh-1-trieu-don.sql` (script tự kiểm tra tên DB là `QuanCaPheDB_Bench`). Chạy mất khoảng 2,5 phút.
3. Chạy API trỏ vào bản thử ở cổng 3100, rồi `bash docs/do-hieu-nang/do-api.sh <nhãn>`.
4. `python -X utf8 docs/do-hieu-nang/doi-chieu-api-sql.py`: so kết quả API với SQL (tìm kiếm, sắp xếp, món/topping, doanh thu).
5. Xong thì xóa bản thử: `DROP DATABASE QuanCaPheDB_Bench`.
