using System.Text.Json;

namespace CoffeeShop.Admin.Services.Auth;

/// <summary>
/// Đọc phần payload của JWT do backend cấp (claim <c>role</c>, <c>exp</c>) — KHÔNG kiểm tra chữ ký
/// (việc đó là của máy chủ). Dùng để vai trò hiển thị luôn lấy từ chính token (sửa tay sessionStorage không đổi được menu)
/// và để biết giờ hết hạn phiên.
/// </summary>
public sealed record TokenClaims(string? Role, DateTime? ExpiresUtc)
{
    /// <summary>Thời hạn token dài nhất coi là hợp lý (backend cấp 12 giờ) + dư 1 giờ.</summary>
    private static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(13);

    /// <summary>
    /// Giờ hết hạn chỉ dùng được khi đồng hồ máy khách khớp máy chủ: ngay lúc vừa đăng nhập, thời gian còn lại phải nằm trong
    /// (0, 13 giờ]. Ngoài khoảng đó (máy quầy chạy sai giờ) → null: không báo / không tự đăng xuất theo giờ máy khách,
    /// để lỗi 401 của máy chủ tự xử lý — tránh vừa đăng nhập đã bị đẩy ra.
    /// </summary>
    public DateTime? ReliableExpiresUtc(DateTime nowUtc) =>
        ExpiresUtc is { } exp && exp > nowUtc && exp - nowUtc <= MaxLifetime ? exp : null;

    public static TokenClaims? TryRead(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;

        string[] parts = token.Split('.');
        if (parts.Length != 3) return null;

        try
        {
            using var doc = JsonDocument.Parse(DecodeBase64Url(parts[1]));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            DateTime? expires = root.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out long seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                : null;

            return new TokenClaims(ReadString(root, "role"), expires);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static byte[] DecodeBase64Url(string value)
    {
        string s = value.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        return Convert.FromBase64String(s);
    }
}
