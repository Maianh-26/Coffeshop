using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Quản lý tài khoản (chỉ Quản lý) — api/v1/nguoi-dung. Web không dùng "khóa / mở khóa": chỉ có Xóa
/// (xem <see cref="DeleteAsync"/>). Tài khoản đã xóa backend không trả về; tài khoản bị khóa (TrangThai = false) hiện nhãn "Bị khóa", Sửa để mở khóa.
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
    /// Xóa tài khoản (204). Backend xóa mềm: giải phóng tên đăng nhập (tạo lại được tài khoản cùng tên),
    /// token đang dùng của tài khoản đó bị 401 ngay, đơn hàng / lịch sử kho cũ vẫn giữ.
    /// 409 khi xóa chính mình hoặc quản lý cuối cùng; 404 khi tài khoản không còn.
    /// </summary>
    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        _api.DeleteAsync($"api/v1/nguoi-dung/{id}", ct);
}
