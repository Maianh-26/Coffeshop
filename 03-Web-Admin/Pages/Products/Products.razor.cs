using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CoffeeShop.Admin.Models;
using CoffeeShop.Admin.Services;
using CoffeeShop.Admin.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CoffeeShop.Admin.Pages.Products;

/// <summary>
/// Quản lý thực đơn: món (giá gốc theo size), danh mục, topping và công thức (định mức nguyên liệu).
/// Danh sách size (S/M/L) có sẵn trong CSDL, chỉ dùng để hiện ô giá trong form món — không quản lý ở đây.
/// Khuyến mãi gắn cho món được quản lý ở trang Khuyến mãi — trang này chỉ hiện và lưu giá gốc.
/// API: api/v1/mon, danh-muc, topping, kich-thuoc (chỉ đọc), chi-tiet-mon/{id}/cong-thuc, topping/{id}/cong-thuc.
/// </summary>
public partial class Products : IDisposable
{
    [Inject] private ProductService ProductApi { get; set; } = default!;
    [Inject] private CategoryService CategoryApi { get; set; } = default!;
    [Inject] private ToppingService ToppingApi { get; set; } = default!;
    [Inject] private InventoryService InventoryApi { get; set; } = default!;

    private readonly CancellationTokenSource pageCts = new();

    private List<Mon> products = [];
    private List<DanhMuc> categories = [];
    private List<Topping> toppings = [];
    private List<KichThuoc> sizes = [];
    private List<NguyenLieu> ingredients = [];

    /// <summary>"products" | "categories" | "toppings"</summary>
    private string activeTab = "products";
    private bool ProductTab => activeTab == "products";
    private bool CategoryTab => activeTab == "categories";
    private bool ToppingTab => activeTab == "toppings";

    private bool loading;
    private bool refreshing;     // đang tải lại do bấm nút Làm mới (biểu tượng xoay)
    private bool saving;
    private bool editing;
    private string? loadError;
    private string? error;
    private string? message;

    private int page = 1;
    private const int PageSize = 10;   // giống trang Kho, Khuyến mãi, Tài khoản

    private string search = "";
    private string categoryFilter = "";
    private string statusFilter = "";
    private bool filtersOpen;    // màn hình hẹp: khung bộ lọc đang mở (giống trang Đơn hàng / Kho)

    /// <summary>Khóa dòng đang chọn: mã món / danh mục / topping.</summary>
    private string? selected;

    /// <summary>"" | "product" | "category" | "topping" | "delete" | "recipe"</summary>
    private string dialog = "";

    private ProductForm productModel = new();
    private NameForm nameModel = new();
    private ToppingForm toppingModel = new();

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

        /// <summary>Chỉ chọn khi sửa: Đang bán / Tạm hết / Ngừng bán. Món mới luôn Đang bán.</summary>
        public string TrangThai { get; set; } = TrangThaiMon.DangBan;

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

