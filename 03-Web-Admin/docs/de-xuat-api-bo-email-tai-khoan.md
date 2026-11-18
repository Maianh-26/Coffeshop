# Đề xuất API: email tài khoản không bắt buộc

**Gửi:** nhóm Backend (CoffeeShop.Api)
**Từ:** nhóm Web admin
**Ngày:** 18/11/2026

## Bối cảnh

Chủ quán quyết định **bỏ luồng quên mật khẩu qua email**: quán chỉ dùng vài tài khoản, ai quên mật khẩu thì chủ quán đặt lại ở trang Tài khoản (Sửa → Mật khẩu mới, tức `PATCH api/v1/nguoi-dung/{id}` với `matKhauMoi`). Vì vậy:

- Web admin đã bỏ trang Quên mật khẩu. Các API `api/auth/forgot-password`, `verify-reset-code`, `reset-password` **không cần làm nữa**.
- Web admin đã bỏ ô **Email** trong form Thêm / Sửa tài khoản và bỏ cột Email trong bảng.

## Vướng mắc hiện tại

`NguoiDungTaoRequest.email` đang là `[Required, EmailAddress]` (openapi: `required: [tenDangNhap, matKhau, hoTen, email, vaiTro]`), cột `NguoiDung.Email` là `VARCHAR(100) NOT NULL UNIQUE`.

**Cách tạm thời của web:** khi tạo tài khoản, web gửi email nội bộ `<tenDangNhap>@coffeeshop.local` (tên đăng nhập đã duy nhất nên email cũng không trùng). Khi sửa, web không gửi `email` (null = giữ nguyên).

## Đề xuất

1. `POST api/v1/nguoi-dung`: bỏ `email` khỏi danh sách bắt buộc (`string? Email`, vẫn kiểm tra định dạng nếu có gửi).
2. Cột `NguoiDung.Email`: cho phép `NULL`. Nếu vẫn muốn giữ `UNIQUE` thì dùng chỉ mục lọc `WHERE Email IS NOT NULL`.
3. `NguoiDungDto.email` trả `null` khi không có.

Sau khi backend đổi, web sẽ bỏ dòng tự sinh email (`NguoiDungTaoRequest.EmailNoiBo`) và không gửi trường `email` nữa.
