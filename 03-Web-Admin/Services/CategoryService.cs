using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Danh mục món — api/v1/danh-muc; kích thước ly — api/v1/kich-thuoc.
/// </summary>
public class CategoryService
{
    private readonly ApiClient _api;

    public CategoryService(ApiClient api)
    {
        _api = api;
    }

    public Task<List<DanhMuc>> GetAllAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<DanhMuc>>("api/v1/danh-muc", ct);

    public Task<DanhMuc> CreateAsync(string tenDanhMuc, CancellationToken ct = default) =>
        _api.PostAsync<DanhMuc>("api/v1/danh-muc", new DanhMucRequest { TenDanhMuc = tenDanhMuc }, ct);

    public Task<DanhMuc> UpdateAsync(int id, string tenDanhMuc, CancellationToken ct = default) =>
        _api.PutAsync<DanhMuc>($"api/v1/danh-muc/{id}", new DanhMucRequest { TenDanhMuc = tenDanhMuc }, ct);

    /// <summary>409 nếu danh mục còn món.</summary>
    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        _api.DeleteAsync($"api/v1/danh-muc/{id}", ct);

    // ------------------------------------------------------------------ Kích thước

    public Task<List<KichThuoc>> GetSizesAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<KichThuoc>>("api/v1/kich-thuoc", ct);

    public Task<KichThuoc> CreateSizeAsync(KichThuocRequest request, CancellationToken ct = default) =>
        _api.PostAsync<KichThuoc>("api/v1/kich-thuoc", request, ct);

    public Task<KichThuoc> UpdateSizeAsync(KichThuocRequest request, CancellationToken ct = default) =>
        _api.PutAsync<KichThuoc>($"api/v1/kich-thuoc/{Uri.EscapeDataString(request.MaKichThuoc)}", request, ct);

    /// <summary>409 nếu kích thước đang có trong bảng giá món.</summary>
    public Task DeleteSizeAsync(string maKichThuoc, CancellationToken ct = default) =>
        _api.DeleteAsync($"api/v1/kich-thuoc/{Uri.EscapeDataString(maKichThuoc)}", ct);
}
