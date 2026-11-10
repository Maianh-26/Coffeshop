using System.Net.Http.Json;
using System.Text.Json;
using CoffeeShop.Admin.Models;
using CoffeeShop.Admin.Services.Auth;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Service xác thực: đăng nhập (api/v1/auth/dang-nhap), tài khoản của tôi, đổi mật khẩu.
/// Quên mật khẩu (api/auth/forgot-password…) backend CHƯA có — hiện chỉ chế độ giả lập trả lời.
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
    // Quên mật khẩu: email → mã xác minh → mật khẩu mới
    // ------------------------------------------------------------------

    /// <summary>
    /// Bước 1: yêu cầu backend gửi mã xác minh tới email (backend gửi qua Gmail).
    /// </summary>
    public Task<AuthResult<ForgotPasswordResponse>> RequestResetCodeAsync(
        string email,
        CancellationToken cancellationToken = default) =>
        PostAsync<ForgotPasswordResponse>(
            "api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email.Trim() },
            cancellationToken);

    /// <summary>
    /// Bước 2: kiểm tra mã 6 số, nhận về token đặt lại mật khẩu.
    /// </summary>
    public Task<AuthResult<VerifyResetCodeResponse>> VerifyResetCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default) =>
        PostAsync<VerifyResetCodeResponse>(
            "api/auth/verify-reset-code",
            new VerifyResetCodeRequest { Email = email.Trim(), Code = code.Trim() },
            cancellationToken);

    /// <summary>
    /// Bước 3: đặt mật khẩu mới bằng token vừa nhận.
    /// </summary>
    public async Task<AuthResult<bool>> ResetPasswordAsync(
        string resetToken,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        // Không đọc body: backend có thể trả 204 hoặc 200 rỗng — chỉ cần biết thành công hay không
        var result = await PostAsync<NoBody>(
            "api/auth/reset-password",
            new ResetPasswordRequest { ResetToken = resetToken, NewPassword = newPassword },
            cancellationToken);

        return result.Success
            ? AuthResult<bool>.Ok(true)
            : AuthResult<bool>.Fail(result.Error!, result.ErrorCode);
    }

    // ------------------------------------------------------------------
    // Tài khoản của tôi (cần đăng nhập)
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
    /// Đổi mật khẩu của chính tài khoản đang đăng nhập: POST api/v1/auth/doi-mat-khau → 204.
    /// Sai mật khẩu cũ → 400 BAD_REQUEST (lỗi kiểm tra dữ liệu là 400 VALIDATION_ERROR).
    /// </summary>
    public async Task<AuthResult<bool>> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<NoBody>(
            "api/v1/auth/doi-mat-khau",
            new DoiMatKhauRequest { MatKhauCu = currentPassword, MatKhauMoi = newPassword },
            cancellationToken);

        if (result.Success)
        {
            return AuthResult<bool>.Ok(true);
        }

        // Endpoint này chỉ trả BAD_REQUEST khi mật khẩu cũ sai → báo ngay dưới ô "Mật khẩu hiện tại"
        string? errorCode = result.ErrorCode == ApiErrorCodes.BadRequest
            ? ResetErrorCodes.WrongPassword
            : result.ErrorCode;

        return AuthResult<bool>.Fail(result.Error!, errorCode);
    }

    /// <summary>
    /// Đánh dấu API không trả nội dung (bỏ qua việc đọc body).
    /// </summary>
    private sealed class NoBody;

    /// <summary>
    /// Gửi POST, trả kết quả hoặc thông báo lỗi tiếng Việt cho người dùng.
    /// </summary>
    private async Task<AuthResult<T>> PostAsync<T>(
        string url,
        object body,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(url, body, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                bool hasBody = typeof(T) != typeof(NoBody)
                               && response.StatusCode != System.Net.HttpStatusCode.NoContent
                               && response.Content.Headers.ContentLength is not 0;

                T? data = hasBody
                    ? await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                    : default;

                return AuthResult<T>.Ok(data!);
            }

            var (title, errorCode) = await ReadErrorAsync(response, cancellationToken);

            // 429 kèm header chuẩn Retry-After (số giây) → giao diện đếm ngược chính xác
            int? retryAfter = response.Headers.RetryAfter?.Delta is { } delta
                ? (int)Math.Ceiling(delta.TotalSeconds)
                : null;

            return AuthResult<T>.Fail(MessageFor(url, (int)response.StatusCode, title), errorCode, retryAfter);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không kết nối được máy chủ khi gọi {Url}.", url);
            return AuthResult<T>.Fail("Không thể kết nối máy chủ. Kiểm tra mạng hoặc máy chủ API.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Phản hồi của {Url} không đúng định dạng.", url);
            return AuthResult<T>.Fail("Không đọc được phản hồi từ máy chủ.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient hết thời gian chờ (không phải do người dùng rời trang)
            _logger.LogWarning("{Url}: máy chủ phản hồi quá lâu.", url);
            return AuthResult<T>.Fail("Máy chủ phản hồi quá lâu. Vui lòng thử lại.");
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
    /// API quên mật khẩu (giả lập) vẫn dùng ProblemDetails: { title, errorCode }.
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

            if (body.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                return (Read(error, "message"), Read(error, "code"));
            }

            return (Read(body, "title"), Read(body, "errorCode"));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return (null, null);
        }
    }
}
