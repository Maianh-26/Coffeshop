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
    /// Danh sách đơn (mới nhất trước), có phân trang. Lọc theo trạng thái thanh toán, trạng thái phục vụ
    /// ([ĐỀ XUẤT] tham số <c>trangThaiPhucVu</c>) và khoảng ngày.
    /// </summary>
    public Task<PagedResult<DonHang>> GetPageAsync(
        int page,
        int limit,
        string? trangThai = null,
        string? trangThaiPhucVu = null,
        DateTime? tuNgay = null,
        DateTime? denNgay = null,
        CancellationToken ct = default) =>
        _api.GetAsync<PagedResult<DonHang>>(Query.Build("api/v1/don-hang",
            ("page", page),
            ("limit", limit),
            ("trangThai", trangThai),
            ("trangThaiPhucVu", trangThaiPhucVu),
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
    /// [ĐỀ XUẤT] Sửa món của đơn chưa thanh toán / chưa phục vụ (thay toàn bộ danh sách món, backend tính lại tổng tiền).
    /// </summary>
    public Task<DonHang> UpdateAsync(int id, DatMonRequest request, CancellationToken ct = default) =>
        _api.PutAsync<DonHang>($"api/v1/don-hang/{id}", request, ct);

    /// <summary>
    /// [ĐỀ XUẤT] Gán / đổi / bỏ số nhận món. 409 SO_NHAN_MON_DANG_DUNG khi số đang dùng cho đơn khác chưa phục vụ xong.
    /// </summary>
    public Task<DonHang> SetPickupNumberAsync(int id, int? soNhanMon, CancellationToken ct = default) =>
        _api.PutAsync<DonHang>($"api/v1/don-hang/{id}/so-nhan-mon", new SoNhanMonRequest { SoNhanMon = soNhanMon }, ct);

    /// <summary>
    /// [ĐỀ XUẤT] Đổi trạng thái phục vụ: WAITING → PREPARING → SERVED. SERVED giải phóng số nhận món.
    /// </summary>
    public Task<DonHang> SetServingStatusAsync(int id, string trangThaiPhucVu, CancellationToken ct = default) =>
        _api.PutAsync<DonHang>($"api/v1/don-hang/{id}/trang-thai-phuc-vu",
            new TrangThaiPhucVuRequest { TrangThaiPhucVu = trangThaiPhucVu }, ct);

    /// <summary>
    /// Hủy đơn (chỉ Quản lý). Đơn đã thanh toán được đánh dấu hoàn tiền.
    /// </summary>
    public Task<DonHang> CancelAsync(int id, string lyDoHuy, CancellationToken ct = default) =>
        _api.PostAsync<DonHang>($"api/v1/don-hang/{id}/huy", new HuyDonRequest { LyDoHuy = lyDoHuy }, ct);
}
