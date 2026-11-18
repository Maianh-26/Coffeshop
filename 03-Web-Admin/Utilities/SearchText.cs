using System.Globalization;
using System.Text;

namespace CoffeeShop.Admin.Utilities;

/// <summary>
/// Tìm kiếm tiếng Việt không phân biệt dấu và hoa thường: "ba" khớp "Bạc xỉu", "ca phe" khớp "Cà phê",
/// "dao" khớp "Trà đào". Cả chuỗi gõ và chuỗi được tìm đều được "bỏ dấu" trước khi so khớp.
/// Không dùng string.Normalize (phụ thuộc ICU trên WebAssembly): tự đổi chữ dựng sẵn bằng bảng
/// và bỏ dấu rời (U+0300…) — gõ bằng bộ gõ kiểu "Unicode tổ hợp" vẫn khớp.
/// </summary>
public static class SearchText
{
    private static readonly Dictionary<char, char> Plain = Build(
        ("àáạảãâầấậẩẫăằắặẳẵ", 'a'),
        ("èéẹẻẽêềếệểễ", 'e'),
        ("ìíịỉĩ", 'i'),
        ("òóọỏõôồốộổỗơờớợởỡ", 'o'),
        ("ùúụủũưừứựửữ", 'u'),
        ("ỳýỵỷỹ", 'y'),
        ("đ", 'd'));

    private static Dictionary<char, char> Build(params (string Accented, char Base)[] groups)
    {
        var map = new Dictionary<char, char>();
        foreach (var (accented, plain) in groups)
        {
            foreach (char c in accented) map[c] = plain;
        }

        return map;
    }

    /// <summary>Chữ thường, bỏ dấu, gộp nhiều khoảng trắng thành một, bỏ khoảng trắng hai đầu.</summary>
    public static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var sb = new StringBuilder(text.Length);
        bool space = false;

        foreach (char raw in text.ToLowerInvariant())
        {
            // Dấu rời (bộ gõ tổ hợp: "a" + dấu nặng) → bỏ
            if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsWhiteSpace(raw))
            {
                space = sb.Length > 0;
                continue;
            }

            if (space)
            {
                sb.Append(' ');
                space = false;
            }

            sb.Append(Plain.TryGetValue(raw, out char plain) ? plain : raw);
        }

        return sb.ToString();
    }

    /// <summary>
    /// <paramref name="text"/> có chứa <paramref name="foldedTerm"/> không. Truyền chuỗi tìm đã qua
    /// <see cref="Fold"/> (tính một lần cho cả danh sách); chuỗi rỗng khớp mọi thứ.
    /// </summary>
    public static bool Contains(string? text, string foldedTerm) =>
        foldedTerm.Length == 0 || Fold(text).Contains(foldedTerm, StringComparison.Ordinal);
}
