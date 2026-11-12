using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Món và giá theo size — api/v1/mon, api/v1/chi-tiet-mon/{id}/cong-thuc.
/// </summary>
public class ProductService
{
    private readonly ApiClient _api;

    public ProductService(ApiClient api)
    {
        _api = api;
    }

    /// <summary>Toàn bộ món (kể cả ngừng bán) cho trang quản lý.</summary>
    public Task<List<Mon>> GetAllAsync(CancellationToken ct = default) =>
        _api.GetAllPagesAsync<Mon>("api/v1/mon?baoGomNgungBan=true", ct);

    /// <summary>Món đang bán được (còn hàng, đủ nguyên liệu) — dùng khi tạo đơn tại quầy.</summary>
    public Task<List<Mon>> GetSellableAsync(CancellationToken ct = default) =>
        _api.GetAllPagesAsync<Mon>("api/v1/mon?coTheBan=true", ct);

    public Task<Mon> CreateAsync(MonTaoRequest request, CancellationToken ct = default) =>
        _api.PostAsync<Mon>("api/v1/mon", request, ct);

    public Task<Mon> UpdateAsync(int id, MonCapNhatRequest request, CancellationToken ct = default) =>
        _api.PatchAsync<Mon>($"api/v1/mon/{id}", request, ct);

    /// <summary>Thay bảng giá theo size; size không gửi lên bị tắt (không xóa vì đơn cũ tham chiếu).</summary>
    public Task<Mon> SetPricesAsync(int id, List<GiaSizeRequest> prices, CancellationToken ct = default) =>
        _api.PutAsync<Mon>($"api/v1/mon/{id}/kich-thuoc", prices, ct);

    /// <summary>Báo tạm hết / có hàng lại trong ngày.</summary>
    public Task<Mon> SetAvailableAsync(int id, bool conHang, CancellationToken ct = default) =>
        _api.PatchAsync<Mon>($"api/v1/mon/{id}/con-hang", new ConHangRequest { ConHang = conHang }, ct);

    /// <summary>Xóa mềm: món chuyển sang "Ngừng bán".</summary>
    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        _api.DeleteAsync($"api/v1/mon/{id}", ct);

    /// <summary>Định mức nguyên liệu của một món-size.</summary>
    public Task<List<CongThucDong>> GetRecipeAsync(int maChiTietMon, CancellationToken ct = default) =>
        _api.GetAsync<List<CongThucDong>>($"api/v1/chi-tiet-mon/{maChiTietMon}/cong-thuc", ct);

    public Task<List<CongThucDong>> SaveRecipeAsync(
        int maChiTietMon,
        List<CongThucDongRequest> lines,
        CancellationToken ct = default) =>
        _api.PutAsync<List<CongThucDong>>($"api/v1/chi-tiet-mon/{maChiTietMon}/cong-thuc", lines, ct);
}
