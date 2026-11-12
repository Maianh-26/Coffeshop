namespace CoffeeShop.Api.Common;

/// <summary>Lỗi nghiệp vụ → được ErrorHandlingMiddleware đổi thành JSON { error: { code, message, details } }.</summary>
public class ApiException(int status, string code, string message, object? details = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public object? Details { get; } = details;

    public static ApiException BadRequest(string msg, object? details = null) => new(400, "BAD_REQUEST", msg, details);
    public static ApiException Forbidden(string msg = "Bạn không có quyền thực hiện thao tác này") => new(403, "FORBIDDEN", msg);
    public static ApiException NotFound(string what = "Tài nguyên") => new(404, "NOT_FOUND", $"{what} không tồn tại");
    public static ApiException Conflict(string msg, object? details = null) => new(409, "CONFLICT", msg, details);
    public static ApiException Unprocessable(string code, string msg, object? details = null) => new(422, code, msg, details);
}
