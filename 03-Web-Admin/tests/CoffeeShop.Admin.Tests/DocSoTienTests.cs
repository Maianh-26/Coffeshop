using CoffeeShop.Admin.Utilities;

namespace CoffeeShop.Admin.Tests;

/// <summary>Đọc số tiền thành chữ cho dòng "bằng chữ" trên hóa đơn.</summary>
public class DocSoTienTests
{
    [Theory]
    [InlineData(0, "Không đồng")]
    [InlineData(15_000, "Mười lăm nghìn đồng")]
    [InlineData(21_000, "Hai mươi mốt nghìn đồng")]
    [InlineData(24_000, "Hai mươi tư nghìn đồng")]
    [InlineData(105_000, "Một trăm linh năm nghìn đồng")]
    [InlineData(480_000, "Bốn trăm tám mươi nghìn đồng")]
    [InlineData(1_250_500, "Một triệu hai trăm năm mươi nghìn năm trăm đồng")]
    [InlineData(1_005_000, "Một triệu không trăm linh năm nghìn đồng")]
    [InlineData(2_000_000_000, "Hai tỷ đồng")]
    [InlineData(-25_000, "Âm hai mươi lăm nghìn đồng")]
    public void Doc_DungQuyTacDocSoTiengViet(long soTien, string expected)
    {
        Assert.Equal(expected, DocSoTien.Doc(soTien));
    }

    [Fact]
    public void Doc_LamTronToiDong()
    {
        Assert.Equal("Mười nghìn không trăm linh một đồng", DocSoTien.Doc(10_000.5m));
    }
}
