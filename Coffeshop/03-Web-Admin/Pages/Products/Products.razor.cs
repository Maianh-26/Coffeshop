using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CoffeeShop.Admin.Models;
using CoffeeShop.Admin.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CoffeeShop.Admin.Pages.Products;

/// <summary>
/// Quản lý thực đơn: món (giá theo size), danh mục, topping, kích thước và công thức (định mức nguyên liệu).
/// API: api/v1/mon, danh-muc, topping, kich-thuoc, chi-tiet-mon/{id}/cong-thuc, topping/{id}/cong-thuc.
/// </summary>
public partial class Products : IDisposable
{
    [Inject] private ProductService ProductApi { get; set; } = default!;
    [Inject] private CategoryService CategoryApi { get; set; } = default!;
    [Inject] private ToppingService ToppingApi { get; set; } = default!;
    [Inject] private PromotionService PromotionApi { get; set; } = default!;
    [Inject] private InventoryService InventoryApi { get; set; } = default!;

    private readonly CancellationTokenSource pageCts = new();

    private List<Mon> products = [];
    private List<DanhMuc> categories = [];
    private List<Topping> toppings = [];
    private List<KichThuoc> sizes = [];
    private List<KhuyenMai> promotions = [];
    private List<NguyenLieu> ingredients = [];

    /// <summary>"products" | "categories" | "toppings" | "sizes"</summary>
    private string activeTab = "products";
    private bool ProductTab => activeTab == "products";
    private bool CategoryTab => activeTab == "categories";
    private bool ToppingTab => activeTab == "toppings";
    private bool SizeTab => activeTab == "sizes";

    private bool loading;
    private bool saving;
    private bool editing;
    private string? loadError;
    private string? error;
    private string? message;

    private int page = 1;
    private const int PageSize = 8;

    private string search = "";
    private string categoryFilter = "";
    private string statusFilter = "";

    /// <summary>Khóa dòng đang chọn: số (món/danh mục/topping) hoặc mã kích thước.</summary>
    private string? selected;

    /// <summary>"" | "product" | "category" | "topping" | "size" | "delete" | "recipe"</summary>
    private string dialog = "";

    private ProductForm productModel = new();
    private NameForm nameModel = new();
    private ToppingForm toppingModel = new();
    private SizeForm sizeModel = new();

    // Công thức
    private int recipeOwner;      // MaChiTietMon (món) hoặc MaTopping
    private bool recipeLoading;
    private List<RecipeRow> recipe = [];

    private static readonly CultureInfo Vi = new("vi-VN");

    private static string Money(decimal value) => value.ToString("N0", Vi) + " đ";

    // =====================================================================
    // FORM
    // =====================================================================

    public class ProductForm
    {
        public int MaTenMon { get; set; }

        [Required(ErrorMessage = "Nhập tên món.")]
        [StringLength(150, ErrorMessage = "Tên món tối đa 150 ký tự.")]
        public string TenMon { get; set; } = "";

        [Range(1, int.MaxValue, ErrorMessage = "Chọn danh mục.")]
        public int MaDanhMuc { get; set; }

        [StringLength(255, ErrorMessage = "Đường dẫn ảnh tối đa 255 ký tự.")]
        public string? HinhAnh { get; set; }

        /// <summary>0 = không áp khuyến mãi.</summary>
        public int MaKhuyenMai { get; set; }

        public bool ConHang { get; set; } = true;
        public bool NgungBan { get; set; }

        public List<SizePrice> Prices { get; set; } = [];
    }

    public class SizePrice
    {
        public string MaKichThuoc { get; set; } = "";
        public string TenKichThuoc { get; set; } = "";
        public bool Ban { get; set; }
        public decimal? GiaBan { get; set; }
    }

