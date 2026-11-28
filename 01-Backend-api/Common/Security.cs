using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CoffeeShop.Api.Data;
using Microsoft.IdentityModel.Tokens;

namespace CoffeeShop.Api.Common;

public class JwtOptions
{
    public string Secret { get; set; } = "";
    public string Issuer { get; set; } = "coffee-shop-api";
    public int ExpiresInHours { get; set; } = 12;

    public SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(Secret));
}

public class TokenService(JwtOptions opt)
{
    public (string Token, DateTime HetHan) Create(NguoiDung user)
    {
        var hetHanUtc = DateTime.UtcNow.AddHours(opt.ExpiresInHours);
        var token = new JwtSecurityToken(
            issuer: opt.Issuer,
            audience: opt.Issuer,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.MaNguoiDung.ToString()),
                new Claim("role", user.VaiTro),
                new Claim("name", user.HoTen),
                new Claim(Passwords.ClaimDauVan, Passwords.DauVan(user.MatKhau)),
            },
            expires: hetHanUtc,
            signingCredentials: new SigningCredentials(opt.Key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), GioViet.TuUtc(hetHanUtc));
    }
}

/// <summary>Băm mật khẩu bằng BCrypt (đúng như ghi chú cột NguoiDung.MatKhau).</summary>
public static class Passwords
{
    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    public static bool Verify(string password, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch (BCrypt.Net.SaltParseException) { return false; } // hash không hợp lệ trong DB
    }

    /// <summary>Tên claim chứa dấu vân mật khẩu trong JWT.</summary>
    public const string ClaimDauVan = "pv";

    /// <summary>
    /// Dấu vân của mật khẩu hiện tại (16 ký tự đầu SHA-256 của chuỗi băm, không lộ chuỗi băm BCrypt).
    /// Ghi vào token lúc đăng nhập; đổi / đặt lại mật khẩu thì dấu vân đổi → token cũ bị từ chối (Program.cs OnTokenValidated).
    /// </summary>
    public static string DauVan(string matKhauDaBam) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(matKhauDaBam)))[..16];
}

public static class ClaimsExtensions
{
    public static int? MaNguoiDung(this ClaimsPrincipal p) =>
        int.TryParse(p.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public static bool LaQuanLy(this ClaimsPrincipal p) => p.IsInRole(VaiTros.QuanLy);
}
