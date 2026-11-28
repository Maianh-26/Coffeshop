using CoffeeShop.Admin.Utilities;

namespace CoffeeShop.Admin.Tests;

/// <summary>Trang mở sau khi đăng nhập theo vai trò.</summary>
public class RolesTests
{
    [Theory]
    [InlineData("", "")]                       // không có trang định mở → Tổng quan
    [InlineData("products", "products")]
    [InlineData("orders?trang=2", "orders?trang=2")]
    public void AfterLogin_ChuQuan_VeDungTrangDinhMo(string returnUrl, string expected)
    {
        Assert.Equal(expected, Roles.AfterLogin(returnUrl, isAdmin: true));
    }

    [Theory]
    [InlineData("", "orders")]
    [InlineData("products", "orders")]         // trang quản lý → bỏ qua, về Đơn hàng
    [InlineData("accounts?x=1", "orders")]
    [InlineData("orders?trang=2", "orders?trang=2")]
    [InlineData("ORDERS", "ORDERS")]
    [InlineData("orders#chi-tiet", "orders#chi-tiet")]
    [InlineData("/orders", "/orders")]
    public void AfterLogin_NhanVien_ChiVeTrangDuocPhep(string returnUrl, string expected)
    {
        Assert.Equal(expected, Roles.AfterLogin(returnUrl, isAdmin: false));
    }
}
