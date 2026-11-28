using CoffeeShop.Admin.Utilities;

namespace CoffeeShop.Admin.Tests;

/// <summary>Tìm kiếm tiếng Việt không phân biệt dấu, hoa thường.</summary>
public class SearchTextTests
{
    [Theory]
    [InlineData("Cà Phê Sữa", "ca phe sua")]
    [InlineData("  Trà   ĐÀO  ", "tra dao")]
    [InlineData("Bạc xỉu", "bac xiu")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Fold_BoDau_ChuThuong_GopKhoangTrang(string? input, string expected)
    {
        Assert.Equal(expected, SearchText.Fold(input));
    }

    [Fact]
    public void Fold_BoDauRoi_CuaBoGoToHop()
    {
        // Viết bằng mã thoát để editor không tự gộp thành chữ dựng sẵn: "a" + dấu huyền rời U+0300, "e" + dấu mũ rời U+0302
        Assert.Equal("ca phe", SearchText.Fold("Ca\u0300 phe\u0302"));
    }

    [Theory]
    [InlineData("Cà phê sữa đá", "phe sua", true)]
    [InlineData("Trà đào", "dao", true)]
    [InlineData("Trà đào", "sua", false)]
    [InlineData("Bất kỳ", "", true)]   // chuỗi tìm rỗng khớp mọi thứ
    public void Contains_SoKhopTrenChuoiDaBoDau(string text, string foldedTerm, bool expected)
    {
        Assert.Equal(expected, SearchText.Contains(text, foldedTerm));
    }
}
