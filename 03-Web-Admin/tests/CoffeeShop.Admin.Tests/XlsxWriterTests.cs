using System.IO.Compression;
using CoffeeShop.Admin.Utilities;

namespace CoffeeShop.Admin.Tests;

/// <summary>Bộ ghi file Excel (.xlsx) dùng cho nút "Xuất Excel" ở trang Tổng quan.</summary>
public class XlsxWriterTests
{
    private static string ReadEntry(byte[] xlsx, string path)
    {
        using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        var entry = zip.GetEntry(path) ?? throw new InvalidOperationException($"Thiếu {path}");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData(1, 1, "A1")]
    [InlineData(26, 3, "Z3")]
    [InlineData(27, 10, "AA10")]
    public void Ref_DoiCotDongSangTenOExcel(int col, int row, string expected)
    {
        Assert.Equal(expected, XlsxSheet.Ref(col, row));
    }

    [Fact]
    public void ToBytes_TaoGoiZipCoDuCacPhanBatBuoc()
    {
        var writer = new XlsxWriter();
        writer.AddSheet("Tổng hợp").Row(XlsxCell.Str("Doanh thu"), XlsxCell.Num(150_000));

        byte[] bytes = writer.ToBytes();

        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var names = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("[Content_Types].xml", names);
        Assert.Contains("xl/workbook.xml", names);
        Assert.Contains("xl/worksheets/sheet1.xml", names);
        Assert.Contains("Doanh thu", ReadEntry(bytes, "xl/worksheets/sheet1.xml"));
    }

    [Fact]
    public void AddSheet_BoKyTuCam_CatDai31_VaKhongTrungTen()
    {
        var writer = new XlsxWriter();

        var a = writer.AddSheet("Món/bán*chạy");
        var b = writer.AddSheet("Món bán chạy");
        var c = writer.AddSheet("MÓN BÁN CHẠY");
        var d = writer.AddSheet(new string('x', 40));

        Assert.Equal("Mónbánchạy", a.Name);
        Assert.Equal("Món bán chạy", b.Name);
        Assert.Equal("MÓN BÁN CHẠY (2)", c.Name);   // trùng không phân biệt hoa thường
        Assert.Equal(31, d.Name.Length);
    }

    [Fact]
    public void ToBytes_KhongCoTrangNao_VanTaoFileHopLe()
    {
        byte[] bytes = new XlsxWriter().ToBytes();

        Assert.Contains("Sheet1", ReadEntry(bytes, "xl/workbook.xml"));
    }
}
