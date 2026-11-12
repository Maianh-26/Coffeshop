using CoffeeShop.Admin.Models;
using CoffeeShop.Admin.Services;
using CoffeeShop.Admin.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace CoffeeShop.Admin.Pages.Authentication;

public partial class Login
{
    [Inject]
    private AuthenticationService AuthService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ILogger<Login> Logger { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthState { get; set; }

    /// <summary>
    /// Trang người dùng định mở trước khi bị chuyển về đăng nhập.
    /// </summary>
    [SupplyParameterFromQuery]
    public string? ReturnUrl { get; set; }

    private LoginRequest model = new();

    private bool showPassword;
    private bool loading;
    private string? error;

    protected override async Task OnInitializedAsync()
    {
        // Đã đăng nhập rồi (ví dụ F5 ở /login) thì vào thẳng trang quản lý
        if (AuthState is not null)
        {
            var state = await AuthState;

            if (state.User.Identity?.IsAuthenticated == true)
            {
                Navigation.NavigateTo(TargetAfterLogin(state.User.IsInRole(Roles.Admin)), replace: true);
            }
        }
    }

    private async Task HandleLogin()
    {
        if (loading) return;

        try
        {
            loading = true;
            error = null;

            var result = await AuthService.LoginAsync(model);

            if (result.Success)
            {
                Navigation.NavigateTo(TargetAfterLogin(result.Role == Roles.Admin), replace: true);
            }
            else
            {
                error = result.Message ?? "Đăng nhập thất bại. Vui lòng kiểm tra lại.";

                // Xóa mật khẩu sai để gõ lại ngay
                model.Password = "";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Lỗi không mong muốn khi đăng nhập.");
            error = "Đã có lỗi xảy ra. Vui lòng thử lại.";
        }
        finally
        {
            loading = false;
        }
    }

    // Trang nhân viên được phép mở (khớp menu nhân viên)
    private static readonly string[] StaffPages = ["orders", "my-account"];

    /// <summary>
    /// Trang mở sau khi đăng nhập: trang định mở trước đó (nếu hợp lệ và đúng quyền),
    /// không thì trang chính theo vai trò — chủ quán: Tổng quan, nhân viên: Đơn hàng.
    /// </summary>
    private string TargetAfterLogin(bool isAdmin)
    {
        string target = SafeReturnUrl();

        if (isAdmin)
        {
            return target;
        }

        string page = target.Split('?', '#')[0].Trim('/');

        return StaffPages.Any(p => page.Equals(p, StringComparison.OrdinalIgnoreCase))
            ? target
            : "orders";
    }

    /// <summary>
    /// Chỉ cho quay về đường dẫn NỘI BỘ của ứng dụng — dùng danh sách CHO PHÉP, không dùng danh sách chặn,
    /// để chặn mọi dạng chuyển hướng ra ngoài / chạy script
    /// (javascript:..., //evil.com, /\evil.com, ký tự tab/điều khiển ở đầu…).
    /// </summary>
    private string SafeReturnUrl()
    {
        string candidate = (ReturnUrl ?? "").TrimStart('/');

        // Chữ/số ở đầu; sau đó chỉ các ký tự an toàn của đường dẫn + query.
        // Không có ':' (scheme), '\', khoảng trắng hay ký tự điều khiển.
        if (candidate.Length == 0
            || !SafeRelativePath().IsMatch(candidate)
            || candidate.StartsWith("login", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        // Lớp kiểm tra thứ hai: đường dẫn tuyệt đối phải nằm trong ứng dụng này
        Uri absolute = Navigation.ToAbsoluteUri(candidate);

        return absolute.ToString().StartsWith(Navigation.BaseUri, StringComparison.OrdinalIgnoreCase)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
            ? candidate
            : "";
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9\-._~/?=&%]*$")]
    private static partial System.Text.RegularExpressions.Regex SafeRelativePath();
}
