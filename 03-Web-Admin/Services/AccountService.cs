using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Quản lý tài khoản (chỉ Quản lý) — api/v1/nguoi-dung. Web không còn "khóa / mở khóa": chỉ có Xóa
/// (xem <see cref="DeleteAsync"/>). Tài khoản đã xóa (TrangThai = false) không hiện trong danh sách.
/// </summary>
public class AccountService
{
    private readonly ApiClient _api;

    public AccountService(ApiClient api)
    {
        _api = api;
    }

    public Task<List<NguoiDung>> GetAllAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<NguoiDung>>("api/v1/nguoi-dung", ct);

    public Task<NguoiDung> CreateAsync(NguoiDungTaoRequest request, CancellationToken ct = default) =>
        _api.PostAsync<NguoiDung>("api/v1/nguoi-dung", request, ct);

    /// <summary>Sửa thông tin, đổi vai trò, đặt lại mật khẩu (trường null = giữ nguyên).</summary>
    public Task<NguoiDung> UpdateAsync(int id, NguoiDungCapNhatRequest request, CancellationToken ct = default) =>
        _api.PatchAsync<NguoiDung>($"api/v1/nguoi-dung/{id}", request, ct);

    /// <summary>
    /// Xóa tài khoản. Backend CHƯA có DELETE api/v1/nguoi-dung/{id} (đã đề xuất, xem docs/de-xuat-api-xoa-tai-khoan.md),
    /// nên tạm vô hiệu hóa bằng PATCH trangThai = false: không đăng nhập được nữa, đơn hàng / lịch sử kho của
    /// tài khoản vẫn giữ. Khi backend có route xóa, chỉ cần đổi lời gọi ở đây.
    /// </summary>
    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        _api.PatchAsync<NguoiDung>($"api/v1/nguoi-dung/{id}", new NguoiDungCapNhatRequest { TrangThai = false }, ct);
}
