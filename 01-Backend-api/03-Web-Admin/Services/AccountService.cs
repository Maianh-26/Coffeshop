using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Quản lý tài khoản (chỉ Quản lý) — api/v1/nguoi-dung. Không có xóa: khóa tài khoản bằng TrangThai = false.
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

    /// <summary>Sửa thông tin, đổi vai trò, khóa/mở khóa, đặt lại mật khẩu (trường null = giữ nguyên).</summary>
    public Task<NguoiDung> UpdateAsync(int id, NguoiDungCapNhatRequest request, CancellationToken ct = default) =>
        _api.PatchAsync<NguoiDung>($"api/v1/nguoi-dung/{id}", request, ct);
}
