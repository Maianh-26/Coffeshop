using System.Net.Http.Json;
using System.Text.Json;
using CoffeeShop.Admin.Models;
using CoffeeShop.Admin.Services.Auth;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Service xác thực: đăng nhập (api/v1/auth/dang-nhap), đăng xuất, thông tin tài khoản đang đăng nhập (api/v1/auth/toi).
/// Không có quên mật khẩu: quán chỉ vài tài khoản, chủ quán đặt lại mật khẩu ở trang Tài khoản (Sửa → Mật khẩu mới).
/// </summary>
public class AuthenticationService
{
    private readonly HttpClient _http;
    private readonly AppState _appState;
    private readonly TokenAuthenticationStateProvider _authState;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        HttpClient http,
        AppState appState,
        TokenAuthenticationStateProvider authState,
        ILogger<AuthenticationService> logger)
    {
        _http = http;
        _appState = appState;
        _authState = authState;
        _logger = logger;
    }

    /// <summary>
    /// Đăng nhập: POST api/v1/auth/dang-nhap → { token, hetHan, nguoiDung }.
    /// Sai thông tin → 401, tài khoản bị khóa → 403 (backend trả error.message tiếng Việt).
    /// </summary>
    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                "api/v1/auth/dang-nhap",
                new DangNhapRequest { TenDangNhap = request.Username.Trim(), MatKhau = request.Password },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                int status = (int)response.StatusCode;
                var (message, _) = await ReadErrorAsync(response, cancellationToken);

                if (status >= 500)
                {
                    _logger.LogWarning("Đăng nhập lỗi: máy chủ trả {Status}.", status);
                }

                return Failure(status switch
                {
                    401 => string.IsNullOrWhiteSpace(message) ? "Sai tên đăng nhập hoặc mật khẩu." : message,
                    403 => string.IsNullOrWhiteSpace(message) ? "Tài khoản đã bị khóa, liên hệ quản lý." : message,
                    400 => "Vui lòng nhập đủ tên đăng nhập và mật khẩu.",
                    _ => ApiClient.MessageFor(status, message)
                });
            }

            var result = await response.Content.ReadFromJsonAsync<DangNhapResponse>(cancellationToken);

            // Thành công mà không có token → coi là phản hồi lỗi (không thì mọi request sau đều 401)
            if (result is null || string.IsNullOrWhiteSpace(result.Token))
            {
                _logger.LogWarning("Đăng nhập trả 200 nhưng không có token.");
                return Failure("Không đọc được phản hồi từ máy chủ.");
            }

            string fullName = string.IsNullOrWhiteSpace(result.NguoiDung.HoTen)
                ? result.NguoiDung.TenDangNhap
                : result.NguoiDung.HoTen;

            await _authState.SignInAsync(result.Token, fullName, result.NguoiDung.VaiTro);

            return new LoginResponse
            {
                Success = true,
                Token = result.Token,
                FullName = fullName,
                Role = result.NguoiDung.VaiTro
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không kết nối được máy chủ khi đăng nhập.");
            return Failure("Không thể kết nối máy chủ. Kiểm tra mạng hoặc máy chủ API.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Phản hồi đăng nhập không đúng định dạng.");
            return Failure("Không đọc được phản hồi từ máy chủ.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient hết thời gian chờ (không phải do người dùng rời trang)
            _logger.LogWarning("Đăng nhập: máy chủ phản hồi quá lâu.");
            return Failure("Máy chủ phản hồi quá lâu. Vui lòng thử lại.");
        }
    }

    public Task LogoutAsync() => _authState.SignOutAsync();

    public bool IsLoggedIn => _appState.IsLoggedIn;

    private static LoginResponse Failure(string message) =>
        new() { Success = false, Message = message };

    // ------------------------------------------------------------------
    // Tài khoản đang đăng nhập (trang Tài khoản dùng để không cho tự xóa chính mình)
    // ------------------------------------------------------------------

    /// <summary>
    /// Thông tin tài khoản đang đăng nhập (GET api/v1/auth/toi).
    /// </summary>
    public async Task<AuthResult<NguoiDung>> GetMyAccountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync("api/v1/auth/toi", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var account = await response.Content.ReadFromJsonAsync<NguoiDung>(cancellationToken);
                return account is null
                    ? AuthResult<NguoiDung>.Fail("Không đọc được thông tin tài khoản.")
                    : AuthResult<NguoiDung>.Ok(account);
            }

            var (message, errorCode) = await ReadErrorAsync(response, cancellationToken);
            return AuthResult<NguoiDung>.Fail(MessageFor("api/v1/auth/toi", (int)response.StatusCode, message), errorCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không kết nối được máy chủ khi tải tài khoản.");
            return AuthResult<NguoiDung>.Fail("Không thể kết nối máy chủ. Kiểm tra mạng hoặc máy chủ API.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Phản hồi api/v1/auth/toi không đúng định dạng.");
            return AuthResult<NguoiDung>.Fail("Không đọc được thông tin tài khoản.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthResult<NguoiDung>.Fail("Máy chủ phản hồi quá lâu. Vui lòng thử lại.");
        }
    }

    /// <summary>
    /// Câu thông báo theo mã trạng thái (dùng chung quy tắc với <see cref="ApiClient.MessageFor"/>).
    /// </summary>
    private string MessageFor(string url, int status, string? message)
    {
        if (status >= 500)
        {
            _logger.LogWarning("{Url} lỗi máy chủ {Status}.", url, status);
        }

        return ApiClient.MessageFor(status, message);
    }

    /// <summary>
    /// Đọc thông báo lỗi và mã lỗi. Backend: { error: { code, message } }.
    /// </summary>
    private static async Task<(string? Message, string? ErrorCode)> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is 0)
        {
            return (null, null);
        }

        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

            if (body.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }

            static string? Read(JsonElement obj, string name) =>
                obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;

            return body.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
                ? (Read(error, "message"), Read(error, "code"))
                : (null, null);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return (null, null);
        }
    }
}
