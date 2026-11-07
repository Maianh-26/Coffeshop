using System.Net.Http.Json;
using System.Text.Json;
using CoffeeShop.Admin.Models;
using CoffeeShop.Admin.Services.Auth;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Service xác thực đăng nhập: gọi api/auth/login và ghi nhận phiên đăng nhập.
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

    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", request, cancellationToken);

            // Sai thông tin đăng nhập: backend có thể trả 200 + Success=false (như mock) hoặc 400/401
            if (response.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Unauthorized)
            {
                return Failure("Sai tên đăng nhập hoặc mật khẩu.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Đăng nhập lỗi: máy chủ trả {Status}.", (int)response.StatusCode);
                return Failure("Máy chủ đang gặp sự cố. Vui lòng thử lại sau.");
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);

            if (result is null)
            {
                return Failure("Không đọc được phản hồi từ máy chủ.");
            }

            // Báo thành công mà không có token → coi là phản hồi lỗi (không thì mọi request sau đều 401)
            if (result.Success && string.IsNullOrWhiteSpace(result.Token))
            {
                _logger.LogWarning("Đăng nhập trả Success=true nhưng không có token.");
                return Failure("Không đọc được phản hồi từ máy chủ.");
            }

            if (result.Success)
            {
                await _authState.SignInAsync(
                    result.Token ?? "",
                    result.FullName ?? "",
                    result.Role ?? "");
            }

            return result;
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
    /// Thông tin tài khoản đang đăng nhập (GET api/auth/me).
    /// </summary>
    public async Task<AuthResult<Account>> GetMyAccountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync("api/auth/me", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var account = await response.Content.ReadFromJsonAsync<Account>(cancellationToken);
                return account is null
                    ? AuthResult<Account>.Fail("Không đọc được thông tin tài khoản.")
                    : AuthResult<Account>.Ok(account);
            }

            var (title, errorCode) = await ReadProblemAsync(response, cancellationToken);
            return AuthResult<Account>.Fail(MessageFor("api/auth/me", (int)response.StatusCode, title), errorCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không kết nối được máy chủ khi tải tài khoản.");
            return AuthResult<Account>.Fail("Không thể kết nối máy chủ. Kiểm tra mạng hoặc máy chủ API.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Phản hồi api/auth/me không đúng định dạng.");
            return AuthResult<Account>.Fail("Không đọc được thông tin tài khoản.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthResult<Account>.Fail("Máy chủ phản hồi quá lâu. Vui lòng thử lại.");
        }
    }

    /// <summary>
    /// Đổi mật khẩu của chính tài khoản đang đăng nhập (cần mật khẩu hiện tại).
    /// </summary>
    public async Task<AuthResult<bool>> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<NoBody>(
            "api/auth/change-password",
            new ChangePasswordRequest { CurrentPassword = currentPassword, NewPassword = newPassword },
            cancellationToken);

        return result.Success
            ? AuthResult<bool>.Ok(true)
            : AuthResult<bool>.Fail(result.Error!, result.ErrorCode);
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

            var (title, errorCode) = await ReadProblemAsync(response, cancellationToken);

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
    /// Chọn câu thông báo theo mã trạng thái. Chỉ dùng "title" của máy chủ cho 400/409/429
    /// (backend cam kết title tiếng Việt cho người dùng); các mã khác dùng câu cố định,
    /// tránh hiện title tiếng Anh mặc định của ASP.NET ("Not Found", "One or more validation errors…").
    /// </summary>
    private string MessageFor(string url, int status, string? title)
    {
        switch (status)
        {
            case 400 or 409 when !string.IsNullOrWhiteSpace(title) && !title.StartsWith("One or more", StringComparison.Ordinal):
                return title;
            case 400:
                return "Thông tin chưa hợp lệ. Vui lòng kiểm tra lại.";
            case 409:
                return "Dữ liệu bị trùng hoặc đã thay đổi. Vui lòng kiểm tra lại.";
            case 429:
                return string.IsNullOrWhiteSpace(title)
                    ? "Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút."
                    : title;
            case 401:
                return "Phiên làm việc đã hết hạn. Vui lòng thử lại.";
            case 403:
                return "Bạn không có quyền thực hiện thao tác này.";
            case 404:
                return "Không tìm thấy chức năng trên máy chủ.";
            default:
                _logger.LogWarning("{Url} lỗi máy chủ {Status}.", url, status);
                return "Máy chủ đang gặp sự cố. Vui lòng thử lại sau.";
        }
    }

    /// <summary>
    /// Đọc "title" và mã lỗi máy đọc được ("errorCode", phần mở rộng của ProblemDetails).
    /// </summary>
    private static async Task<(string? Title, string? ErrorCode)> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is 0)
        {
            return (null, null);
        }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

            if (problem.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }

            string? Read(string name) =>
                problem.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;

            return (Read("title"), Read("errorCode"));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
