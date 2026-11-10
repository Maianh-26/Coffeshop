using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Topping — api/v1/topping.
/// </summary>
public class ToppingService
{
    private readonly ApiClient _api;

    public ToppingService(ApiClient api)
    {
        _api = api;
    }

    /// <param name="includeStopped">true = lấy cả topping đã ngừng bán (trang quản lý).</param>
    /// <param name="ct">Hủy khi rời trang.</param>
    public Task<List<Topping>> GetAllAsync(bool includeStopped = false, CancellationToken ct = default) =>
        _api.GetAsync<List<Topping>>(Query.Build("api/v1/topping", ("baoGomNgungBan", includeStopped ? true : null)), ct);

    public Task<Topping> CreateAsync(ToppingRequest request, CancellationToken ct = default) =>
        _api.PostAsync<Topping>("api/v1/topping", request, ct);

    public Task<Topping> UpdateAsync(int id, ToppingRequest request, CancellationToken ct = default) =>
        _api.PutAsync<Topping>($"api/v1/topping/{id}", request, ct);

    public Task<Topping> SetAvailableAsync(int id, bool conHang, CancellationToken ct = default) =>
        _api.PatchAsync<Topping>($"api/v1/topping/{id}/con-hang", new ConHangRequest { ConHang = conHang }, ct);

    /// <summary>Xóa mềm: topping chuyển sang "Ngừng bán".</summary>
    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        _api.DeleteAsync($"api/v1/topping/{id}", ct);

    public Task<List<CongThucDong>> GetRecipeAsync(int id, CancellationToken ct = default) =>
        _api.GetAsync<List<CongThucDong>>($"api/v1/topping/{id}/cong-thuc", ct);

    public Task<List<CongThucDong>> SaveRecipeAsync(int id, List<CongThucDongRequest> lines, CancellationToken ct = default) =>
        _api.PutAsync<List<CongThucDong>>($"api/v1/topping/{id}/cong-thuc", lines, ct);
}
