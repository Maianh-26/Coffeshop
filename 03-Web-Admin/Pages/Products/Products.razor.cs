using System.Globalization;
using CoffeeShop.Admin.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace CoffeeShop.Admin.Pages.Products;

public partial class Products
{
    private List<Product> products = [];
    private List<Category> categories = [];
    private List<Ingredient> ingredients = [];
    private List<Topping> toppings = [];

    private List<RecipeLine> recipe = [];

    private Product model = new();
    private Category categoryModel = new();
    private Topping toppingModel = new();

    private string activeTab = "products"; // "products", "categories", "toppings"
    private bool categoryTab => activeTab == "categories";
    private bool toppingTab => activeTab == "toppings";

    private bool editing;
    private bool saving;
    private bool reading;

    private int page = 1;
    private const int Size = 8;

    private string search = "";
    private string category = "";
    private string status = "";
    private string toppingType = "";
    private string toppingStatus = "";

    private string appliedSearch = "";
    private string appliedCategory = "";
    private string appliedStatus = "";
    private string appliedToppingType = "";
    private string appliedToppingStatus = "";

    private string dialog = "";
    private string target = "";
    private string? selected;
    private string? error;
    private string? message;
    private string? imageError;

    private IEnumerable<Product> Matches =>
        products.Where(product =>
            (
                product.Code.Contains(appliedSearch, StringComparison.OrdinalIgnoreCase) ||
                product.Name.Contains(appliedSearch, StringComparison.OrdinalIgnoreCase)
            ) &&
            (appliedCategory == "" || product.CategoryId == appliedCategory) &&
            (appliedStatus == "" || product.Status == appliedStatus)
        );

    private IEnumerable<Category> CategoryMatches =>
        categories.Where(item =>
            item.Code.Contains(appliedSearch, StringComparison.OrdinalIgnoreCase) ||
            item.Name.Contains(appliedSearch, StringComparison.OrdinalIgnoreCase)
        );

    private IEnumerable<Topping> ToppingMatches =>
        toppings.Where(item =>
            (
                item.Code.Contains(appliedSearch, StringComparison.OrdinalIgnoreCase) ||
                item.Name.Contains(appliedSearch, StringComparison.OrdinalIgnoreCase)
            ) &&
            (appliedToppingType == "" || item.Type == appliedToppingType) &&
            (appliedToppingStatus == "" || item.Status == appliedToppingStatus)
        );

    private IEnumerable<Product> VisibleProducts =>
        Matches.Skip((page - 1) * Size).Take(Size);

    private IEnumerable<Category> VisibleCategories =>
        CategoryMatches.Skip((page - 1) * Size).Take(Size);

    private IEnumerable<Topping> VisibleToppings =>
        ToppingMatches.Skip((page - 1) * Size).Take(Size);

    private int Count =>
        toppingTab ? ToppingMatches.Count() :
        categoryTab ? CategoryMatches.Count() :
        Matches.Count();

    private int Pages =>
        Math.Max(1, (Count + Size - 1) / Size);

    private string DialogTitle => dialog switch
    {
        "product" => editing ? "Sửa món" : "Thêm món mới",
        "category" => editing ? "Sửa danh mục" : "Thêm danh mục",
        "topping" => editing ? "Sửa Topping / Size" : "Thêm Topping / Size mới",
        "delete" => toppingTab ? "Xóa Topping / Size" : (categoryTab ? "Xóa danh mục" : "Xóa món"),
        "recipe" => toppingTab ? "Định mức tiêu hao Topping / Size" : "Định mức nguyên liệu món",
        _ => "Thông báo"
    };

    private static string Money(decimal value)
    {
        return value.ToString("N0", new CultureInfo("vi-VN")) + " đ";
    }

