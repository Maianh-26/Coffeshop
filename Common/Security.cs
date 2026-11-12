using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
        var expires = DateTime.Now.AddHours(opt.ExpiresInHours);
        var token = new JwtSecurityToken(
            issuer: opt.Issuer,
            audience: opt.Issuer,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.MaNguoiDung.ToString()),
                new Claim("role", user.VaiTro),
                new Claim("name", user.HoTen),
            },
            expires: expires.ToUniversalTime(),
            signingCredentials: new SigningCredentials(opt.Key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
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
}

public static class ClaimsExtensions
{
    public static int? MaNguoiDung(this ClaimsPrincipal p) =>
        int.TryParse(p.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public static bool LaQuanLy(this ClaimsPrincipal p) => p.IsInRole(VaiTros.QuanLy);
}
