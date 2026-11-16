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

    /// <summary>Danh sách nguyên liệu. Tình trạng hiển thị tính ở web (TinhTrangKho), không dùng dự báo của backend.</summary>
    public Task<List<NguyenLieu>> GetIngredientsAsync(bool? sapHet = null, bool? dangSuDung = null, CancellationToken ct = default) =>
        _api.GetAsync<List<NguyenLieu>>(Query.Build("api/v1/nguyen-lieu", ("sapHet", sapHet), ("dangSuDung", dangSuDung)), ct);

    public Task<NguyenLieu> CreateAsync(NguyenLieuRequest request, CancellationToken ct = default) =>
        _api.PostAsync<NguyenLieu>("api/v1/nguyen-lieu", request, ct);

    public Task<NguyenLieu> UpdateAsync(int id, NguyenLieuCapNhatRequest request, CancellationToken ct = default) =>
        _api.PatchAsync<NguyenLieu>($"api/v1/nguyen-lieu/{id}", request, ct);

    /// <summary>
    /// Xóa mềm: nguyên liệu chuyển sang "Ngừng sử dụng". Trang Kho hiện không gọi (đổi trạng thái qua hộp thoại Sửa,
    /// PATCH dangSuDung); giữ lại để khớp API của backend.
    /// </summary>
    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        _api.DeleteAsync($"api/v1/nguyen-lieu/{id}", ct);

    /// <summary>Nhập hàng (+) hoặc xuất hủy (−); luôn ghi lịch sử kho.</summary>
    public Task<NguyenLieu> TransactAsync(int id, string loai, GiaoDichKhoRequest request, CancellationToken ct = default) =>
        _api.PostAsync<NguyenLieu>(
            $"api/v1/nguyen-lieu/{id}/{(loai == LoaiGiaoDichKho.XuatHuy ? "xuat-huy" : "nhap-kho")}",
            request,
            ct);

    /// <param name="tuNgay">Mốc bắt đầu chính xác (đã là 00:00 nếu người dùng chọn ngày).</param>
    /// <param name="denNgay">Ngày kết thúc; service tự đổi thành hết ngày 23:59:59.</param>
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
            // Gửi CHÍNH XÁC đến giờ phút (mốc "24 giờ qua" = lúc này trừ 24 giờ), giống trang Đơn hàng
            ("tuNgay", tuNgay),
            ("denNgay", Query.EndOfDay(denNgay))), ct);
}