    private static string Color(string value)
    {
        return value switch
        {
            "Đang bán" or "Đang phục vụ" => "green",
            "Tạm hết" => "yellow",
            _ => "red"
        };
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            categories = new()
            {
                new() { Code = "DM0001", Name = "Cà phê", SortOrder = 1, Visible = true },
                new() { Code = "DM0002", Name = "Trà sữa", SortOrder = 2, Visible = true },
                new() { Code = "DM0003", Name = "Trà trái cây", SortOrder = 3, Visible = true },
                new() { Code = "DM0004", Name = "Bánh & Ăn kèm", SortOrder = 4, Visible = true }
            };

            ingredients = new()
            {
                new() { Code = "NL0001", Name = "Cà phê hạt Arabica", Unit = "g" },
                new() { Code = "NL0002", Name = "Sữa đặc Ngôi Sao", Unit = "ml" },
                new() { Code = "NL0003", Name = "Sữa tươi thanh trùng", Unit = "ml" },
                new() { Code = "NL0004", Name = "Trân châu đen thô", Unit = "g" },
                new() { Code = "NL0005", Name = "Bột phô mai Macchiato", Unit = "g" },
                new() { Code = "NL0006", Name = "Bột Pudding trứng", Unit = "g" },
                new() { Code = "NL0007", Name = "Ly giấy 500ml", Unit = "cái" },
                new() { Code = "NL0008", Name = "Ly giấy 700ml", Unit = "cái" }
            };

            toppings = new()
            {
                new()
                {
                    Code = "SZ-M",
                    Name = "Size M (Vừa 500ml)",
                    Type = "Size",
                    ExtraPrice = 0,
                    Status = "Đang phục vụ",
                    Description = "Kích cỡ chuẩn mặc định",
                    Recipe = new List<RecipeLine> { new() { IngredientId = "NL0007", Quantity = 1 } }
                },
                new()
                {
                    Code = "SZ-L",
                    Name = "Size L (Lớn 700ml)",
                    Type = "Size",
                    ExtraPrice = 6000,
                    Status = "Đang phục vụ",
                    Description = "Kích cỡ lớn (+6.000 đ)",
                    Recipe = new List<RecipeLine> { new() { IngredientId = "NL0008", Quantity = 1 } }
                },
                new()
                {
                    Code = "SZ-XL",
                    Name = "Size XL (Khổng lồ 1000ml)",
                    Type = "Size",
                    ExtraPrice = 12000,
                    Status = "Đang phục vụ",
                    Description = "Kích cỡ siêu to (+12.000 đ)"
                },
                new()
                {
                    Code = "TP-TC",
                    Name = "Trân châu đen",
                    Type = "Topping",
                    ExtraPrice = 8000,
                    Status = "Đang phục vụ",
                    Description = "Trân châu đường đen dẻo bùi",
                    Recipe = new List<RecipeLine> { new() { IngredientId = "NL0004", Quantity = 35 } }
                },
                new()
                {
                    Code = "TP-TCTRANG",
                    Name = "Trân châu trắng",
                    Type = "Topping",
                    ExtraPrice = 8000,
                    Status = "Đang phục vụ",
                    Description = "Trân châu thủy tinh giòn sần sật"
                },
                new()
                {
                    Code = "TP-KEM",
                    Name = "Kem Cheese Macchiato",
                    Type = "Topping",
                    ExtraPrice = 10000,
                    Status = "Đang phục vụ",
                    Description = "Lớp kem béo ngậy vị mặn nhẹ",
                    Recipe = new List<RecipeLine> { new() { IngredientId = "NL0005", Quantity = 25 } }
                },
                new()
                {
                    Code = "TP-FLAN",
                    Name = "Pudding trứng",
                    Type = "Topping",
                    ExtraPrice = 10000,
                    Status = "Đang phục vụ",
                    Description = "Pudding mềm mịn tan trong miệng",
                    Recipe = new List<RecipeLine> { new() { IngredientId = "NL0006", Quantity = 30 } }
                },
                new()
                {
                    Code = "TP-THACH",
                    Name = "Thạch nha đam",
                    Type = "Topping",
                    ExtraPrice = 6000,
                    Status = "Đang phục vụ",
                    Description = "Nha đam giòn ngọt thanh nhiệt"
                }
            };

            products = new()
            {
                new()
                {
                    Code = "SP0001",
                    Name = "Cà phê sữa đá",
                    CategoryId = "DM0001",
                    Price = 29000,
                    Status = "Đang bán",
                    Description = "Cà phê pha phin truyền thống thơm đượm",
                    Recipe = new List<RecipeLine>
                    {
                        new() { IngredientId = "NL0001", Quantity = 25 },
                        new() { IngredientId = "NL0002", Quantity = 40 }
                    }
                },
                new()
                {
                    Code = "SP0002",
                    Name = "Bạc xỉu",
                    CategoryId = "DM0001",
                    Price = 32000,
                    Status = "Đang bán",
                    Description = "Nhiều sữa ít cà phê, béo ngậy ngọt ngào",
                    Recipe = new List<RecipeLine>
                    {
                        new() { IngredientId = "NL0001", Quantity = 15 },
                        new() { IngredientId = "NL0002", Quantity = 30 },
                        new() { IngredientId = "NL0003", Quantity = 80 }
                    }
                },
                new()
                {
                    Code = "SP0003",
                    Name = "Trà sữa Oolong nướng",
                    CategoryId = "DM0002",
                    Price = 42000,
                    Status = "Đang bán",
                    Description = "Vị trà oolong đậm đà hòa quyện khói nướng đặc trưng"
                },
                new()
                {
                    Code = "SP0004",
                    Name = "Trà đào cam sả",
                    CategoryId = "DM0003",
                    Price = 39000,
                    Status = "Đang bán",
                    Description = "Thanh mát giải nhiệt cùng đào miếng giòn ngọt"
                },
                new()
                {
                    Code = "SP0005",
                    Name = "Trà sữa Matcha Nhật",
                    CategoryId = "DM0002",
                    Price = 45000,
                    Status = "Đang bán",
                    Description = "Matcha Uji nguyên chất thơm thanh tao"
                }
            };

            page = Math.Clamp(page, 1, Pages);
        }
        catch (Exception exception)
        {
            Notice(exception.Message);
        }
        await Task.CompletedTask;
    }

    private async Task Reload()
    {
        page = Math.Clamp(page, 1, Pages);
        await Task.CompletedTask;
    }

    private void SearchKey(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            Filter();
        }
    }

    private void SelectKey(KeyboardEventArgs e, string code)
    {
        if (e.Key is "Enter" or " ")
        {
            selected = code;
        }
    }

    private void Filter()
    {
        appliedSearch = search.Trim();
        appliedCategory = category;
        appliedStatus = status;
        appliedToppingType = toppingType;
        appliedToppingStatus = toppingStatus;

        selected = null;
        page = 1;
        message = null;
    }

    private void Reset()
    {
        search = "";
        category = "";
        status = "";
        toppingType = "";
        toppingStatus = "";

        Filter();
    }

    private void SwitchTab(string tab)
    {
        activeTab = tab;
        Reset();
    }

    private void SwitchTab(bool isCategory)
    {
        activeTab = isCategory ? "categories" : "products";
        Reset();
    }

    private void Move(int delta)
    {
        int nextPage = page + delta;

        if (nextPage < 1 || nextPage > Pages)
        {
            Notice("Không có trang dữ liệu khác.");
            return;
        }

        page = nextPage;
        selected = null;
    }

    private void Notice(string text)
    {
        error = text;
        dialog = "notice";
    }

    private void Prepare()
    {
        error = null;
        imageError = null;
        message = null;
        target = selected ?? "";
    }

    private void Add()
    {
        Prepare();
        editing = false;
        target = "";

        if (toppingTab)
        {
            toppingModel = new Topping { Type = "Topping", Status = "Đang phục vụ", ExtraPrice = 0 };
            dialog = "topping";
        }
        else if (categoryTab)
        {
            categoryModel = new Category();
            dialog = "category";
        }
        else
        {
            model = new Product();
            dialog = "product";
        }
    }

    private void Edit()
    {
        Prepare();
        editing = true;
        LoadEditTarget();

        if (toppingTab)
        {
            dialog = "topping";
        }
        else if (categoryTab)
        {
            dialog = "category";
        }
        else
        {
            dialog = "product";
        }
    }

    private void LoadEditTarget()
    {
        if (toppingTab)
        {
            var top = toppings.FirstOrDefault(t => t.Code == target);
            toppingModel = top != null
                ? new Topping
                {
                    Code = top.Code,
                    Name = top.Name,
                    Type = top.Type,
                    ExtraPrice = top.ExtraPrice,
                    Status = top.Status,
                    Description = top.Description,
                    Recipe = top.Recipe != null ? new List<RecipeLine>(top.Recipe) : []
                }
                : new Topping { Type = "Topping" };
        }
        else if (categoryTab)
        {
            categoryModel = new Category
            {
                Code = categories.FirstOrDefault(item => item.Code == target)?.Code ?? "",
                Name = categories.FirstOrDefault(item => item.Code == target)?.Name ?? "",
                SortOrder = categories.FirstOrDefault(item => item.Code == target)?.SortOrder ?? 1,
                Visible = categories.FirstOrDefault(item => item.Code == target)?.Visible ?? true
            };
        }
        else
        {
            model = new Product
            {
                Code = products.FirstOrDefault(product => product.Code == target)?.Code ?? "",
                Name = products.FirstOrDefault(product => product.Code == target)?.Name ?? "",
                CategoryId = products.FirstOrDefault(product => product.Code == target)?.CategoryId ?? "",
                Price = products.FirstOrDefault(product => product.Code == target)?.Price,
                Status = products.FirstOrDefault(product => product.Code == target)?.Status ?? "Ngừng bán",
                Description = products.FirstOrDefault(product => product.Code == target)?.Description,
                ImageUrl = products.FirstOrDefault(product => product.Code == target)?.ImageUrl,
                Recipe = products.FirstOrDefault(product => product.Code == target)?.Recipe ?? []
            };
        }
    }

    private void PickEdit(ChangeEventArgs e)
    {
        if (reading || saving)
        {
            return;
        }

        target = e.Value?.ToString() ?? "";
        LoadEditTarget();

        error = null;
        imageError = null;
    }

    private void AskDelete()
    {
        Prepare();
        dialog = "delete";
    }

    private async Task OpenRecipe()
    {
        Prepare();

        if (toppingTab)
        {
            recipe = target == ""
                ? []
                : (toppings.FirstOrDefault(t => t.Code == target)?.Recipe ?? []).ToList();
        }
        else
        {
            recipe = target == ""
                ? []
                : (products.FirstOrDefault(p => p.Code == target)?.Recipe ?? []).ToList();
        }

        dialog = "recipe";
        await Task.CompletedTask;
    }

    private async Task PickRecipe(ChangeEventArgs e)
    {
        target = e.Value?.ToString() ?? "";

        if (toppingTab)
        {
            recipe = target == ""
                ? []
                : (toppings.FirstOrDefault(t => t.Code == target)?.Recipe ?? []).ToList();
        }
        else
        {
            recipe = target == ""
                ? []
                : (products.FirstOrDefault(p => p.Code == target)?.Recipe ?? []).ToList();
        }

        error = null;
        await Task.CompletedTask;
    }

    private async Task Execute(Func<Task> action, string success)
    {
        if (saving || reading)
        {
            return;
        }

        saving = true;
        error = null;

        try
        {
            await action();
            await Reload();

            dialog = "";
            selected = null;
            message = success;
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }
        finally
        {
            saving = false;
        }
    }

    private Task SaveProduct()
    {
        if (editing && model.Code == "")
        {
            error = "Chọn món cần sửa.";
            return Task.CompletedTask;
        }

        if (imageError is not null)
        {
            return Task.CompletedTask;
        }

        return Execute(
            () =>
            {
                if (editing)
                {
                    var existing = products.FirstOrDefault(p => p.Code == target);
                    if (existing != null)
                    {
                        existing.Name = model.Name;
                        existing.CategoryId = model.CategoryId;
                        existing.Price = model.Price;
                        existing.Status = model.Status;
                        existing.Description = model.Description;
                        existing.ImageUrl = model.ImageUrl;
                    }
                }
                else
                {
                    model.Code = $"SP{products.Count + 1:D4}";
                    products.Add(new Product
                    {
                        Code = model.Code,
                        Name = model.Name,
                        CategoryId = model.CategoryId,
                        Price = model.Price,
                        Status = model.Status,
                        Description = model.Description,
                        ImageUrl = model.ImageUrl,
                        Recipe = model.Recipe
                    });
                }
                return Task.CompletedTask;
            },
            editing ? "Đã cập nhật món." : "Đã thêm món mới."
        );
    }

    private Task SaveCategory()
    {
        if (editing && categoryModel.Code == "")
        {
            error = "Chọn danh mục cần sửa.";
            return Task.CompletedTask;
        }

        return Execute(
            () =>
            {
                if (editing)
                {
                    var existing = categories.FirstOrDefault(c => c.Code == target);
                    if (existing != null)
                    {
                        existing.Name = categoryModel.Name;
                        existing.SortOrder = categoryModel.SortOrder;
                        existing.Visible = categoryModel.Visible;
                    }
                }
                else
                {
                    categoryModel.Code = $"DM{categories.Count + 1:D4}";
                    categories.Add(new Category
                    {
                        Code = categoryModel.Code,
                        Name = categoryModel.Name,
                        SortOrder = categoryModel.SortOrder,
                        Visible = categoryModel.Visible
                    });
                }
                return Task.CompletedTask;
            },
            editing ? "Đã cập nhật danh mục." : "Đã thêm danh mục mới."
        );
    }

    private Task SaveTopping()
    {
        if (editing && toppingModel.Code == "")
        {
            error = "Chọn Topping / Size cần sửa.";
            return Task.CompletedTask;
        }

        return Execute(
            () =>
            {
                if (editing)
                {
                    var existing = toppings.FirstOrDefault(t => t.Code == target);
                    if (existing != null)
                    {
                        existing.Name = toppingModel.Name;
                        existing.Type = toppingModel.Type;
                        existing.ExtraPrice = toppingModel.ExtraPrice;
                        existing.Status = toppingModel.Status;
                        existing.Description = toppingModel.Description;
                    }
                }
                else
                {
                    string prefix = toppingModel.Type == "Size" ? "SZ" : "TP";
                    int countOfType = toppings.Count(t => t.Type == toppingModel.Type) + 1;
                    toppingModel.Code = $"{prefix}-{countOfType:D2}";
                    toppings.Add(new Topping
                    {
                        Code = toppingModel.Code,
                        Name = toppingModel.Name,
                        Type = toppingModel.Type,
                        ExtraPrice = toppingModel.ExtraPrice,
                        Status = toppingModel.Status,
                        Description = toppingModel.Description,
                        Recipe = toppingModel.Recipe
                    });
                }
                return Task.CompletedTask;
            },
            editing ? "Đã cập nhật Topping / Size." : "Đã thêm Topping / Size mới."
        );
    }

    private Task SaveRecipe()
    {
        if (target == "")
        {
            error = "Chọn đối tượng trước khi lưu công thức.";
            return Task.CompletedTask;
        }

        if (toppingTab)
        {
            var top = toppings.FirstOrDefault(t => t.Code == target);
            if (top != null)
            {
                top.Recipe = new List<RecipeLine>(recipe);
            }
        }
        else
        {
            var product = products.FirstOrDefault(p => p.Code == target);
            if (product != null)
            {
                product.Recipe = new List<RecipeLine>(recipe);
            }
        }

        return Execute(
            () => Task.CompletedTask,
            "Đã lưu công thức tiêu hao."
        );
    }

    private Task Delete()
    {
        if (target == "")
        {
            error = "Chọn đối tượng cần xóa.";
            return Task.CompletedTask;
        }

        return Execute(
            () =>
            {
                if (toppingTab)
                {
                    toppings.RemoveAll(t => t.Code == target);
                }
                else if (categoryTab)
                {
                    categories.RemoveAll(c => c.Code == target);
                }
                else
                {
                    products.RemoveAll(p => p.Code == target);
                }
                return Task.CompletedTask;
            },
            "Đã xóa thành công."
        );
    }

    private void Close()
    {
        if (saving || reading)
        {
            return;
        }

        dialog = "";
        error = null;
        imageError = null;
    }

    private void RemoveImage()
    {
        if (reading)
        {
            return;
        }

        model.ImageUrl = null;
        imageError = null;
    }

    private async Task ReadImage(InputFileChangeEventArgs e)
    {
        if (reading)
        {
            return;
        }

        imageError = null;
        IBrowserFile file = e.File;

        bool invalidType = file.ContentType is not ("image/png" or "image/jpeg");
        bool tooLarge = file.Size > 2 * 1024 * 1024;

        if (invalidType || tooLarge)
        {
            imageError = "Chọn ảnh JPG/PNG không quá 2 MB.";
            return;
        }

        reading = true;

        try
        {
            IBrowserFile image = await file.RequestImageFileAsync("image/jpeg", 600, 600);
            await using Stream stream = image.OpenReadStream(3 * 1024 * 1024);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);

            model.ImageUrl = "data:image/jpeg;base64," + Convert.ToBase64String(memory.ToArray());
        }
        catch
        {
            imageError = "Không đọc được ảnh. Hãy chọn lại.";
        }
        finally
        {
            reading = false;
        }
    }
}
