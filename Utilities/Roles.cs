using System.Security.Claims;

namespace CoffeeShop.Admin.Utilities;

/// <summary>
/// Vai trò người dùng — MỘT nơi duy nhất cho chuỗi vai trò, nhãn hiển thị và trang chính,
/// để không lệch chữ giữa các trang. Chuỗi phải khớp từng ký tự với backend (auth/login, auth/me).
/// </summary>
public static class Roles
{
    /// <summary>Chủ quán — toàn quyền.</summary>
    public const string Admin = "Admin";

    /// <summary>Nhân viên — chỉ Đơn hàng và Tài khoản của tôi.</summary>
    public const string Staff = "Nhân viên";

    /// <summary>Nhãn tiếng Việt hiển thị cho vai trò.</summary>
    public static string Label(string? role) => role == Admin ? "Quản lý" : role ?? "";

    public static string Label(ClaimsPrincipal user) => user.IsInRole(Admin) ? Label(Admin) : Label(Staff);

    /// <summary>Trang chính theo vai trò: chủ quán → Tổng quan, nhân viên → Đơn hàng.</summary>
    public static string HomeUrl(ClaimsPrincipal user) => user.IsInRole(Admin) ? "" : "orders";
}
