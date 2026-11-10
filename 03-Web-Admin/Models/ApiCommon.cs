using System.Text.Json;

namespace CoffeeShop.Admin.Models;

// ==========================================================
// Kiểu dùng chung của API backend (CoffeeShop.Api, tiền tố api/v1).
// Hợp đồng chuẩn: openapi/openapi.yaml của nhóm backend.
// Tên thuộc tính = tên trường JSON (camelCase) của backend → giữ đúng chính tả khi sửa.
// ==========================================================

/// <summary>
/// Danh sách có phân trang: { data: [...], meta: { page, limit, total, totalPages } }.
/// Dùng cho GET mon, don-hang, lich-su-kho.
/// </summary>
public class PagedResult<T>
{
    public List<T> Data { get; set; } = [];
    public PageMeta Meta { get; set; } = new();
}

public class PageMeta
{
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
    public int Total { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// Định dạng lỗi chung: { error: { code, message, details } }.
/// </summary>
public class ApiErrorBody
{
    public ApiErrorInfo? Error { get; set; }
}

public class ApiErrorInfo
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public JsonElement? Details { get; set; }
}

/// <summary>
/// Mã lỗi máy đọc được của backend (error.code).
/// </summary>
public static class ApiErrorCodes
{
    public const string Validation = "VALIDATION_ERROR";
    public const string BadRequest = "BAD_REQUEST";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string OutOfIngredients = "HET_NGUYEN_LIEU";
}
