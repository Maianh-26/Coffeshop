using System.Net.Http.Json;
using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Service gọi API tổng quan và báo cáo.
/// </summary>
public class DashboardService
{
    private readonly HttpClient _http;

    public DashboardService(HttpClient http)
    {
        _http = http;
    }

    public async Task<DashboardReport> GetReportAsync(
        DashboardFilter filter
    )
    {
        string url = "api/dashboard/report";

        if (filter.From.HasValue && filter.To.HasValue)
        {
            url += $"?from={filter.From:yyyy-MM-dd}"
                 + $"&to={filter.To:yyyy-MM-dd}";
        }

        return await _http.GetFromJsonAsync<DashboardReport>(url)
               ?? new DashboardReport();
    }
}