        /// <summary>Chỉ chọn khi sửa: Đang bán / Tạm hết / Ngừng bán. Topping mới luôn Đang bán.</summary>
        public string TrangThai { get; set; } = TrangThaiMon.DangBan;
    }

    /// <summary>Cờ của backend → lựa chọn trong form (món đang "Hết nguyên liệu" hiện là Đang bán).</summary>
    private static string StatusOf(bool conHang, bool ngungBan) =>
        ngungBan ? TrangThaiMon.NgungBan : !conHang ? TrangThaiMon.TamHet : TrangThaiMon.DangBan;

    public class RecipeRow
    {
        public int MaNguyenLieu { get; set; }
        public decimal? LuongSuDung { get; set; }
    }

    // =====================================================================
    // DANH SÁCH & LỌC
    // =====================================================================

    /// <summary>
    /// Chuỗi tìm đã bỏ dấu, chữ thường (xem <see cref="SearchText"/>): gõ "ba" ra "Bạc xỉu", "ca phe" ra "Cà phê".
    /// Trước đây so từng ký tự nên "ạ"/"à" khác "a" → gõ không dấu không tìm ra món có dấu.
    /// </summary>
    private string Term => SearchText.Fold(search);

    /// <summary>Khớp tên (không phân biệt dấu) hoặc mã (chuỗi số chứa chuỗi gõ).</summary>
    private static bool Matches(int id, string name, string term) =>
        term.Length == 0
        || id.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.Ordinal)
        || SearchText.Contains(name, term);

    private IEnumerable<Mon> ProductMatches
    {
        get
        {
            string term = Term;
            return sort.Apply(products.Where(p =>
                    Matches(p.MaTenMon, p.TenMon, term)
                    && (categoryFilter == "" || p.MaDanhMuc.ToString(CultureInfo.InvariantCulture) == categoryFilter)
                    && (statusFilter == "" || TrangThaiMon.Of(p) == statusFilter)),
                (SortId, p => p.MaTenMon), (SortPrice, p => LowestPrice(p)));
        }
    }

    private IEnumerable<DanhMuc> CategoryMatches
    {
        get
        {
            string term = Term;
            return sort.Apply(categories.Where(c => Matches(c.MaDanhMuc, c.TenDanhMuc, term)), (SortId, c => c.MaDanhMuc));
        }
    }

    private IEnumerable<Topping> ToppingMatches
    {
        get
        {
            string term = Term;
            return sort.Apply(toppings.Where(t =>
                    Matches(t.MaTopping, t.TenTopping, term)
                    && (statusFilter == "" || TrangThaiMon.Of(t) == statusFilter)),
                (SortId, t => t.MaTopping), (SortPrice, t => t.GiaThem));
        }
    }

    // ---------------------------------------------------------------- Bộ lọc (đồng bộ trang Đơn hàng / Kho)

    /// <summary>Tab có ô lọc ngoài ô tìm kiếm (Món: danh mục + trạng thái; Topping: trạng thái).</summary>
    private bool HasFilters => ProductTab || ToppingTab;

    private int ActiveFilterCount =>
        (ProductTab && categoryFilter != "" ? 1 : 0) + (HasFilters && statusFilter != "" ? 1 : 0);

    // Đang lọc / tìm / sắp xếp khác mặc định → nút Làm mới có chấm báo (bấm sẽ đưa về mặc định)
    private bool FiltersActive => Term != "" || ActiveFilterCount > 0 || sort.Active;

    private string TabNoun => ToppingTab ? "topping" : CategoryTab ? "danh mục" : "món";

    private string RefreshTitle => FiltersActive ? $"Làm mới và bỏ lọc, xem toàn bộ {TabNoun}" : $"Làm mới danh sách {TabNoun}";

    private string SearchPlaceholder => $"Tìm theo tên hoặc mã {TabNoun}…";

    private string EmptyText =>
        loading ? "Đang tải dữ liệu…"
        : FiltersActive ? $"Không có {TabNoun} khớp bộ lọc."
        : $"Chưa có {TabNoun}.";

    // ---------------------------------------------------------------- Sắp xếp theo cột (dùng chung: Utilities/ColumnSort)

    private const string SortId = "id";
    private const string SortPrice = "price";

    private readonly ColumnSort sort = new();

    /// <summary>Giá gốc thấp nhất trong các size đang bán (món chưa có giá xếp như 0 đ).</summary>
    private static decimal LowestPrice(Mon m) =>
        m.KichThuoc.Where(s => s.DangApDung).Select(s => s.GiaBan).DefaultIfEmpty(0).Min();

    /// <summary>Bấm tiêu đề cột (Mã, Giá). Như Filter(): bỏ chọn dòng vì dòng đó có thể sang trang khác.</summary>
    private void ToggleSort(string key)
    {
        sort.Toggle(key);
        Filter();
    }

    private int Count =>
        ToppingTab ? ToppingMatches.Count()
        : CategoryTab ? CategoryMatches.Count()
        : ProductMatches.Count();

    private int Pages => Math.Max(1, (Count + PageSize - 1) / PageSize);

    private IEnumerable<T> Paged<T>(IEnumerable<T> items) => items.Skip((page - 1) * PageSize).Take(PageSize);

    // Chỉ tính dòng đang thấy (như Kho / Khuyến mãi / Tài khoản): dòng vừa bị lọc mất hoặc sang trang khác
    // (ví dụ sửa sang "Ngừng bán" khi đang lọc "Đang bán") không còn được chọn ngầm cho Sửa / Xóa / Công thức
    private Mon? SelectedProduct => ProductTab ? Paged(ProductMatches).FirstOrDefault(p => p.MaTenMon.ToString() == selected) : null;
    private DanhMuc? SelectedCategory => CategoryTab ? Paged(CategoryMatches).FirstOrDefault(c => c.MaDanhMuc.ToString() == selected) : null;
    private Topping? SelectedTopping => ToppingTab ? Paged(ToppingMatches).FirstOrDefault(t => t.MaTopping.ToString() == selected) : null;

    private bool HasSelection => SelectedProduct is not null || SelectedCategory is not null || SelectedTopping is not null;

    /// <summary>
    /// Món / topping đã ngừng bán thì không xóa nữa (backend chỉ xóa mềm: chuyển sang Ngừng bán để giữ lịch sử đơn).
    /// Bán lại: Sửa → Trạng thái "Đang bán".
    /// </summary>
    private string? DeleteBlockedReason =>
        SelectedProduct is { NgungBan: true } || SelectedTopping is { NgungBan: true }
            ? "Đã ngừng bán — chọn Sửa để bán lại"
            // Backend không cho xóa danh mục còn món (kể cả món ngừng bán) → chặn trước, khỏi mở hộp xác nhận rồi mới báo lỗi
            : SelectedCategory is { } c && products.Count(p => p.MaDanhMuc == c.MaDanhMuc) is > 0 and var n
                ? $"Danh mục còn {n} món — chuyển món sang danh mục khác trước"
                : null;

    private string SelectedName =>
        SelectedProduct?.TenMon ?? SelectedCategory?.TenDanhMuc ?? SelectedTopping?.TenTopping ?? "";

    private string DialogTitle => dialog switch
    {
        "product" => editing ? "Sửa món" : "Thêm món mới",
        "category" => editing ? "Sửa danh mục" : "Thêm danh mục",
        "topping" => editing ? "Sửa topping" : "Thêm topping",
        "delete" => ProductTab ? "Xóa món" : ToppingTab ? "Xóa topping" : "Xóa danh mục",
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

            products = await productTask;
            categories = await categoryTask;
            toppings = await toppingTask;
            sizes = await sizeTask;

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

    /// <summary>Đưa bộ lọc, tìm kiếm và sắp xếp về mặc định, về trang 1.</summary>
    private void ResetFilters()
    {
        search = "";
        categoryFilter = "";
        statusFilter = "";
        sort.Reset();
        Filter();
    }

    private void SwitchTab(string tab)
    {
        if (activeTab == tab) return;

        activeTab = tab;
        message = null;
        filtersOpen = false;
        ResetFilters();
    }

    /// <summary>Nút Làm mới: bỏ mọi bộ lọc / tìm kiếm / sắp xếp của tab đang xem, về trang 1 và tải lại dữ liệu mới nhất.</summary>
    private async Task RefreshAll()
    {
        ResetFilters();

        // Đang có lượt tải khác (mở trang, sau khi lưu) → chỉ bỏ lọc, không gọi API thêm lần nữa
        if (refreshing || loading) return;

        message = null;
        refreshing = true;

        try
        {
            await Load();
        }
        finally
        {
            refreshing = false;
        }
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
            default:
                productModel = new ProductForm
                {
                    // Không chọn sẵn danh mục: người dùng phải tự chọn (tránh lưu nhầm danh mục đầu danh sách)
                    MaDanhMuc = 0,
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
                TrangThai = StatusOf(p.ConHang, p.NgungBan),
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
                MaTopping = t.MaTopping, TenTopping = t.TenTopping, GiaThem = t.GiaThem, TrangThai = StatusOf(t.ConHang, t.NgungBan)
            };
            dialog = "topping";
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
                    // MaKhuyenMai = null → giữ nguyên khuyến mãi đang gắn (gắn / gỡ ở trang Khuyến mãi)
                    NgungBan = productModel.TrangThai == TrangThaiMon.NgungBan
                }, pageCts.Token);

                saved = await ProductApi.SetPricesAsync(saved.MaTenMon, priceRequests, pageCts.Token);

                // Đang bán ↔ Tạm hết đổi cờ "còn hàng" qua API riêng; chọn Ngừng bán thì giữ nguyên cờ này
                bool? conHang = productModel.TrangThai switch
                {
                    TrangThaiMon.DangBan => true,
                    TrangThaiMon.TamHet => false,
                    _ => null
                };

                if (conHang is { } value && saved.ConHang != value)
                {
                    saved = await ProductApi.SetAvailableAsync(saved.MaTenMon, value, pageCts.Token);
                }
            }
            else
            {
                // Món mới luôn ở trạng thái Đang bán (backend mặc định còn hàng, chưa ngừng bán)
                saved = await ProductApi.CreateAsync(new MonTaoRequest
                {
                    MaDanhMuc = productModel.MaDanhMuc,
                    TenMon = productModel.TenMon.Trim(),
                    HinhAnh = image,
                    KichThuoc = priceRequests
                }, pageCts.Token);
            }

            selected = saved.MaTenMon.ToString();
            return editing ? $"Đã cập nhật món {saved.TenMon}." : $"Đã thêm món {saved.TenMon}.";
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
        // Topping mới luôn Đang bán. Khi sửa: Ngừng bán giữ nguyên cờ "còn hàng" đang có.
        var original = editing ? toppings.FirstOrDefault(t => t.MaTopping == toppingModel.MaTopping) : null;
        string status = editing ? toppingModel.TrangThai : TrangThaiMon.DangBan;

        var request = new ToppingRequest
        {
            TenTopping = toppingModel.TenTopping.Trim(),
            GiaThem = toppingModel.GiaThem,
            ConHang = status switch
            {
                TrangThaiMon.DangBan => true,
                TrangThaiMon.TamHet => false,
                _ => original?.ConHang ?? true
            },
            NgungBan = status == TrangThaiMon.NgungBan
        };

        var saved = editing
            ? await ToppingApi.UpdateAsync(toppingModel.MaTopping, request, pageCts.Token)
            : await ToppingApi.CreateAsync(request, pageCts.Token);

        selected = saved.MaTopping.ToString();
        return editing ? $"Đã cập nhật topping {saved.TenTopping}." : $"Đã thêm topping {saved.TenTopping}.";
    });

    // =====================================================================
    // XÓA
    // =====================================================================

    private void AskDelete()
    {
        if (!HasSelection || DeleteBlockedReason is not null) return;

        Prepare();
        dialog = "delete";
    }

    private Task Delete() => Execute(async () =>
    {
        string name = SelectedName;

        if (SelectedProduct is { } p) await ProductApi.DeleteAsync(p.MaTenMon, pageCts.Token);
        else if (SelectedTopping is { } t) await ToppingApi.DeleteAsync(t.MaTopping, pageCts.Token);
        else if (SelectedCategory is { } c) await CategoryApi.DeleteAsync(c.MaDanhMuc, pageCts.Token);

        // Món / topping đã có trong đơn hàng → backend giữ lại ở trạng thái Ngừng bán (vẫn hiện trong bảng)
        bool soft = ProductTab || ToppingTab;
        if (!soft) selected = null;
        // Món / topping: backend chuyển sang Ngừng bán (dòng vẫn hiện, nhãn đã đủ rõ)
        return $"Đã xóa {TabNoun} {name}.";
    });

    // =====================================================================
    // CÔNG THỨC
    // =====================================================================

    /// <summary>Các món-size có thể đặt công thức (mọi size của món, kể cả đang tắt).</summary>
    private IEnumerable<(int Id, string Label)> RecipeTargets =>
        ToppingTab
            ? toppings.OrderBy(t => t.TenTopping).Select(t => (t.MaTopping, $"{t.TenTopping}"))
            : products.OrderBy(p => p.TenMon)
                .SelectMany(p => p.KichThuoc.Select(s => (s.MaChiTietMon, $"{p.TenMon} – {s.TenKichThuoc}{(s.DangApDung ? "" : " (đã tắt)")}")));

    /// <summary>Tên các nguyên liệu đã ngừng sử dụng đang nằm trong công thức đang mở.</summary>
    private List<string> StoppedInRecipe =>
        recipe.Select(r => ingredients.FirstOrDefault(i => i.MaNguyenLieu == r.MaNguyenLieu))
            .Where(i => i is { DangSuDung: false })
            .Select(i => i!.TenNguyenLieu)
            .Distinct()
            .ToList();

    private async Task OpenRecipe()
    {
        Prepare();
        recipe = [];

        recipeOwner = SelectedTopping?.MaTopping
                      ?? SelectedProduct?.KichThuoc.FirstOrDefault(s => s.DangApDung)?.MaChiTietMon
                      ?? SelectedProduct?.KichThuoc.FirstOrDefault()?.MaChiTietMon
                      ?? 0;

        dialog = "recipe";
        // Bật cờ đang tải ngay: trong lúc chờ danh sách nguyên liệu không được hiện nhầm "chưa có định mức"
        recipeLoading = true;

        try
        {
            ingredients = (await InventoryApi.GetIngredientsAsync(ct: pageCts.Token))
                .OrderBy(i => i.TenNguyenLieu)
                .ToList();
        }
        catch (ApiException ex)
        {
            error = ex.Message;
            recipeLoading = false;
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

            message = "Đã lưu công thức.";
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
