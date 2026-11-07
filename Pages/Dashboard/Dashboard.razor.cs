using CoffeeShop.Admin.Models;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace CoffeeShop.Admin.Pages.Dashboard;

public partial class Dashboard
{
    private DashboardFilter filter = new();

    private DashboardReport report = new();

    private bool exportOpen;

    private string? error;

    private string range = "Toàn bộ thời gian";

    private static readonly CultureInfo Vi = new("vi-VN");

    private static string Money(decimal value)
    {
        return value.ToString("N0", Vi) + " đ";
    }

    protected override Task OnInitializedAsync()
    {
        // Khởi tạo báo cáo trống
        report = new DashboardReport
        {
            Rows = new()
        };
        return Task.CompletedTask;
    }

    private async Task Apply()
    {
        try
        {
            // Giả lập dữ liệu từ service
            range = filter.From.HasValue
                ? $"{filter.From:dd/MM/yyyy} – {filter.To:dd/MM/yyyy}"
                : "Toàn bộ thời gian";

            error = null;
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }
        await Task.CompletedTask;
    }

    private void ChangePeriod(ChangeEventArgs e)
    {
        DateTime today = DateTime.Today;

        switch (e.Value?.ToString())
        {
            case "7":
                filter.From = today.AddDays(-6);
                filter.To = today;
                break;

            case "30":
                filter.From = today.AddDays(-29);
                filter.To = today;
                break;

            case "month":
                filter.From = new DateTime(
                    today.Year,
                    today.Month,
                    1
                );

                filter.To = today;
                break;

            default:
                filter.From = null;
                filter.To = null;
                break;
        }
    }

    private async Task ExportExcel()
    {
        try
        {
            // Placeholder - không cần implement export ngay
            error = "Tính năng xuất Excel sẽ được thêm sau.";
            exportOpen = false;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            exportOpen = false;
        }
        await Task.CompletedTask;
    }

    private async Task PrintPdf()
    {
        try
        {
            // Placeholder - không cần implement print ngay
            error = "Tính năng in PDF sẽ được thêm sau.";
            exportOpen = false;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            exportOpen = false;
        }
        await Task.CompletedTask;
    }

    private decimal ChartMax =>
        Math.Max(
            1000000m,
            decimal.Ceiling(
                (
                    report.Rows.Count == 0
                        ? 0
                        : report.Rows.Max(row => row.Revenue)
                ) / 1000000m
            ) * 1000000m
        );

    private string BarHeight(decimal value)
    {
        string height = (
            value / ChartMax * 150m
        ).ToString(CultureInfo.InvariantCulture);

        return $"height:{height}px";
    }

    private string LabelPosition(decimal value)
    {
        string bottom = (
            value / ChartMax * 150m + 6m
        ).ToString(CultureInfo.InvariantCulture);

        return $"bottom:{bottom}px";
    }

    private static string AxisPosition(int index)
    {
        string top = (
            (6 - index) / 6m * 100m
        ).ToString(CultureInfo.InvariantCulture);

        return $"top:{top}%";
    }
}