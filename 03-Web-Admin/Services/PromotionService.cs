using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Khuyến mãi — api/v1/khuyen-mai.
/// </summary>
public class PromotionService
{
    private readonly ApiClient _api;

    public PromotionService(ApiClient api)
    {
        _api = api;
    }

    public Task<List<KhuyenMai>> GetAllAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<KhuyenMai>>("api/v1/khuyen-mai", ct);

    public Task<KhuyenMai> CreateAsync(KhuyenMaiRequest request, CancellationToken ct = default) =>
        _api.PostAsync<KhuyenMai>("api/v1/khuyen-mai", request, ct);

    /// <summary>Sửa toàn bộ; MaTenMons khác null thì thay danh sách món áp dụng.</summary>
    public Task<KhuyenMai> UpdateAsync(int id, KhuyenMaiRequest request, CancellationToken ct = default) =>
        _api.PutAsync<KhuyenMai>($"api/v1/khuyen-mai/{id}", request, ct);

    /// <summary>Xóa khuyến mãi: backend tự gỡ khỏi các món đang gắn.</summary>
    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        _api.DeleteAsync($"api/v1/khuyen-mai/{id}", ct);
}
