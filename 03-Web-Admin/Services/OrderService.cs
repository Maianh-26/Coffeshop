using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Đơn hàng — api/v1/don-hang. Mọi lỗi được ném ra dưới dạng <see cref="ApiException"/>.
/// </summary>
public class OrderService
{
    private readonly ApiClient _api;

    public OrderService(ApiClient api)
    {
        _api = api;
    }

    /// <summary>
    /// Danh sách đơn (mới nhất trước), có phân trang. Lọc theo trạng thái thanh toán và khoảng ngày.
    /// </summary>
    public Task<PagedResult<DonHang>> GetPageAsync(
        int page,
        int limit,
        string? trangThai = null,
        DateTime? tuNgay = null,
        DateTime? denNgay = null,
        CancellationToken ct = default) =>
        _api.GetAsync<PagedResult<DonHang>>(Query.Build("api/v1/don-hang",
            ("page", page),
            ("limit", limit),
            ("trangThai", trangThai),
            ("tuNgay", tuNgay?.Date),
            ("denNgay", Query.EndOfDay(denNgay))), ct);

    public Task<DonHang> GetAsync(int id, CancellationToken ct = default) =>
        _api.GetAsync<DonHang>($"api/v1/don-hang/{id}", ct);

    /// <summary>
    /// Tạo đơn tại quầy (gửi kèm token → backend ghi nhận nhân viên tạo đơn). 422 khi hết món / nguyên liệu.
    /// </summary>
    public Task<DonHang> CreateAsync(DatMonRequest request, CancellationToken ct = default) =>
        _api.PostAsync<DonHang>("api/v1/don-hang", request, ct);

    /// <summary>
    /// Thanh toán: backend chuyển PAID, trừ kho theo công thức và trả cảnh báo nguyên liệu sắp hết.
    /// </summary>
    public Task<ThanhToanResponse> PayAsync(int id, ThanhToanRequest request, CancellationToken ct = default) =>
        _api.PostAsync<ThanhToanResponse>($"api/v1/don-hang/{id}/thanh-toan", request, ct);

    /// <summary>
    /// Hủy đơn (chỉ Quản lý). Đơn đã thanh toán được đánh dấu hoàn tiền.
    /// </summary>
    public Task<DonHang> CancelAsync(int id, string lyDoHuy, CancellationToken ct = default) =>
        _api.PostAsync<DonHang>($"api/v1/don-hang/{id}/huy", new HuyDonRequest { LyDoHuy = lyDoHuy }, ct);
}
