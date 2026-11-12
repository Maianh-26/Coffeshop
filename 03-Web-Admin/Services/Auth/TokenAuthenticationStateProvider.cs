using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace CoffeeShop.Admin.Services.Auth;

/// <summary>
/// Nguồn trạng thái đăng nhập cho Blazor (dùng bởi [Authorize], AuthorizeView, AuthorizeRouteView).
/// Phiên đăng nhập (token, họ tên, vai trò) được lưu ở sessionStorage:
/// F5 vẫn giữ đăng nhập, đóng tab là phải đăng nhập lại — phù hợp máy dùng chung tại quầy.
/// </summary>
public class TokenAuthenticationStateProvider : AuthenticationStateProvider
{
    private const string StorageKey = "coffeeshop.session";

    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly IJSRuntime _js;
    private readonly AppState _appState;
    private readonly ILogger<TokenAuthenticationStateProvider> _logger;

    // Dùng chung một Task khôi phục: mọi lời gọi đồng thời đều chờ cùng một lần đọc sessionStorage
    private Task? _restoreTask;

    /// <summary>
    /// true khi người dùng tự bấm Đăng xuất — trang đăng nhập không cần nhớ "trang đang mở" (returnUrl).
    /// </summary>
    public bool SignedOutByUser { get; set; }

    public TokenAuthenticationStateProvider(
        IJSRuntime js,
        AppState appState,
        ILogger<TokenAuthenticationStateProvider> logger)
    {
        _js = js;
        _appState = appState;
        _logger = logger;
    }

    /// <summary>
    /// Gọi một lần trong Program.cs trước RunAsync: đọc phiên từ sessionStorage
    /// để trạng thái đăng nhập có sẵn ngay ở lần vẽ đầu tiên.
    /// </summary>
    public Task InitializeAsync() => _restoreTask ??= RestoreAsync();

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Lần đầu (mở trang / F5): khôi phục phiên từ sessionStorage
        await (_restoreTask ??= RestoreAsync());

        return _appState.IsLoggedIn
            ? new AuthenticationState(BuildPrincipal())
            : Anonymous;
    }

    /// <summary>
    /// Ghi nhận đăng nhập thành công và báo cho toàn bộ giao diện cập nhật.
    /// </summary>
    public async Task SignInAsync(string token, string fullName, string role)
    {
        _appState.Login(token, fullName, role);
        _restoreTask ??= Task.CompletedTask;
        SignedOutByUser = false;

        try
        {
            string json = JsonSerializer.Serialize(new StoredSession(token, fullName, role));
            await _js.InvokeVoidAsync("sessionStorage.setItem", StorageKey, json);
        }
        catch (JSException ex)
        {
            // Không lưu được (trình duyệt chặn storage) thì vẫn đăng nhập trong phiên hiện tại
            _logger.LogWarning(ex, "Không lưu được phiên đăng nhập vào sessionStorage.");
        }

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(BuildPrincipal())));
    }

    /// <summary>
    /// Đăng xuất: xóa phiên và báo giao diện (AuthorizeRouteView sẽ chuyển về trang Đăng nhập).
    /// </summary>
    /// <param name="byUser">true = người dùng bấm Đăng xuất; false = phiên hết hạn (401).</param>
    public async Task SignOutAsync(bool byUser = true)
    {
        SignedOutByUser = byUser;
        _appState.Logout();

        try
        {
            await _js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "Không xóa được phiên đăng nhập trong sessionStorage.");
        }

        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    private async Task RestoreAsync()
    {
        try
        {
            string? json = await _js.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);

            if (string.IsNullOrEmpty(json)) return;

            var session = JsonSerializer.Deserialize<StoredSession>(json);

            if (session is { Token.Length: > 0 })
            {
                _appState.Login(session.Token, session.FullName, session.Role);
            }
        }
        catch (Exception ex) when (ex is JSException or JsonException)
        {
            _logger.LogWarning(ex, "Không khôi phục được phiên đăng nhập.");
        }
    }

    private ClaimsPrincipal BuildPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, _appState.FullName ?? ""),
        };

        if (!string.IsNullOrEmpty(_appState.Role))
        {
            claims.Add(new Claim(ClaimTypes.Role, _appState.Role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Token"));
    }

    private sealed record StoredSession(string Token, string FullName, string Role);
}
