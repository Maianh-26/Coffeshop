using System.Text;
using CoffeeShop.Admin.Services.Auth;

namespace CoffeeShop.Admin.Tests;

/// <summary>Đọc vai trò và giờ hết hạn từ JWT (không kiểm tra chữ ký).</summary>
public class TokenClaimsTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 28, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>Dựng một JWT giả (header.payload.chữ-ký) với payload JSON cho trước.</summary>
    private static string Jwt(string payloadJson) =>
        $"{Base64Url("{\"alg\":\"HS256\"}")}.{Base64Url(payloadJson)}.chuky";

    private static string Base64Url(string text) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static long Unix(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeSeconds();

    [Fact]
    public void TryRead_DocDuocVaiTroVaGioHetHan()
    {
        var exp = NowUtc.AddHours(12);

        var claims = TokenClaims.TryRead(Jwt($"{{\"role\":\"QuanLy\",\"exp\":{Unix(exp)}}}"));

        Assert.NotNull(claims);
        Assert.Equal("QuanLy", claims.Role);
        Assert.Equal(exp, claims.ExpiresUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("khong-phai-jwt")]
    [InlineData("a.%%%.c")]          // payload không phải base64
    public void TryRead_TokenHong_TraVeNull(string? token)
    {
        Assert.Null(TokenClaims.TryRead(token));
    }

    [Fact]
    public void TryRead_ThieuRoleVaExp_TraVeNullTungTruong()
    {
        var claims = TokenClaims.TryRead(Jwt("{\"name\":\"quanly\"}"));

        Assert.NotNull(claims);
        Assert.Null(claims.Role);
        Assert.Null(claims.ExpiresUtc);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(13)]    // mốc biên: còn đúng 13 giờ vẫn hợp lệ
    public void ReliableExpiresUtc_ChapNhan_KhiConTrongThoiHanHopLy(int hoursLeft)
    {
        var claims = new TokenClaims("QuanLy", NowUtc.AddHours(hoursLeft));

        Assert.Equal(NowUtc.AddHours(hoursLeft), claims.ReliableExpiresUtc(NowUtc));
    }

    [Theory]
    [InlineData(-1)]    // đã hết hạn theo đồng hồ máy khách
    [InlineData(0)]     // hết hạn đúng lúc này
    [InlineData(20)]    // còn quá 13 giờ → đồng hồ máy khách sai
    public void ReliableExpiresUtc_BoQua_KhiDongHoMayKhachKhongTinDuoc(int hoursLeft)
    {
        var claims = new TokenClaims("QuanLy", NowUtc.AddHours(hoursLeft));

        Assert.Null(claims.ReliableExpiresUtc(NowUtc));
    }
}
