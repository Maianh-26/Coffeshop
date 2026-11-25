using System.Globalization;
using CoffeeShop.Admin.Models;
using CoffeeShop.Admin.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Xuất báo cáo doanh thu của trang Tổng quan ra file Excel (.xlsx) — dạng file dữ liệu báo cáo, không có chữ giải thích:
/// Tổng hợp · Doanh thu theo giờ / ngày / tháng · Món bán chạy · Topping bán chạy.
/// Mỗi trang tính: tên quán, tiêu đề, kỳ báo cáo chính xác, ngày lập, rồi bảng số liệu (số thật, ngày thật,
/// dòng Tổng bằng công thức) để chủ quán lọc / sắp xếp / tính tiếp trong Excel.
/// Tỷ trọng chỉ có ở Tổng hợp (tiền mặt / chuyển khoản) và Món bán chạy (chủ quán chọn 23/11/2026).
/// </summary>
public class ReportExporter
{
    /// <summary>Hàm JS trong wwwroot/js/app.js (window.appHelpers).</summary>
    internal const string DownloadFileFunction = "appHelpers.downloadFile";

    private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly CultureInfo Vi = new("vi-VN");

    private readonly IJSRuntime _js;
    private readonly IConfiguration _config;

    public ReportExporter(IJSRuntime js, IConfiguration config)
    {
        _js = js;
        _config = config;
    }

    /// <summary>Thông tin quán in ở đầu báo cáo (cùng mục "Receipt" của hóa đơn trong appsettings.json).</summary>
    public sealed record ThongTinQuan(string Ten, string? DiaChi, string? DienThoai);

    /// <summary>Dựng file .xlsx rồi tải về máy (một lần gọi JS, gửi thẳng mảng byte).</summary>
    public async Task ExportExcelAsync(BaoCaoXuat data)
    {
        var shop = new ThongTinQuan(
            _config["Receipt:ShopName"] is { Length: > 0 } name ? name : "COFFEE SHOP",
            _config["Receipt:Address"],
            _config["Receipt:Phone"]);

        byte[] bytes = Build(data, shop);
        await _js.InvokeVoidAsync(DownloadFileFunction, FileName(data), bytes, XlsxMime);
    }

    /// <summary>BaoCaoDoanhThu_20261123.xlsx (một ngày) hoặc BaoCaoDoanhThu_20261117-20261123.xlsx.</summary>
    public static string FileName(BaoCaoXuat data) => data.TuLuc.Date == data.DenLuc.Date
        ? $"BaoCaoDoanhThu_{data.TuLuc:yyyyMMdd}.xlsx"
        : $"BaoCaoDoanhThu_{data.TuLuc:yyyyMMdd}-{data.DenLuc:yyyyMMdd}.xlsx";

    /// <summary>Dựng nội dung file (tách riêng để kiểm thử không cần trình duyệt).</summary>
    public static byte[] Build(BaoCaoXuat data, ThongTinQuan shop)
    {
        var book = new XlsxWriter();

        SummarySheet(book.AddSheet("Tổng hợp"), data, shop);
        TimelineSheet(book.AddSheet($"Doanh thu theo {UnitName(data.DonVi)}"), data, shop);
        DishesSheet(book.AddSheet("Món bán chạy"), data, shop);
        ToppingsSheet(book.AddSheet("Topping bán chạy"), data, shop);

        return book.ToBytes();
    }

    // -------------------------------------------------------------------------
    // Phần đầu mỗi trang tính
    // -------------------------------------------------------------------------

    private static string UnitName(DonViMoc unit) => unit switch
    {
        DonViMoc.Gio => "giờ",
        DonViMoc.Thang => "tháng",
        _ => "ngày"
    };

