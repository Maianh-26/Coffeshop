using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Danh mục món — api/v1/danh-muc; kích thước ly — api/v1/kich-thuoc (chỉ đọc: S/M/L có sẵn trong CSDL,
/// dùng cho ô giá theo size trong form món; web không thêm / sửa / xóa size).
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

    // ------------------------------------------------------------------ Kích thước (chỉ đọc)

    public Task<List<KichThuoc>> GetSizesAsync(CancellationToken ct = default) =>
        _api.GetAsync<List<KichThuoc>>("api/v1/kich-thuoc", ct);
}