    public class NameForm
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nhập tên danh mục.")]
        [StringLength(100, ErrorMessage = "Tên tối đa 100 ký tự.")]
        public string Ten { get; set; } = "";
    }

    public class ToppingForm
    {
        public int MaTopping { get; set; }

        [Required(ErrorMessage = "Nhập tên topping.")]
        [StringLength(100, ErrorMessage = "Tên tối đa 100 ký tự.")]
        public string TenTopping { get; set; } = "";

        [Required(ErrorMessage = "Nhập giá thêm.")]
        [Range(0, 1_000_000_000, ErrorMessage = "Giá thêm không được âm.")]
        public decimal? GiaThem { get; set; }

        public bool ConHang { get; set; } = true;
        public bool NgungBan { get; set; }
    }

    public class SizeForm
    {
        [Required(ErrorMessage = "Nhập mã kích thước.")]
        [RegularExpression("^[A-Za-z0-9]{1,10}$", ErrorMessage = "Mã 1–10 ký tự chữ không dấu hoặc số (ví dụ: S, M, L, XL).")]
        public string MaKichThuoc { get; set; } = "";

        [Required(ErrorMessage = "Nhập tên kích thước.")]
        [StringLength(50, ErrorMessage = "Tên tối đa 50 ký tự.")]
        public string TenKichThuoc { get; set; } = "";
    }

    public class RecipeRow
    {
        public int MaNguyenLieu { get; set; }
        public decimal? LuongSuDung { get; set; }
    }

    // =====================================================================
    // DANH SÁCH & LỌC
    // =====================================================================

    private IEnumerable<Mon> ProductMatches =>
        products.Where(p =>
            (search == ""
             || p.MaTenMon.ToString().Contains(search, StringComparison.OrdinalIgnoreCase)
             || p.TenMon.Contains(search, StringComparison.OrdinalIgnoreCase))
            && (categoryFilter == "" || p.MaDanhMuc.ToString() == categoryFilter)
            && (statusFilter == "" || TrangThaiMon.Of(p) == statusFilter));

    private IEnumerable<DanhMuc> CategoryMatches =>
        categories.Where(c =>
            search == ""
            || c.MaDanhMuc.ToString().Contains(search, StringComparison.OrdinalIgnoreCase)
            || c.TenDanhMuc.Contains(search, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<Topping> ToppingMatches =>
        toppings.Where(t =>
            (search == ""
             || t.MaTopping.ToString().Contains(search, StringComparison.OrdinalIgnoreCase)
             || t.TenTopping.Contains(search, StringComparison.OrdinalIgnoreCase))
            && (statusFilter == "" || TrangThaiMon.Of(t) == statusFilter));

    private IEnumerable<KichThuoc> SizeMatches =>
        sizes.Where(s =>
            search == ""
            || s.MaKichThuoc.Contains(search, StringComparison.OrdinalIgnoreCase)
            || s.TenKichThuoc.Contains(search, StringComparison.OrdinalIgnoreCase));

    private int Count =>
        ToppingTab ? ToppingMatches.Count()
        : CategoryTab ? CategoryMatches.Count()
        : SizeTab ? SizeMatches.Count()
        : ProductMatches.Count();

    private int Pages => Math.Max(1, (Count + PageSize - 1) / PageSize);

    private IEnumerable<T> Paged<T>(IEnumerable<T> items) => items.Skip((page - 1) * PageSize).Take(PageSize);

    private Mon? SelectedProduct => ProductTab ? products.FirstOrDefault(p => p.MaTenMon.ToString() == selected) : null;
    private DanhMuc? SelectedCategory => CategoryTab ? categories.FirstOrDefault(c => c.MaDanhMuc.ToString() == selected) : null;
    private Topping? SelectedTopping => ToppingTab ? toppings.FirstOrDefault(t => t.MaTopping.ToString() == selected) : null;
    private KichThuoc? SelectedSize => SizeTab ? sizes.FirstOrDefault(s => s.MaKichThuoc == selected) : null;

    private bool HasSelection => SelectedProduct is not null || SelectedCategory is not null
                                 || SelectedTopping is not null || SelectedSize is not null;

    private string SelectedName =>
        SelectedProduct?.TenMon ?? SelectedCategory?.TenDanhMuc ?? SelectedTopping?.TenTopping
        ?? (SelectedSize is { } s ? $"{s.MaKichThuoc} – {s.TenKichThuoc}" : "");

    private string DialogTitle => dialog switch
    {
        "product" => editing ? "Sửa món" : "Thêm món mới",
        "category" => editing ? "Sửa danh mục" : "Thêm danh mục",
        "topping" => editing ? "Sửa topping" : "Thêm topping",
        "size" => editing ? "Sửa kích thước" : "Thêm kích thước",
        "delete" => ProductTab ? "Ngừng bán món" : ToppingTab ? "Ngừng bán topping" : CategoryTab ? "Xóa danh mục" : "Xóa kích thước",
        "recipe" => ToppingTab ? "Định mức nguyên liệu topping" : "Định mức nguyên liệu món",
        _ => ""
    };

    /// <summary>Giá theo size đang bán, ví dụ "M 29.000 đ · L 35.000 đ".</summary>
    private static IEnumerable<GiaTheoSize> ActiveSizes(Mon m) => m.KichThuoc.Where(s => s.DangApDung);

    protected override Task OnInitializedAsync() => Load();

    private async Task Load()
    {
        loading = true;
        loadError = null;

        try
        {
            var productTask = ProductApi.GetAllAsync(pageCts.Token);
            var categoryTask = CategoryApi.GetAllAsync(pageCts.Token);
            var toppingTask = ToppingApi.GetAllAsync(includeStopped: true, pageCts.Token);
            var sizeTask = CategoryApi.GetSizesAsync(pageCts.Token);
            var promotionTask = PromotionApi.GetAllAsync(pageCts.Token);

            products = await productTask;
            categories = await categoryTask;
            toppings = await toppingTask;
            sizes = await sizeTask;
            promotions = await promotionTask;

            page = Math.Clamp(page, 1, Pages);
        }
        catch (ApiException ex)
        {
            loadError = ex.Message;
        }
        catch (OperationCanceledException) when (pageCts.IsCancellationRequested)
        {
        }
        finally
        {
            loading = false;
        }
    }

    private void SelectKey(KeyboardEventArgs e, string key)
    {
        if (e.Key is "Enter" or " ") selected = key;
    }

    private void Filter()
    {
        selected = null;
        page = 1;
    }

    private void Reset()
    {
        search = "";
        categoryFilter = "";
        statusFilter = "";
        Filter();
    }

    private void SwitchTab(string tab)
    {
        activeTab = tab;
        message = null;
        Reset();
    }

    private void Move(int delta)
    {
        int next = page + delta;
        if (next < 1 || next > Pages) return;

        page = next;
        selected = null;
    }

    private void Prepare()
    {
        error = null;
        message = null;
    }

    private void Close()
    {
        if (saving) return;

        dialog = "";
        error = null;
    }

    // =====================================================================
    // THÊM / SỬA
    // =====================================================================

    private void Add()
    {
        Prepare();
        editing = false;

        switch (activeTab)
        {
            case "categories":
                nameModel = new NameForm();
                dialog = "category";
                break;
            case "toppings":
                toppingModel = new ToppingForm();
                dialog = "topping";
                break;
            case "sizes":
                sizeModel = new SizeForm();
                dialog = "size";
                break;
            default:
                productModel = new ProductForm
                {
                    MaDanhMuc = categories.FirstOrDefault()?.MaDanhMuc ?? 0,
                    Prices = sizes.Select(s => new SizePrice { MaKichThuoc = s.MaKichThuoc, TenKichThuoc = s.TenKichThuoc }).ToList()
                };
                dialog = "product";
                break;
        }
    }

    private void Edit()
    {
        Prepare();
        editing = true;

        if (SelectedProduct is { } p)
        {
            productModel = new ProductForm
            {
                MaTenMon = p.MaTenMon,
                TenMon = p.TenMon,
                MaDanhMuc = p.MaDanhMuc,
                HinhAnh = p.HinhAnh,
                MaKhuyenMai = p.KhuyenMai?.MaKhuyenMai ?? 0,
                ConHang = p.ConHang,
                NgungBan = p.NgungBan,
                Prices = sizes.Select(s =>
                {
                    var price = p.KichThuoc.FirstOrDefault(k => k.MaKichThuoc == s.MaKichThuoc);
                    return new SizePrice
                    {
                        MaKichThuoc = s.MaKichThuoc,
                        TenKichThuoc = s.TenKichThuoc,
                        Ban = price?.DangApDung ?? false,
                        GiaBan = price?.GiaBan
                    };
                }).ToList()
            };
            dialog = "product";
        }
        else if (SelectedCategory is { } c)
        {
            nameModel = new NameForm { Id = c.MaDanhMuc, Ten = c.TenDanhMuc };
            dialog = "category";
        }
        else if (SelectedTopping is { } t)
        {
            toppingModel = new ToppingForm
            {
                MaTopping = t.MaTopping, TenTopping = t.TenTopping, GiaThem = t.GiaThem, ConHang = t.ConHang, NgungBan = t.NgungBan
            };
            dialog = "topping";
        }
        else if (SelectedSize is { } s)
        {
            sizeModel = new SizeForm { MaKichThuoc = s.MaKichThuoc, TenKichThuoc = s.TenKichThuoc };
            dialog = "size";
        }
    }

    /// <summary>Gọi API ghi: khóa nút, báo lỗi trong hộp thoại, đóng hộp thoại và tải lại khi xong.</summary>
    private async Task Execute(Func<Task<string>> action)
    {
        if (saving) return;

        saving = true;
        error = null;

        try
        {
            message = await action();
            dialog = "";
            await Load();
        }
        catch (ApiException ex)
        {
            error = ex.Message;
        }
        catch (OperationCanceledException) when (pageCts.IsCancellationRequested)
        {
        }
        finally
        {
            saving = false;
        }
    }

    private async Task SaveProduct()
    {
        var prices = productModel.Prices.Where(p => p.Ban).ToList();

        if (prices.Count == 0)
        {
            error = "Chọn ít nhất một kích thước và nhập giá bán.";
            return;
        }

        if (prices.Any(p => p.GiaBan is null or < 0))
        {
            error = "Nhập giá bán (không âm) cho mọi kích thước đã chọn.";
            return;
        }

        var priceRequests = prices
            .Select(p => new GiaSizeRequest { MaKichThuoc = p.MaKichThuoc, GiaBan = p.GiaBan, DangApDung = true })
            .ToList();
        string? image = string.IsNullOrWhiteSpace(productModel.HinhAnh) ? null : productModel.HinhAnh.Trim();

        await Execute(async () =>
        {
            Mon saved;

            if (editing)
            {
                var original = products.First(p => p.MaTenMon == productModel.MaTenMon);

                saved = await ProductApi.UpdateAsync(productModel.MaTenMon, new MonCapNhatRequest
                {
                    MaDanhMuc = productModel.MaDanhMuc,
                    TenMon = productModel.TenMon.Trim(),
                    // Backend giữ ảnh cũ khi gửi null → gửi chuỗi rỗng để xóa ảnh
                    HinhAnh = image ?? (original.HinhAnh is null ? null : ""),
                    MaKhuyenMai = productModel.MaKhuyenMai, // 0 = gỡ khuyến mãi
                    NgungBan = productModel.NgungBan
                }, pageCts.Token);

                saved = await ProductApi.SetPricesAsync(saved.MaTenMon, priceRequests, pageCts.Token);
            }
            else
            {
                saved = await ProductApi.CreateAsync(new MonTaoRequest
                {
                    MaDanhMuc = productModel.MaDanhMuc,
                    TenMon = productModel.TenMon.Trim(),
                    HinhAnh = image,
                    MaKhuyenMai = productModel.MaKhuyenMai == 0 ? null : productModel.MaKhuyenMai,
                    KichThuoc = priceRequests
                }, pageCts.Token);
            }

            if (saved.ConHang != productModel.ConHang)
            {
                saved = await ProductApi.SetAvailableAsync(saved.MaTenMon, productModel.ConHang, pageCts.Token);
            }

            selected = saved.MaTenMon.ToString();
            return editing ? $"Đã cập nhật món {saved.TenMon}." : $"Đã thêm món {saved.TenMon}. Nhớ thiết lập công thức để món bán được.";
        });

        // Lưu món gồm 2–3 lời gọi API: nếu bước sau lỗi thì bước trước đã ghi → tải lại để danh sách khớp máy chủ
        if (error is not null)
        {
            error += " Một phần thay đổi có thể đã được lưu — kiểm tra lại danh sách.";
            await Load();
        }
    }

    private Task SaveCategory() => Execute(async () =>
    {
        var saved = editing
            ? await CategoryApi.UpdateAsync(nameModel.Id, nameModel.Ten.Trim(), pageCts.Token)
            : await CategoryApi.CreateAsync(nameModel.Ten.Trim(), pageCts.Token);

        selected = saved.MaDanhMuc.ToString();
        return editing ? $"Đã cập nhật danh mục {saved.TenDanhMuc}." : $"Đã thêm danh mục {saved.TenDanhMuc}.";
    });

    private Task SaveTopping() => Execute(async () =>
    {
        var request = new ToppingRequest
        {
            TenTopping = toppingModel.TenTopping.Trim(),
            GiaThem = toppingModel.GiaThem,
            ConHang = toppingModel.ConHang,
            NgungBan = toppingModel.NgungBan
        };

        var saved = editing
            ? await ToppingApi.UpdateAsync(toppingModel.MaTopping, request, pageCts.Token)
            : await ToppingApi.CreateAsync(request, pageCts.Token);

        selected = saved.MaTopping.ToString();
        return editing ? $"Đã cập nhật topping {saved.TenTopping}." : $"Đã thêm topping {saved.TenTopping}.";
    });

    private Task SaveSize() => Execute(async () =>
    {
        var request = new KichThuocRequest
        {
            MaKichThuoc = sizeModel.MaKichThuoc.Trim().ToUpperInvariant(),
            TenKichThuoc = sizeModel.TenKichThuoc.Trim()
        };

        var saved = editing
            ? await CategoryApi.UpdateSizeAsync(request, pageCts.Token)
            : await CategoryApi.CreateSizeAsync(request, pageCts.Token);

        selected = saved.MaKichThuoc;
        return editing ? $"Đã cập nhật kích thước {saved.MaKichThuoc}." : $"Đã thêm kích thước {saved.MaKichThuoc}.";
    });

    // =====================================================================
    // XÓA / TẠM HẾT
    // =====================================================================

    private void AskDelete()
    {
        if (!HasSelection) return;

        Prepare();
        dialog = "delete";
    }

    private Task Delete() => Execute(async () =>
    {
        string name = SelectedName;

        if (SelectedProduct is { } p) await ProductApi.DeleteAsync(p.MaTenMon, pageCts.Token);
        else if (SelectedTopping is { } t) await ToppingApi.DeleteAsync(t.MaTopping, pageCts.Token);
        else if (SelectedCategory is { } c) await CategoryApi.DeleteAsync(c.MaDanhMuc, pageCts.Token);
        else if (SelectedSize is { } s) await CategoryApi.DeleteSizeAsync(s.MaKichThuoc, pageCts.Token);

        bool soft = ProductTab || ToppingTab;
        if (!soft) selected = null;
        return soft ? $"Đã chuyển {name} sang Ngừng bán." : $"Đã xóa {name}.";
    });

    /// <summary>Báo tạm hết / có hàng lại trong ngày (món hoặc topping đang chọn).</summary>
    private async Task ToggleAvailable()
    {
        if (saving) return;

        Prepare();
        saving = true;

        try
        {
            if (SelectedProduct is { } p)
            {
                var saved = await ProductApi.SetAvailableAsync(p.MaTenMon, !p.ConHang, pageCts.Token);
                message = saved.ConHang ? $"{saved.TenMon} đã có hàng trở lại." : $"Đã báo {saved.TenMon} tạm hết.";
            }
            else if (SelectedTopping is { } t)
            {
                var saved = await ToppingApi.SetAvailableAsync(t.MaTopping, !t.ConHang, pageCts.Token);
                message = saved.ConHang ? $"{saved.TenTopping} đã có hàng trở lại." : $"Đã báo {saved.TenTopping} tạm hết.";
            }

            await Load();
        }
        catch (ApiException ex)
        {
            loadError = ex.Message;
        }
        catch (OperationCanceledException) when (pageCts.IsCancellationRequested)
        {
        }
        finally
        {
            saving = false;
        }
    }

    // =====================================================================
    // CÔNG THỨC
    // =====================================================================

    /// <summary>Các món-size có thể đặt công thức (mọi size của món, kể cả đang tắt).</summary>
    private IEnumerable<(int Id, string Label)> RecipeTargets =>
        ToppingTab
            ? toppings.OrderBy(t => t.TenTopping).Select(t => (t.MaTopping, $"{t.TenTopping}"))
            : products.OrderBy(p => p.TenMon)
                .SelectMany(p => p.KichThuoc.Select(s => (s.MaChiTietMon, $"{p.TenMon} – {s.TenKichThuoc}{(s.DangApDung ? "" : " (đã tắt)")}")));

    private async Task OpenRecipe()
    {
        Prepare();
        recipe = [];

        recipeOwner = SelectedTopping?.MaTopping
                      ?? SelectedProduct?.KichThuoc.FirstOrDefault(s => s.DangApDung)?.MaChiTietMon
                      ?? SelectedProduct?.KichThuoc.FirstOrDefault()?.MaChiTietMon
                      ?? 0;

        dialog = "recipe";

        try
        {
            ingredients = (await InventoryApi.GetIngredientsAsync(ct: pageCts.Token))
                .OrderBy(i => i.TenNguyenLieu)
                .ToList();
        }
        catch (ApiException ex)
        {
            error = ex.Message;
            return;
        }

        await LoadRecipe();
    }

    private async Task PickRecipe(ChangeEventArgs e)
    {
        recipeOwner = int.TryParse(e.Value?.ToString(), out int id) ? id : 0;
        error = null;
        await LoadRecipe();
    }

    private async Task LoadRecipe()
    {
        recipe = [];
        if (recipeOwner == 0) return;

        recipeLoading = true;

        try
        {
            var lines = ToppingTab
                ? await ToppingApi.GetRecipeAsync(recipeOwner, pageCts.Token)
                : await ProductApi.GetRecipeAsync(recipeOwner, pageCts.Token);

            recipe = lines.Select(l => new RecipeRow { MaNguyenLieu = l.MaNguyenLieu, LuongSuDung = l.LuongSuDung }).ToList();
        }
        catch (ApiException ex)
        {
            error = ex.Message;
        }
        catch (OperationCanceledException) when (pageCts.IsCancellationRequested)
        {
        }
        finally
        {
            recipeLoading = false;
        }
    }

    private async Task SaveRecipe()
    {
        if (saving) return;

        if (recipeOwner == 0)
        {
            error = "Chọn món / topping trước khi lưu công thức.";
            return;
        }

        var lines = recipe.Where(r => r.MaNguyenLieu > 0).ToList();

        if (recipe.Any(r => r.MaNguyenLieu == 0))
        {
            error = "Chọn nguyên liệu cho mọi dòng (hoặc xóa dòng trống).";
            return;
        }

        if (lines.Any(r => r.LuongSuDung is null or < 0.01m))
        {
            error = "Định lượng phải lớn hơn 0.";
            return;
        }

        if (lines.Select(r => r.MaNguyenLieu).Distinct().Count() != lines.Count)
        {
            error = "Mỗi nguyên liệu chỉ xuất hiện một lần trong công thức.";
            return;
        }

        saving = true;
        error = null;

        try
        {
            var request = lines
                .Select(r => new CongThucDongRequest { MaNguyenLieu = r.MaNguyenLieu, LuongSuDung = r.LuongSuDung })
                .ToList();

            if (ToppingTab) await ToppingApi.SaveRecipeAsync(recipeOwner, request, pageCts.Token);
            else await ProductApi.SaveRecipeAsync(recipeOwner, request, pageCts.Token);

            message = "Đã lưu công thức. Đơn mới sẽ trừ kho theo định mức này.";
            dialog = "";
            await Load(); // tình trạng "đủ nguyên liệu" của món có thể đổi
        }
        catch (ApiException ex)
        {
            error = ex.Message;
        }
        catch (OperationCanceledException) when (pageCts.IsCancellationRequested)
        {
        }
        finally
        {
            saving = false;
        }
    }

    private string UnitOf(int maNguyenLieu) =>
        ingredients.FirstOrDefault(i => i.MaNguyenLieu == maNguyenLieu)?.DonViTinh ?? "";

    public void Dispose()
    {
        pageCts.Cancel();
        pageCts.Dispose();
    }
}
