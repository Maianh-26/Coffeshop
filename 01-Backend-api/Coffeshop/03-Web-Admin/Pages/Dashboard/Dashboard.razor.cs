using System.Globalization;
using CoffeeShop.Admin.Models;
using CoffeeShop.Admin.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CoffeeShop.Admin.Pages.Dashboard;

/// <summary>
/// Tổng quan cho chủ quán: doanh thu (api/v1/bao-cao/doanh-thu), món bán chạy (mon-ban-chay).
/// </summary>
public partial class Dashboard : IDisposable
{
    [Inject]
    private DashboardService Reports { get; set; } = default!;

    [Inject]
    private ReportExporter Exporter { get; set; } = default!;

    [Inject]
    private ILogger<Dashboard> Logger { get; set; } = default!;

    private readonly CancellationTokenSource pageCts = new();

    /// <summary>Bộ lọc đang chỉnh trên thanh công cụ (báo cáo hiển thị theo lần bấm Áp dụng gần nhất).</summary>
    private DashboardFilter filter = new();

    private BaoCaoDoanhThu? report;
    private List<MonBanChay> bestSellers = [];

    private bool loading;
    private bool exportOpen;
    private bool exporting;
    private string? error;

    private string range = "7 ngày gần nhất";

    private static readonly CultureInfo Vi = new("vi-VN");

    private static string Money(decimal value) => value.ToString("N0", Vi) + " đ";

    private List<DoanhThuNgay> Rows => report?.TheoNgay ?? [];

    protected override async Task OnInitializedAsync()
    {
        // Mặc định: 7 ngày gần nhất
        filter = new DashboardFilter { From = DateTime.Today.AddDays(-6), To = DateTime.Today };
        await Apply();
    }

    private async Task Apply()
    {
        if (loading) return;

        if (filter.From.HasValue && filter.To.HasValue && filter.To < filter.From)
        {
            error = "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.";
            return;
        }

        loading = true;
        error = null;

        try
        {
            var current = new DashboardFilter { From = filter.From, To = filter.To };

            var revenueTask = Reports.GetRevenueAsync(current, pageCts.Token);
            var bestTask = Reports.GetBestSellersAsync(current, 5, pageCts.Token);

            report = await revenueTask;
            bestSellers = await bestTask;

            range = (current.From, current.To) switch
            {
                (null, null) => "Toàn bộ thời gian",
                ({ } f, null) => $"Từ {f:dd/MM/yyyy}",
                (null, { } t) => $"Đến {t:dd/MM/yyyy}",
                ({ } f, { } t) => $"{f:dd/MM/yyyy} – {t:dd/MM/yyyy}"
            };
        }
        catch (ApiException ex)
        {
            error = ex.Message;
        }
        catch (OperationCanceledException) when (pageCts.IsCancellationRequested)
        {
            // Rời trang khi đang tải
        }
        finally
        {
            loading = false;
        }
    }

    private void ChangePeriod(ChangeEventArgs e)
    {
        DateTime today = DateTime.Today;

        (filter.From, filter.To) = e.Value?.ToString() switch
        {
            "7" => (today.AddDays(-6), today),
            "30" => (today.AddDays(-29), today),
            "month" => (new DateTime(today.Year, today.Month, 1), today),
            "all" => ((DateTime?)null, (DateTime?)null),
            _ => (filter.From, filter.To)
        };
    }

    private async Task ExportExcel()
    {
        if (report is null || exporting) return;

        exporting = true;

        try
        {
            await Exporter.ExportExcelAsync(report, bestSellers, range);
            exportOpen = false;
        }
        catch (JSException ex)
        {
            Logger.LogError(ex, "Xuất CSV thất bại.");
            error = "Không xuất được file. Hãy tải lại trang (Ctrl + F5) rồi thử lại.";
            exportOpen = false;
        }
        finally
        {
            exporting = false;
        }
    }

    private async Task PrintPdf()
    {
        exportOpen = false;

        try
        {
            await Exporter.PrintPdfAsync();
        }
        catch (JSException ex)
        {
            Logger.LogError(ex, "Mở hộp thoại in thất bại.");
            error = "Không mở được hộp thoại in. Hãy tải lại trang rồi thử lại.";
        }
    }

    private decimal ChartMax =>
        Math.Max(
            1000000m,
            decimal.Ceiling((Rows.Count == 0 ? 0 : Rows.Max(row => row.DoanhThu)) / 1000000m) * 1000000m);

    private string BarHeight(decimal value) =>
        $"height:{(value / ChartMax * 150m).ToString(CultureInfo.InvariantCulture)}px";

    private string LabelPosition(decimal value) =>
        $"bottom:{(value / ChartMax * 150m + 6m).ToString(CultureInfo.InvariantCulture)}px";

    private static string AxisPosition(int index) =>
        $"top:{((6 - index) / 6m * 100m).ToString(CultureInfo.InvariantCulture)}%";

    public void Dispose()
    {
        pageCts.Cancel();
        pageCts.Dispose();
    }
}
