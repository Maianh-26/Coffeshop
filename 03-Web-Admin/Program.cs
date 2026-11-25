using CoffeeShop.Admin;
using CoffeeShop.Admin.Services;
using CoffeeShop.Admin.Services.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient
// Mọi request "api/v1/..." gọi backend thật tại Api:BaseUrl (wwwroot/appsettings.json).
// Mọi request đi qua AuthHeaderHandler (gắn token, tự đăng xuất khi gặp 401) trước khi tới API.
string? configuredBaseUrl = builder.Configuration["Api:BaseUrl"];
string baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : configuredBaseUrl.Trim();

// Đường dẫn tương đối "api/v1/..." chỉ ghép đúng khi BaseAddress kết thúc bằng "/"
if (!baseUrl.EndsWith('/')) baseUrl += "/";

builder.Services.AddScoped(sp =>
{
    var authHandler = new AuthHeaderHandler(sp.GetRequiredService<AppState>(), sp)
    {
        InnerHandler = new HttpClientHandler()
    };

    return new HttpClient(authHandler) { BaseAddress = new Uri(baseUrl) };
});

// Đăng nhập & phân quyền ([Authorize], AuthorizeView, AuthorizeRouteView)
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<TokenAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<TokenAuthenticationStateProvider>());

// Application State & Services
builder.Services.AddSingleton<AppState>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<ToppingService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<PromotionService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportExporter>();

// Log phân quyền ("Authorization failed…") chỉ hiện khi là cảnh báo, cho Console gọn
builder.Logging.AddFilter("Microsoft.AspNetCore.Authorization", LogLevel.Warning);

var host = builder.Build();

// Khôi phục phiên đăng nhập (sessionStorage) TRƯỚC khi vẽ giao diện
// → không có pha "đang kiểm tra" và không chớp khung quản trị khi F5 / khi chưa đăng nhập.
var authProvider = host.Services.GetRequiredService<TokenAuthenticationStateProvider>();
await authProvider.InitializeAsync();

await host.RunAsync();
