using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Kho nguyên liệu — api/v1/nguyen-lieu, api/v1/lich-su-kho (chỉ Quản lý).
/// </summary>
public class InventoryService
{
    private readonly ApiClient _api;

    public InventoryService(ApiClient api)
    {
        _api = api;
    }

    /// <summary>Danh sách nguyên liệu kèm dự báo số ngày còn dùng.</summary>
    public Task<List<NguyenLieu>> GetIngredientsAsync(bool? sapHet = null, bool? dangSuDung = null, CancellationToken ct = default) =>
        _api.GetAsync<List<NguyenLieu>>(Query.Build("api/v1/nguyen-lieu", ("sapHet", sapHet), ("dangSuDung", dangSuDung)), ct);

    public Task<NguyenLieu> CreateAsync(NguyenLieuRequest request, CancellationToken ct = default) =>
        _api.PostAsync<NguyenLieu>("api/v1/nguyen-lieu", request, ct);

    public Task<NguyenLieu> UpdateAsync(int id, NguyenLieuCapNhatRequest request, CancellationToken ct = default) =>
        _api.PatchAsync<NguyenLieu>($"api/v1/nguyen-lieu/{id}", request, ct);

    /// <summary>Xóa mềm: nguyên liệu chuyển sang "Ngừng sử dụng".</summary>
    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        _api.DeleteAsync($"api/v1/nguyen-lieu/{id}", ct);

    /// <summary>Nhập hàng (+) hoặc xuất hủy (−); luôn ghi lịch sử kho.</summary>
    public Task<NguyenLieu> TransactAsync(int id, string loai, GiaoDichKhoRequest request, CancellationToken ct = default) =>
        _api.PostAsync<NguyenLieu>(
            $"api/v1/nguyen-lieu/{id}/{(loai == LoaiGiaoDichKho.XuatHuy ? "xuat-huy" : "nhap-kho")}",
            request,
            ct);

    public Task<PagedResult<LichSuKho>> GetHistoryAsync(
        int page,
        int limit,
        int? maNguyenLieu = null,
        string? loaiGiaoDich = null,
        DateTime? tuNgay = null,
        DateTime? denNgay = null,
        CancellationToken ct = default) =>
        _api.GetAsync<PagedResult<LichSuKho>>(Query.Build("api/v1/lich-su-kho",
            ("page", page),
            ("limit", limit),
            ("maNguyenLieu", maNguyenLieu),
            ("loaiGiaoDich", loaiGiaoDich),
            ("tuNgay", tuNgay?.Date),
            ("denNgay", Query.EndOfDay(denNgay))), ct);
}
