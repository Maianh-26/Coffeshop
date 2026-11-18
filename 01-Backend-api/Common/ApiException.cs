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

public static class DbErrors
{
    /// <summary>
    /// Vi phạm UNIQUE: 2627 (UNIQUE/PRIMARY KEY constraint) hoặc 2601 (unique index, kể cả index lọc).
    /// Nhận diện theo mã lỗi, không theo nội dung message (message của 2601 viết thường "unique index").
    /// </summary>
    public static bool LaTrungKhoa(Microsoft.EntityFrameworkCore.DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && LaTrungKhoa(sql);

    /// <summary>Bản cho SqlException ném trực tiếp (ExecuteUpdateAsync / ExecuteSql không bọc trong DbUpdateException).</summary>
    public static bool LaTrungKhoa(Microsoft.Data.SqlClient.SqlException ex) => ex.Number is 2601 or 2627;
}
