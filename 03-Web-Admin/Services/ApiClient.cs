using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Lỗi khi gọi API. <see cref="Exception.Message"/> LUÔN là câu tiếng Việt dành cho người dùng
/// (lấy từ error.message của backend hoặc câu cố định) — trang được phép hiển thị trực tiếp.
/// </summary>
public sealed class ApiException : Exception
{
    public ApiException(int status, string? code, string message, JsonElement? details = null)
        : base(message)
    {
        Status = status;
        Code = code;
        Details = details;
    }

    /// <summary>Mã HTTP (0 = không kết nối được máy chủ).</summary>
    public int Status { get; }

    /// <summary>error.code của backend, ví dụ "CONFLICT", "HET_NGUYEN_LIEU".</summary>
    public string? Code { get; }

    /// <summary>error.details của backend (nếu có).</summary>
    public JsonElement? Details { get; }
}

/// <summary>
/// Lớp gọi REST API dùng chung cho mọi service: gửi JSON, đọc JSON,
/// đổi mọi lỗi (HTTP, mạng, hết thời gian, sai định dạng) thành <see cref="ApiException"/>.
/// Định dạng lỗi của backend: { "error": { "code", "message", "details" } }.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient http, ILogger<ApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(string url, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, url, null, ct);
        return await ReadAsync<T>(response, url, ct);
    }

    public async Task<T> PostAsync<T>(string url, object? body, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, url, body, ct);
        return await ReadAsync<T>(response, url, ct);
    }

    public async Task PostAsync(string url, object? body, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, url, body, ct);
    }

    public async Task<T> PutAsync<T>(string url, object? body, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Put, url, body, ct);
        return await ReadAsync<T>(response, url, ct);
    }

    public async Task<T> PatchAsync<T>(string url, object? body, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Patch, url, body, ct);
        return await ReadAsync<T>(response, url, ct);
    }

    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Delete, url, null, ct);
    }

    /// <summary>
    /// Đọc hết mọi trang của một danh sách phân trang (dùng cho danh sách nhỏ như thực đơn).
    /// </summary>
    public async Task<List<T>> GetAllPagesAsync<T>(string url, CancellationToken ct = default)
    {
        const int limit = 100; // tối đa backend cho phép
        string separator = url.Contains('?') ? "&" : "?";
        var all = new List<T>();

        for (int page = 1; ; page++)
        {
            var result = await GetAsync<PagedResult<T>>($"{url}{separator}page={page}&limit={limit}", ct);
            all.AddRange(result.Data);

            if (page >= result.Meta.TotalPages || result.Data.Count == 0)
            {
                return all;
            }
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        object? body,
        CancellationToken ct)
    {
        HttpResponseMessage response;

        try
        {
            using var request = new HttpRequestMessage(method, url);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body, body.GetType());
            }

            response = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không kết nối được máy chủ khi gọi {Method} {Url}.", method, url);
            throw new ApiException(0, null, "Không thể kết nối máy chủ. Kiểm tra mạng hoặc máy chủ API.");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient hết thời gian chờ (không phải do người dùng rời trang)
            _logger.LogWarning(ex, "{Method} {Url}: máy chủ phản hồi quá lâu.", method, url);
            throw new ApiException(0, null, "Máy chủ phản hồi quá lâu. Vui lòng thử lại.");
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            throw await ToExceptionAsync(response, method, url, ct);
        }
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response, string url, CancellationToken ct)
    {
        try
        {
            var data = await response.Content.ReadFromJsonAsync<T>(ct);
            return data ?? throw new JsonException("Phản hồi rỗng.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Phản hồi của {Url} không đúng định dạng.", url);
            throw new ApiException((int)response.StatusCode, null, "Không đọc được phản hồi từ máy chủ.");
        }
    }

    private async Task<ApiException> ToExceptionAsync(
        HttpResponseMessage response,
        HttpMethod method,
        string url,
        CancellationToken ct)
    {
        int status = (int)response.StatusCode;
        ApiErrorInfo? error = await ReadErrorAsync(response, ct);

        string message = MessageFor(status, error?.Message);

        if (status >= 500)
        {
            _logger.LogWarning("{Method} {Url} lỗi máy chủ {Status}: {Code} {Message}.",
                method, url, status, error?.Code, error?.Message);
        }

        return new ApiException(status, error?.Code, message, error?.Details);
    }

    /// <summary>
    /// Chọn câu thông báo theo mã trạng thái. Backend cam kết error.message tiếng Việt cho lỗi nghiệp vụ
    /// (4xx) nên dùng luôn; lỗi máy chủ (5xx) và lỗi không có nội dung dùng câu cố định.
    /// </summary>
    public static string MessageFor(int status, string? serverMessage)
    {
        bool hasMessage = !string.IsNullOrWhiteSpace(serverMessage);

        return status switch
        {
            401 => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
            400 or 403 or 404 or 409 or 422 or 429 when hasMessage => serverMessage!,
            400 or 422 => "Thông tin chưa hợp lệ. Vui lòng kiểm tra lại.",
            403 => "Bạn không có quyền thực hiện thao tác này.",
            404 => "Không tìm thấy dữ liệu trên máy chủ.",
            409 => "Dữ liệu bị trùng hoặc đã thay đổi. Vui lòng tải lại.",
            429 => "Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút.",
            _ => "Máy chủ đang gặp sự cố. Vui lòng thử lại sau."
        };
    }

    private static async Task<ApiErrorInfo?> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength is 0)
        {
            return null;
        }

        try
        {
            var body = await response.Content.ReadFromJsonAsync<ApiErrorBody>(ct);
            return body?.Error;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
