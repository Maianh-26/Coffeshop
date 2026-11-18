using System.Globalization;
using CoffeeShop.Admin.Models;
using CoffeeShop.Admin.Services;
using CoffeeShop.Admin.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CoffeeShop.Admin.Pages.Dashboard;

/// <summary>
/// Tổng quan cho chủ quán: doanh thu (api/v1/bao-cao/doanh-thu), món bán chạy (mon-ban-chay),
/// doanh thu theo giờ (gio-cao-diem — chỉ khi xem 24 giờ qua hoặc một ngày).
/// Bộ lọc thời gian dùng chung lựa chọn với trang Đơn hàng / Kho (Utilities/DatePresets).
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
    private CancellationTokenSource? loadCts;   // lượt tải đang chạy; đổi bộ lọc giữa chừng thì hủy lượt cũ

    /// <summary>Mở trang / bấm Làm mới khi đang lọc → về 7 ngày qua.</summary>
    private const string DefaultPreset = DatePresets.Last7;

    /// <summary>Khoảng dài hơn số ngày này thì biểu đồ gom theo tháng.</summary>
    private const int MaxDailyPoints = 62;

    // ---- Bộ lọc: giống trang Đơn hàng — mốc nhanh áp dụng ngay, "Chọn khoảng ngày…" chỉ lọc khi bấm Áp dụng
    private string datePreset = DefaultPreset;      // lựa chọn đang hiện trong ô chọn
    private string appliedPreset = DefaultPreset;   // lựa chọn ĐANG ÁP DỤNG
    private DateTime? customFrom;                   // bản nháp 2 ô Từ ngày / Đến ngày
    private DateTime? customTo;
    private string? dateError;
    private DashboardFilter applied = new();        // khoảng của lần tải gần nhất

    // ---- Dữ liệu
    private BaoCaoDoanhThu? report;
    private List<MonBanChay> bestSellers = [];
    private List<DongBaoCao> points = [];           // mọi mốc của biểu đồ, kể cả mốc không có doanh thu
    private ChartScale scale = ChartScale.Day;
    private AxisScale axis = AxisScale.For(0);

    private bool loading;
    private bool exportOpen;
    private bool exporting;
    private string? error;
    private string range = "";

    private static readonly CultureInfo Vi = new("vi-VN");

    private enum ChartScale { Hour, Day, Month }

    private bool FiltersActive => appliedPreset != DefaultPreset;

    private string RefreshTitle => FiltersActive
        ? "Làm mới và về 7 ngày qua"
        : "Làm mới báo cáo";

    private string ScaleName => scale switch
    {
        ChartScale.Hour => "giờ",
        ChartScale.Month => "tháng",
        _ => "ngày"
    };

    /// <summary>Bảng chỉ liệt kê mốc có đơn; biểu đồ vẫn vẽ đủ mốc (cột 0) để trục thời gian liền mạch.</summary>
    private IEnumerable<DongBaoCao> TableRows => points.Where(p => p.SoDon > 0 || p.DoanhThu > 0);

    private static string Money(decimal value) => value.ToString("N0", Vi) + " đ";

    protected override Task OnInitializedAsync() => LoadAsync();

    // =========================================================================
    // KHOẢNG THỜI GIAN
    // =========================================================================

    /// <summary>
    /// Khoảng của một mốc nhanh, canh theo đơn vị của biểu đồ để mỗi cột là một giờ / một ngày trọn vẹn:
    /// 24 giờ qua = đúng 24 khung giờ (từ đầu giờ của 23 giờ trước tới hết giờ hiện tại — có mốc cuối để
    /// đơn tạo sau khi đồng hồ máy chủ qua giờ mới không bị gom chung với khung giờ đầu, vì API gom theo giờ trong ngày);
    /// 7 / 30 ngày qua = 7 / 30 ngày lịch, tính cả hôm nay.
    /// </summary>
    private static DashboardFilter RangeOf(string preset, DateTime now)
    {
        if (preset == DatePresets.Last24h)
        {
            DateTime start = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(-23);
            return new() { From = start, To = start.AddHours(24).AddMilliseconds(-3) };
        }

        return RangeOfDays(preset, now);
    }

    private static DashboardFilter RangeOfDays(string preset, DateTime now) => preset switch
    {
        DatePresets.Last7 => new() { From = now.Date.AddDays(-6) },
        DatePresets.Last30 => new() { From = now.Date.AddDays(-29) },
        _ => new()
    };

    private bool IsHourly(DashboardFilter f) =>
        appliedPreset == DatePresets.Last24h
        || (appliedPreset == DatePresets.Custom && f.From is { } from && f.To is { } to && from.Date == to.Date);

    private string Describe(DashboardFilter f)
    {
        if (f.From is not { } from) return "Đang xem doanh thu tất cả thời gian";

        // Mốc nhanh của trang này canh theo giờ / ngày lịch (khác khoảng trượt của trang Đơn hàng) → ghi rõ mốc đầu
        if (appliedPreset == DatePresets.Last24h)
            return $"Đang xem doanh thu từ {from:HH:mm dd/MM/yyyy} đến nay (24 khung giờ gần nhất)";

        if (f.To is { } to)
            return from.Date == to.Date
                ? $"Đang xem doanh thu ngày {from:dd/MM/yyyy}"
                : $"Đang xem doanh thu từ {from:dd/MM/yyyy} đến hết {to:dd/MM/yyyy}";

        int days = (DateTime.Today - from.Date).Days + 1;
        return $"Đang xem doanh thu từ 00:00 {from:dd/MM/yyyy} đến nay ({days} ngày gần nhất, gồm hôm nay)";
    }

    private async Task OnDatePresetChanged()
    {
        dateError = null;

        if (datePreset == DatePresets.Custom)
        {
            // Mở 2 ô Từ / Đến, điền sẵn khoảng đang xem (hoặc hôm nay). Chưa tải cho tới khi bấm Áp dụng.
            customFrom = (applied.From ?? DateTime.Today).Date;
            customTo = (applied.To ?? DateTime.Today).Date;
            return;
        }

        appliedPreset = datePreset;
        await LoadAsync();
    }

    /// <summary>Xóa lỗi một nhịp rồi mới gán, để vùng live đọc lại cả khi lỗi trùng lần trước.</summary>
    private async Task ShowDateError(string message)
    {
        if (dateError == message)
        {
            dateError = null;
            StateHasChanged();
            await Task.Yield();
        }

        dateError = message;
    }

    private async Task ApplyCustomRange()
    {
        // Chỉ chọn một đầu → xem đúng ngày đó
        if (DatePresets.ValidateCustom(customFrom, customTo, out DateTime from, out DateTime to) is { } rangeError)
        {
            await ShowDateError(rangeError);
            return;
        }

        dateError = null;
        customFrom = from;
        customTo = to;
        applied = new DashboardFilter { From = from, To = Query.EndOfDay(to) };
        appliedPreset = DatePresets.Custom;

        await LoadAsync();
    }

    private async Task RefreshAll()
    {
        if (FiltersActive || datePreset != appliedPreset)
        {
            datePreset = DefaultPreset;
            appliedPreset = DefaultPreset;
            dateError = null;
        }

        await LoadAsync();
    }

    // =========================================================================
    // TẢI DỮ LIỆU
    // =========================================================================

    private async Task LoadAsync()
    {
        loadCts?.Cancel();
        loadCts?.Dispose();
        loadCts = CancellationTokenSource.CreateLinkedTokenSource(pageCts.Token);
        CancellationToken token = loadCts.Token;

        // Mốc nhanh tính lại theo thời điểm bấm, để "24 giờ qua" luôn tính tới lúc này
        if (appliedPreset != DatePresets.Custom)
            applied = RangeOf(appliedPreset, DateTime.Now);

        var current = new DashboardFilter { From = applied.From, To = applied.To };
        bool hourly = IsHourly(current);

        loading = true;
        error = null;

        try
        {
            var revenueTask = Reports.GetRevenueAsync(current, token);
            var bestTask = Reports.GetBestSellersAsync(current, 5, token);
            var hoursTask = hourly ? Reports.GetPeakHoursAsync(current, token) : Task.FromResult(new List<GioCaoDiem>());

            await Task.WhenAll(revenueTask, bestTask, hoursTask);

            // Lượt này đã bị lượt mới thay thế → không ghi đè dữ liệu của lượt mới
            token.ThrowIfCancellationRequested();

            report = await revenueTask;
            bestSellers = await bestTask;

            (scale, points) = hourly
                ? (ChartScale.Hour, HourPoints(current.From!.Value, await hoursTask))
                : DayOrMonthPoints(current, report.TheoNgay);

            axis = AxisScale.For(points.Count == 0 ? 0 : points.Max(p => p.DoanhThu));
            range = Describe(current);
        }
        catch (ApiException ex) when (!token.IsCancellationRequested)
        {
            // ApiException.Message luôn là câu tiếng Việt dành cho người dùng
            error = ex.Message;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Đổi bộ lọc khi đang tải (lượt mới đã thay) hoặc rời trang
        }
        finally
        {
            if (!token.IsCancellationRequested) loading = false;
        }
    }

    /// <summary>24 khung giờ liên tiếp bắt đầu từ <paramref name="start"/> (đầu giờ).</summary>
    private static List<DongBaoCao> HourPoints(DateTime start, List<GioCaoDiem> hours)
    {
        var byHour = hours.ToDictionary(h => h.Gio);

        return Enumerable.Range(0, 24)
            .Select(i =>
            {
                DateTime slot = start.AddHours(i);
                byHour.TryGetValue(slot.Hour, out var h);

                // Qua nửa đêm (không phải cột đầu): ghi thêm ngày dưới "00:00" để biết đâu là hôm qua / hôm nay
                string? newDay = i > 0 && slot.Hour == 0 ? slot.ToString("dd/MM", Vi) : null;

                return new DongBaoCao(
                    slot.ToString("HH:mm", Vi),
                    $"{slot:HH:mm}–{slot.AddHours(1):HH:mm} {slot:dd/MM/yyyy}",
                    h?.SoDon ?? 0,
                    h?.DoanhThu ?? 0,
                    newDay);
            })
            .ToList();
    }

    /// <summary>Theo ngày (điền đủ ngày không bán, cột 0) nếu khoảng ≤ 62 ngày, dài hơn thì gom theo tháng.</summary>
    private static (ChartScale, List<DongBaoCao>) DayOrMonthPoints(DashboardFilter f, List<DoanhThuNgay> days)
    {
        var byDay = days.ToDictionary(d => d.Ngay);

        // "Toàn bộ thời gian": bắt đầu từ ngày đầu tiên có doanh thu
        DateOnly? first = f.From is { } from ? DateOnly.FromDateTime(from)
            : days.Count > 0 ? days.Min(d => d.Ngay) : null;

        if (first is not { } start) return (ChartScale.Day, []);

        DateOnly end = DateOnly.FromDateTime(f.To ?? DateTime.Today);
        if (days.Count > 0 && days.Max(d => d.Ngay) > end) end = days.Max(d => d.Ngay);

        int dayCount = end.DayNumber - start.DayNumber + 1;

        if (dayCount <= MaxDailyPoints)
        {
            return (ChartScale.Day, Enumerable.Range(0, dayCount)
                .Select(i =>
                {
                    DateOnly day = start.AddDays(i);
                    byDay.TryGetValue(day, out var d);
                    return new DongBaoCao(day.ToString("dd/MM", Vi), day.ToString("dd/MM/yyyy", Vi), d?.SoDon ?? 0, d?.DoanhThu ?? 0);
                })
                .ToList());
        }

        var months = new List<DongBaoCao>();

        for (var month = new DateOnly(start.Year, start.Month, 1); month <= end; month = month.AddMonths(1))
        {
            var inMonth = days.Where(d => d.Ngay.Year == month.Year && d.Ngay.Month == month.Month).ToList();
            string label = month.ToString("MM/yyyy", Vi);
            months.Add(new DongBaoCao(label, "Tháng " + label, inMonth.Sum(d => d.SoDon), inMonth.Sum(d => d.DoanhThu)));
        }

        return (ChartScale.Month, months);
    }

    // =========================================================================
    // XUẤT BÁO CÁO
    // =========================================================================

    private async Task ExportExcel()
    {
        if (report is null || exporting) return;

        exporting = true;

        try
        {
            await Exporter.ExportExcelAsync(report, TableRows.ToList(), ColumnTitle, bestSellers, range);
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

    private string ColumnTitle => scale switch
    {
        ChartScale.Hour => "Giờ",
        ChartScale.Month => "Tháng",
        _ => "Ngày"
    };

    // =========================================================================
    // BIỂU ĐỒ
    // =========================================================================

    /// <summary>
    /// Trục doanh thu tự co giãn theo mốc có doanh thu cao nhất, chia mốc tròn: bước = 1 / 2 / 2,5 / 5 × 10ⁿ đồng
    /// (tối thiểu 1.000 đ), tối đa 5 khoảng. Không có doanh thu → chỉ còn mốc 0 đ.
    /// Ví dụ đỉnh 600.000 đ → 0 · 200k · 400k · 600k; đỉnh 3.450.000 đ → 0 · 1tr · 2tr · 3tr · 4tr.
    /// </summary>
    private readonly record struct AxisScale(decimal Step, int Steps)
    {
        public decimal Top => Step * Steps;

        public static AxisScale For(decimal max)
        {
            if (max <= 0) return new AxisScale(0, 0);

            double raw = (double)max / 5;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double normalized = raw / magnitude;
            double nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 2.5 ? 2.5 : normalized <= 5 ? 5 : 10;

            decimal step = Math.Max(1_000m, (decimal)(nice * magnitude));
            int steps = Math.Max(1, (int)Math.Ceiling(max / step));

            return new AxisScale(step, steps);
        }
    }

    /// <summary>
    /// Số tiền gọn cho trục tiền: 200k, 1,5tr (số đầy đủ nằm trong thẻ số liệu và bảng).
    /// Làm tròn trước rồi mới xét ngưỡng, để 999.950 đ hiện "1tr" chứ không phải "1000k".
    /// </summary>
    private static string Compact(decimal value)
    {
        decimal thousands = Math.Round(value / 1_000m, 1);

        if (thousands >= 1_000m) return Math.Round(value / 1_000_000m, 2).ToString("0.##", Vi) + "tr";
        if (Math.Round(value) >= 1_000m) return thousands.ToString("0.#", Vi) + "k";

        return value.ToString("N0", Vi);
    }

    /// <summary>Nhãn trục tiền: "0 đ", "200k đ", "1,5tr đ".</summary>
    private static string AxisLabel(decimal value) => value <= 0 ? "0 đ" : Compact(value) + " đ";

    private string Percent(decimal value) =>
        (axis.Top <= 0 ? 0 : Math.Min(100m, value / axis.Top * 100m)).ToString("0.##", CultureInfo.InvariantCulture);

    private string BarHeight(decimal value) => $"height:{Percent(value)}%";

    private string AxisPosition(int index) => axis.Steps == 0
        ? "top:100%"
        : $"top:{((axis.Steps - index) / (decimal)axis.Steps * 100m).ToString("0.##", CultureInfo.InvariantCulture)}%";

    private static string BarTitle(DongBaoCao p) => $"{p.MocDayDu}: {Money(p.DoanhThu)} · {p.SoDon} đơn";

    // Cột co theo khung (không cuộn ngang). Nhãn trục thời gian thưa dần khi nhiều mốc:
    // màn hình rộng ghi tối đa ~15 nhãn, màn hình hẹp chỉ giữ các nhãn "chính" (tối đa ~6) — CSS ẩn nhãn .is-minor.
    // Nhịp chính là bội số của nhịp phụ, và cả hai đếm từ cột 00:00 (nếu có) để nhãn ngày không dính nhãn bên cạnh.
    private int MinorEvery => points.Count <= 16 ? 1 : (int)Math.Ceiling(points.Count / 15.0);

    private int MajorEvery
    {
        get
        {
            if (points.Count <= 8) return 1;
            int minor = MinorEvery;
            return minor * (int)Math.Ceiling(Math.Ceiling(points.Count / 6.0) / minor);
        }
    }

    private int LabelAnchor => Math.Max(0, points.FindIndex(p => p.MocPhu is not null));

    private bool OnBeat(int index, int every) => ((index - LabelAnchor) % every + every) % every == 0;

    private bool ShowLabel(int index) => OnBeat(index, MinorEvery);

    private bool IsMajorLabel(int index) => OnBeat(index, MajorEvery);

    /// <summary>
    /// Thẻ số liệu: 1/3 số cột bên trái canh theo mép trái cột, 1/3 bên phải canh theo mép phải,
    /// ở giữa canh giữa — để thẻ (~170px) không tràn khỏi khung cả khi cột chỉ rộng vài px trên điện thoại.
    /// </summary>
    private string TipSide(int index) =>
        index * 3 < points.Count ? "tip-start"
        : index * 3 >= points.Count * 2 ? "tip-end"
        : "";

    /// <summary>Chiều cao cột (%) làm biến CSS để thẻ số liệu nằm ngay trên đỉnh cột, không che cột.</summary>
    private string BarVar(decimal value) => $"--h:{Percent(value)}%";

    public void Dispose()
    {
        loadCts?.Cancel();
        loadCts?.Dispose();
        pageCts.Cancel();
        pageCts.Dispose();
    }
}
