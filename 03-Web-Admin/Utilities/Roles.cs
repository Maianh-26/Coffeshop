using System.Security.Claims;

namespace CoffeeShop.Admin.Utilities;

/// <summary>
/// Vai trò người dùng — MỘT nơi duy nhất cho chuỗi vai trò, nhãn hiển thị và trang chính,
/// để không lệch chữ giữa các trang. Chuỗi phải khớp từng ký tự với backend
/// (trường "vaiTro" của api/v1/auth/dang-nhap, auth/toi, nguoi-dung).
/// </summary>
public static class Roles
{
    /// <summary>Chủ quán — toàn quyền (hiển thị "Quản lý").</summary>
    public const string Admin = "QuanLy";

    /// <summary>Nhân viên — chỉ trang Đơn hàng.</summary>
    public const string Staff = "NhanVien";

    /// <summary>Nhãn tiếng Việt hiển thị cho vai trò.</summary>
    public static string Label(string? role) => role switch
    {
        Admin => "Quản lý",
        Staff => "Nhân viên",
        _ => role ?? ""
    };

    public static string Label(ClaimsPrincipal user) => user.IsInRole(Admin) ? Label(Admin) : Label(Staff);

    /// <summary>Trang chính theo vai trò: chủ quán → Tổng quan, nhân viên → Đơn hàng.</summary>
    public static string HomeUrl(ClaimsPrincipal user) => user.IsInRole(Admin) ? "" : StaffHome;

    /// <summary>Trang nhân viên được mở (khớp menu nhân viên trong MainLayout).</summary>
    private static readonly string[] StaffPages = [StaffHome];

    private const string StaffHome = "orders";

    /// <summary>
    /// Trang mở sau khi đăng nhập. <paramref name="safeReturnUrl"/> là đường dẫn nội bộ đã kiểm tra an toàn ("" = không có).
    /// Chủ quán: về đúng trang định mở (hoặc Tổng quan). Nhân viên: chỉ về trang được phép, không thì Đơn hàng.
    /// </summary>
    public static string AfterLogin(string safeReturnUrl, bool isAdmin)
    {
        if (isAdmin) return safeReturnUrl;

        string page = safeReturnUrl.Split('?', '#')[0].Trim('/');
        return StaffPages.Any(p => page.Equals(p, StringComparison.OrdinalIgnoreCase)) ? safeReturnUrl : StaffHome;
    }
}
