using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Báo cáo cho chủ quán — api/v1/bao-cao/*. Doanh thu chỉ tính đơn đã thanh toán.
/// </summary>
public class DashboardService
{
    private readonly ApiClient _api;

    public DashboardService(ApiClient api)
    {
        _api = api;
    }

    private static string Range(string path, DashboardFilter filter, int? top = null) =>
        Query.Build(path,
            ("tuNgay", filter.From?.Date),
            ("denNgay", Query.EndOfDay(filter.To)),
            ("soLuong", top));

    public Task<BaoCaoDoanhThu> GetRevenueAsync(DashboardFilter filter, CancellationToken ct = default) =>
        _api.GetAsync<BaoCaoDoanhThu>(Range("api/v1/bao-cao/doanh-thu", filter), ct);

    public Task<List<MonBanChay>> GetBestSellersAsync(DashboardFilter filter, int top = 5, CancellationToken ct = default) =>
        _api.GetAsync<List<MonBanChay>>(Range("api/v1/bao-cao/mon-ban-chay", filter, top), ct);

    public Task<List<GioCaoDiem>> GetPeakHoursAsync(DashboardFilter filter, CancellationToken ct = default) =>
        _api.GetAsync<List<GioCaoDiem>>(Range("api/v1/bao-cao/gio-cao-diem", filter), ct);
}
