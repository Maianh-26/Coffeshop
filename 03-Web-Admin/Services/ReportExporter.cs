using System.Globalization;
using System.Text;
using CoffeeShop.Admin.Models;
using Microsoft.JSInterop;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Xuất báo cáo ra file Excel (CSV) hoặc gọi window.print() cho PDF.
/// </summary>
public class ReportExporter
{
    private readonly IJSRuntime _js;

    public ReportExporter(IJSRuntime js)
    {
        _js = js;
    }

    private static readonly CultureInfo Vi = new("vi-VN");

    /// <summary>
    /// Xuất báo cáo dashboard ra file CSV (tương thích Excel).
    /// </summary>
    public async Task ExportExcelAsync(
        DashboardReport report,
        string dateRange
    )
    {
        var sb = new StringBuilder();

        sb.AppendLine("Ngày,Đơn đã thanh toán,Doanh thu,Chi phí nhập kho");

        foreach (var row in report.Rows)
        {
            sb.AppendLine(string.Join(",",
                row.Date.ToString("dd/MM/yyyy"),
                row.Orders,
                row.Revenue.ToString("N0", Vi),
                row.Cost.ToString("N0", Vi)
            ));
        }

        sb.AppendLine(string.Join(",",
            "Tổng",
            report.Orders,
            report.Revenue.ToString("N0", Vi),
            report.Cost.ToString("N0", Vi)
        ));

        string csv = sb.ToString();
        string fileName = $"BaoCao_{DateTime.Now:yyyyMMdd_HHmm}.csv";

        await _js.InvokeVoidAsync(
            "appHelpers.downloadCsv",
            fileName,
            csv
        );
    }

    /// <summary>
    /// Gọi window.print() để in hoặc lưu PDF.
    /// </summary>
    public async Task PrintPdfAsync()
    {
        await _js.InvokeVoidAsync("appHelpers.printPage");
    }
}

