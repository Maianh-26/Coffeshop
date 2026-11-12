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
    /// Xuất doanh thu theo ngày và món bán chạy ra file CSV (tương thích Excel).
    /// </summary>
    public async Task ExportExcelAsync(
        BaoCaoDoanhThu report,
        IReadOnlyList<MonBanChay> bestSellers,
        string dateRange)
    {
        var sb = new StringBuilder();

        sb.AppendLine(Csv("Báo cáo doanh thu", dateRange));
        sb.AppendLine();
        sb.AppendLine(Csv("Ngày", "Đơn đã thanh toán", "Doanh thu"));

        foreach (var row in report.TheoNgay)
        {
            sb.AppendLine(Csv(row.Ngay.ToString("dd/MM/yyyy", Vi), row.SoDon.ToString(Vi), row.DoanhThu.ToString("N0", Vi)));
        }

        sb.AppendLine(Csv("Tổng", report.SoDonDaThanhToan.ToString(Vi), report.DoanhThu.ToString("N0", Vi)));
        sb.AppendLine();
        sb.AppendLine(Csv("Đơn chờ thanh toán", report.SoDonChoThanhToan.ToString(Vi)));
        sb.AppendLine(Csv("Đơn đã hủy", report.SoDonHuy.ToString(Vi)));
        sb.AppendLine(Csv("Giá trị đơn trung bình", report.GiaTriDonTrungBinh.ToString("N0", Vi)));

        if (bestSellers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Csv("Món bán chạy", "Số lượng", "Doanh thu"));

            foreach (var item in bestSellers)
            {
                sb.AppendLine(Csv(item.TenMon, item.SoLuong.ToString(Vi), item.DoanhThu.ToString("N0", Vi)));
            }
        }

        string fileName = $"BaoCao_{DateTime.Now:yyyyMMdd_HHmm}.csv";

        await _js.InvokeVoidAsync("appHelpers.downloadCsv", fileName, sb.ToString());
    }

    /// <summary>
    /// Gọi window.print() để in hoặc lưu PDF.
    /// </summary>
    public async Task PrintPdfAsync()
    {
        await _js.InvokeVoidAsync("appHelpers.printPage");
    }

    /// <summary>Một dòng CSV: bọc ô trong dấu nháy kép (số "29.000" có dấu chấm, tên món có dấu phẩy).</summary>
    private static string Csv(params string[] cells) =>
        string.Join(",", cells.Select(c => "\"" + c.Replace("\"", "\"\"") + "\""));
}
