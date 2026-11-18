# Đề xuất API: xóa tài khoản

Gửi nhóm backend (CoffeeShop.Api). Viết ngày 17/11/2026.

## Bối cảnh

Chủ quán yêu cầu mọi trang quản lý của web admin có đủ ba nút **Thêm, Sửa, Xóa**. Ở trang Tài khoản, nút **Khóa / Mở khóa** được thay bằng **Xóa tài khoản**.

Backend hiện **chưa có** `DELETE api/v1/nguoi-dung/{id}`. Trong lúc chờ, web dùng tạm API đang có:

```
PATCH api/v1/nguoi-dung/{id}   { "trangThai": false }
```

Sau lời gọi này, tài khoản không đăng nhập được nữa và web không hiện tài khoản có `trangThai = false` trong danh sách.

## Đề xuất

```
DELETE api/v1/nguoi-dung/{id}      (chỉ QuanLy)   → 204 No Content
```

| Trường hợp | Kết quả |
|---|---|
| Xóa tài khoản đang đăng nhập | 409 `CONFLICT`: "Không thể xóa tài khoản của chính mình" |
| Không tìm thấy | 404 `NOT_FOUND` |
| Tài khoản đã tạo đơn / ghi lịch sử kho | **Xóa mềm** (giữ khóa ngoại `DonHang.MaNguoiDung`, `LichSuKho.MaNguoiDung`) |

Gợi ý cách xóa mềm:
- Đặt `TrangThai = 0`, rồi đổi `TenDangNhap` và `Email` sang giá trị không trùng (ví dụ thêm hậu tố `#deleted-{id}`), để **tạo lại được tài khoản cùng tên đăng nhập / email**. Đây là chỗ cách làm tạm hiện tại chưa xử lý được: tạo lại sẽ bị 409 "Tên đăng nhập đã tồn tại".
- `GET api/v1/nguoi-dung` mặc định không trả tài khoản đã xóa.

## Phía web khi backend xong

Chỉ cần đổi một dòng trong `03-Web-Admin/Services/AccountService.cs` (`DeleteAsync`) sang `DELETE`, và thêm route tương ứng vào `Services/Mock/MockApiHandler.cs`.