    private static void Heading(XlsxSheet sheet, string title, BaoCaoXuat data, ThongTinQuan shop, int columns)
    {
        sheet.Row(XlsxCell.Str(shop.Ten, XlsxStyle.Section)).MergeLastRow(1, columns);

        string contact = string.Join(" · ", new[] { shop.DiaChi, shop.DienThoai is { Length: > 0 } p ? "ĐT: " + p : null }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (contact.Length > 0)
            sheet.Row(XlsxCell.Str(contact, XlsxStyle.Note)).MergeLastRow(1, columns);

        sheet.BlankRow();
        sheet.Row(XlsxCell.Str(title, XlsxStyle.Title)).MergeLastRow(1, columns);
        sheet.Row(XlsxCell.Str($"Kỳ báo cáo: từ {data.TuLuc:HH:mm dd/MM/yyyy} đến {data.DenLuc:HH:mm dd/MM/yyyy}", XlsxStyle.Default))
            .MergeLastRow(1, columns);
        sheet.Row(XlsxCell.Str($"Ngày lập: {data.XuatLuc:HH:mm dd/MM/yyyy}", XlsxStyle.Note)).MergeLastRow(1, columns);
        sheet.BlankRow();
    }

    private static XlsxCell[] Header(params string[] titles) => titles.Select(t => XlsxCell.Str(t, XlsxStyle.Header)).ToArray();

    private static decimal Share(decimal part, decimal total) => total == 0 ? 0 : Math.Round(part / total, 4, MidpointRounding.AwayFromZero);

    /// <summary>Làm tròn tới đồng kiểu .5 lên — khớp hàm ROUND của Excel (giá trị tính sẵn của ô công thức).</summary>
    private static decimal RoundVnd(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

    // -------------------------------------------------------------------------
    // Trang 1 — Tổng hợp
    // -------------------------------------------------------------------------

    private static void SummarySheet(XlsxSheet sheet, BaoCaoXuat data, ThongTinQuan shop)
    {
        var r = data.TongQuan;

        sheet.Widths(34, 20, 14);
        Heading(sheet, "BÁO CÁO DOANH THU", data, shop, 3);

        // 1. Doanh thu theo hình thức thanh toán; dòng Doanh thu thuần = tổng các hình thức
        var methods = new[] { PhuongThucThanhToans.Cash, PhuongThucThanhToans.QrTransfer }
            .Concat(r.TheoPhuongThuc.Keys.Where(k => k is not (PhuongThucThanhToans.Cash or PhuongThucThanhToans.QrTransfer)).Order())
            .ToList();

        sheet.Row(XlsxCell.Str("1. DOANH THU", XlsxStyle.Section));
        sheet.Row(Header("Khoản mục", "Số tiền", "Tỷ trọng"));

        int first = sheet.RowCount + 1;
        int total = first + methods.Count;
        string totalRef = $"$B${total}";

        foreach (string method in methods)
        {
            decimal amount = r.TheoPhuongThuc.GetValueOrDefault(method);
            int row = sheet.RowCount + 1;
            sheet.Row(
                XlsxCell.Str(PhuongThucThanhToans.Label(method)),
                XlsxCell.Num(amount, XlsxStyle.Money),
                XlsxCell.Calc($"IF({totalRef}=0,0,B{row}/{totalRef})", Share(amount, r.DoanhThu), XlsxStyle.Percent));
        }

        sheet.Row(
            XlsxCell.Str("Doanh thu thuần", XlsxStyle.TotalText),
            XlsxCell.Sum($"B{first}:B{total - 1}", r.DoanhThu, XlsxStyle.TotalMoney),
            XlsxCell.Calc($"IF({totalRef}=0,0,1)", r.DoanhThu == 0 ? 0 : 1, XlsxStyle.TotalPercent));

        sheet.Row(
            XlsxCell.Str("Giá trị trung bình / đơn"),
            XlsxCell.Num(r.GiaTriDonTrungBinh, XlsxStyle.Money),
            XlsxCell.Empty(XlsxStyle.Text));

        sheet.BlankRow();

        // 2. Đơn hàng
        sheet.Row(XlsxCell.Str("2. ĐƠN HÀNG", XlsxStyle.Section));
        sheet.Row(Header("Chỉ tiêu", "Số đơn"));
        sheet.Row(XlsxCell.Str("Đơn đã thanh toán"), XlsxCell.Num(r.SoDonDaThanhToan));
        sheet.Row(XlsxCell.Str("Đơn chờ thanh toán"), XlsxCell.Num(r.SoDonChoThanhToan));
        sheet.Row(XlsxCell.Str("Đơn đã hủy"), XlsxCell.Num(r.SoDonHuy));
        sheet.Row(XlsxCell.Str("Đơn hoàn tiền"), XlsxCell.Num(r.SoDonHoanTien));
    }

    // -------------------------------------------------------------------------
    // Trang 2 — Doanh thu theo giờ / ngày / tháng
    // -------------------------------------------------------------------------

    private static void TimelineSheet(XlsxSheet sheet, BaoCaoXuat data, ThongTinQuan shop)
    {
        bool monthly = data.DonVi == DonViMoc.Thang;
        bool hourly = data.DonVi == DonViMoc.Gio;

        // Tháng: Tháng · Số đơn · Doanh thu thuần · TB/đơn
        // Ngày:  Ngày · Thứ · ...   Giờ: Ngày · Khung giờ · ...
        // Không có cột tỷ trọng (chủ quán 23/11/2026): cột doanh thu đã đủ để so sánh, 24 giờ / 30 ngày toàn "0,0%" gây rối.
        string[] titles = monthly
            ? ["Tháng", "Số đơn", "Doanh thu thuần", "Trung bình / đơn"]
            : ["Ngày", hourly ? "Khung giờ" : "Thứ", "Số đơn", "Doanh thu thuần", "Trung bình / đơn"];
        int columns = titles.Length;

        sheet.Widths(monthly ? [14, 10, 20, 18] : [14, 15, 10, 20, 18]);
        Heading(sheet, $"DOANH THU THEO {UnitName(data.DonVi).ToUpper(Vi)}", data, shop, columns);

        sheet.Row(Header(titles));
        sheet.FreezeRows(sheet.RowCount);

        // Cột số đơn / doanh thu
        string cnt = monthly ? "B" : "C", rev = monthly ? "C" : "D";

        // Phòng thủ: trang luôn gửi ít nhất một mốc, nhưng file vẫn hợp lệ nếu danh sách rỗng
        if (data.TheoMoc.Count == 0)
        {
            sheet.Row(XlsxCell.Str("Không có doanh thu trong kỳ báo cáo"));
            return;
        }

        int first = sheet.RowCount + 1;
        int total = first + data.TheoMoc.Count;
        decimal revenueTotal = data.TheoMoc.Sum(p => p.DoanhThu);
        int ordersTotal = data.TheoMoc.Sum(p => p.SoDon);

        foreach (var p in data.TheoMoc)
        {
            int row = sheet.RowCount + 1;
            var cells = new List<XlsxCell>
            {
                XlsxCell.Day(p.BatDau, monthly ? XlsxStyle.Month : XlsxStyle.Date)
            };

            if (!monthly)
            {
                cells.Add(XlsxCell.Str(hourly
                    ? $"{p.BatDau:HH:mm} – {p.BatDau.AddHours(1):HH:mm}"
                    : Vi.DateTimeFormat.GetDayName(p.BatDau.DayOfWeek)));
            }

            cells.Add(XlsxCell.Num(p.SoDon));
            cells.Add(XlsxCell.Num(p.DoanhThu, XlsxStyle.Money));
            cells.Add(XlsxCell.Calc($"IF({cnt}{row}=0,0,ROUND({rev}{row}/{cnt}{row},0))",
                p.SoDon == 0 ? 0 : RoundVnd(p.DoanhThu / p.SoDon), XlsxStyle.Money));

            sheet.Row(cells.ToArray());
        }

        var totals = new List<XlsxCell> { XlsxCell.Str("Tổng", XlsxStyle.TotalText) };
        if (!monthly) totals.Add(XlsxCell.Empty(XlsxStyle.TotalText));
        totals.Add(XlsxCell.Sum($"{cnt}{first}:{cnt}{total - 1}", ordersTotal, XlsxStyle.TotalInteger));
        totals.Add(XlsxCell.Sum($"{rev}{first}:{rev}{total - 1}", revenueTotal, XlsxStyle.TotalMoney));
        totals.Add(XlsxCell.Calc($"IF({cnt}{total}=0,0,ROUND({rev}{total}/{cnt}{total},0))",
            ordersTotal == 0 ? 0 : RoundVnd(revenueTotal / ordersTotal), XlsxStyle.TotalMoney));
        sheet.Row(totals.ToArray());
    }

    // -------------------------------------------------------------------------
    // Trang 3 — Món bán chạy
    // -------------------------------------------------------------------------

    private static void DishesSheet(XlsxSheet sheet, BaoCaoXuat data, ThongTinQuan shop)
    {
        decimal net = data.TongQuan.DoanhThu;

        sheet.Widths(7, 30, 11, 18, 20, 18, 16);
        Heading(sheet, "MÓN BÁN CHẠY", data, shop, 7);

        sheet.Row(Header("Hạng", "Tên món", "Số lượng", "Doanh thu món", "Doanh thu topping kèm", "Tổng doanh thu", "Tỷ trọng / doanh thu thuần"));
        sheet.FreezeRows(sheet.RowCount);

        if (data.Mon.Count == 0)
        {
            sheet.Row(XlsxCell.Empty(XlsxStyle.Text), XlsxCell.Str("Không có món nào được bán trong kỳ báo cáo"));
            return;
        }

        int first = sheet.RowCount + 1;

        for (int i = 0; i < data.Mon.Count; i++)
        {
            var m = data.Mon[i];
            decimal sum = m.DoanhThuGomTopping;

            // Tỷ trọng so với doanh thu thuần cả kỳ (trang Tổng hợp)
            sheet.Row(
                XlsxCell.Num(i + 1),
                XlsxCell.Str(m.TenMon),
                XlsxCell.Num(m.SoLuong),
                XlsxCell.Num(m.DoanhThu, XlsxStyle.Money),
                XlsxCell.Num(sum - m.DoanhThu, XlsxStyle.Money),
                XlsxCell.Num(sum, XlsxStyle.Money),
                XlsxCell.Num(Share(sum, net), XlsxStyle.Percent));
        }

        int last = sheet.RowCount;
        decimal dishesTotal = data.Mon.Sum(m => m.DoanhThuGomTopping);

        sheet.Row(
            XlsxCell.Empty(XlsxStyle.TotalText),
            XlsxCell.Str($"Tổng {data.Mon.Count} món", XlsxStyle.TotalText),
            XlsxCell.Sum($"C{first}:C{last}", data.Mon.Sum(m => m.SoLuong), XlsxStyle.TotalInteger),
            XlsxCell.Sum($"D{first}:D{last}", data.Mon.Sum(m => m.DoanhThu), XlsxStyle.TotalMoney),
            XlsxCell.Sum($"E{first}:E{last}", data.Mon.Sum(m => m.DoanhThuGomTopping - m.DoanhThu), XlsxStyle.TotalMoney),
            XlsxCell.Sum($"F{first}:F{last}", dishesTotal, XlsxStyle.TotalMoney),
            XlsxCell.Sum($"G{first}:G{last}", data.Mon.Sum(m => Share(m.DoanhThuGomTopping, net)), XlsxStyle.TotalPercent));
    }

    // -------------------------------------------------------------------------
    // Trang 4 — Topping bán chạy
    // -------------------------------------------------------------------------

    private static void ToppingsSheet(XlsxSheet sheet, BaoCaoXuat data, ThongTinQuan shop)
    {
        // Không có cột tỷ trọng: tiền topping đã nằm trong "Tổng doanh thu" của món → cộng tỷ trọng 2 trang sẽ bị trùng
        sheet.Widths(7, 30, 11, 18);
        Heading(sheet, "TOPPING BÁN CHẠY", data, shop, 4);

        sheet.Row(Header("Hạng", "Tên topping", "Số ly", "Doanh thu"));
        sheet.FreezeRows(sheet.RowCount);

        if (data.Topping is null || data.Topping.Count == 0)
        {
            string message = data.Topping is null
                ? "Máy chủ chưa hỗ trợ báo cáo topping"
                : "Không có topping nào được bán trong kỳ báo cáo";
            sheet.Row(XlsxCell.Empty(XlsxStyle.Text), XlsxCell.Str(message));
            return;
        }

        int first = sheet.RowCount + 1;

        for (int i = 0; i < data.Topping.Count; i++)
        {
            var t = data.Topping[i];
            sheet.Row(
                XlsxCell.Num(i + 1),
                XlsxCell.Str(t.TenTopping),
                XlsxCell.Num(t.SoLuong),
                XlsxCell.Num(t.DoanhThu, XlsxStyle.Money));
        }

        int last = sheet.RowCount;

        sheet.Row(
            XlsxCell.Empty(XlsxStyle.TotalText),
            XlsxCell.Str($"Tổng {data.Topping.Count} topping", XlsxStyle.TotalText),
            XlsxCell.Sum($"C{first}:C{last}", data.Topping.Sum(t => t.SoLuong), XlsxStyle.TotalInteger),
            XlsxCell.Sum($"D{first}:D{last}", data.Topping.Sum(t => t.DoanhThu), XlsxStyle.TotalMoney));
    }
}
