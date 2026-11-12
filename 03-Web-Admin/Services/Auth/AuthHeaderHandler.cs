using System.Net;
using System.Net.Http.Headers;

namespace CoffeeShop.Admin.Services.Auth;

/// <summary>
/// Gắn token đăng nhập vào mọi request API (header "Authorization: Bearer ...").
/// Nếu máy chủ trả 401 (token hết hạn / không hợp lệ) thì tự đăng xuất,
/// AuthorizeRouteView sẽ đưa người dùng về trang Đăng nhập.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly AppState _appState;
    private readonly IServiceProvider _services;

    public AuthHeaderHandler(AppState appState, IServiceProvider services)
    {
        _appState = appState;
        _services = services;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? sentToken = _appState.Token;

        if (!string.IsNullOrEmpty(sentToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sentToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Chỉ đăng xuất nếu 401 là của CHÍNH phiên hiện tại
        // (request cũ gửi bằng token trước đó không được đăng xuất phiên vừa đăng nhập lại)
        if (response.StatusCode == HttpStatusCode.Unauthorized
            && _appState.IsLoggedIn
            && _appState.Token == sentToken)
        {
            // Lấy provider lúc cần để tránh phụ thuộc vòng khi tạo HttpClient
            await _services.GetRequiredService<TokenAuthenticationStateProvider>().SignOutAsync(byUser: false);
        }

        return response;
    }
}
