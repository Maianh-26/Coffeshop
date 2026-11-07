using CoffeeShop.Admin;
using CoffeeShop.Admin.Services;
using CoffeeShop.Admin.Services.Auth;
using CoffeeShop.Admin.Services.Mock;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient
// Api:UseMock = true  → mọi request "api/..." được trả lời bởi dữ liệu giả trong bộ nhớ (Services/Mock).
// Api:UseMock = false → gọi backend thật tại Api:BaseUrl.
// Cấu hình nằm trong wwwroot/appsettings.json.
// Mọi request đi qua AuthHeaderHandler (gắn token, tự đăng xuất khi gặp 401) trước khi tới API.
var api = builder.Configuration.GetSection("Api");
bool useMock = api.GetValue("UseMock", true);

if (useMock)
{
    builder.Services.AddSingleton<MockDatabase>();
}

string baseUrl = useMock
    ? builder.HostEnvironment.BaseAddress
    : api["BaseUrl"] ?? builder.HostEnvironment.BaseAddress;

builder.Services.AddScoped(sp =>
{
    HttpMessageHandler inner = useMock
        ? new MockApiHandler(
            sp.GetRequiredService<MockDatabase>(),
            api.GetValue("MockLatencyMs", 300),
            api.GetValue("MockNewTabletOrderEverySeconds", 0))
        : new HttpClientHandler();

    var authHandler = new AuthHeaderHandler(sp.GetRequiredService<AppState>(), sp)
    {
        InnerHandler = inner
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
await host.Services.GetRequiredService<TokenAuthenticationStateProvider>().InitializeAsync();

await host.RunAsync();
