using CoffeeShop.Admin.Utilities;

namespace CoffeeShop.Admin.Tests;

/// <summary>Sắp xếp theo cột trên các trang danh sách.</summary>
public class ColumnSortTests
{
    private static readonly int[] Items = [3, 1, 2];

    private static IEnumerable<int> Apply(ColumnSort sort) => sort.Apply(Items, ("so", x => x));

    [Fact]
    public void MacDinh_GiuThuTuMayChu()
    {
        var sort = new ColumnSort();

        Assert.False(sort.Active);
        Assert.Equal(Items, Apply(sort));
        Assert.Null(sort.AriaSort("so"));
    }

    [Fact]
    public void BamCotMoi_ThapDenCao_BamLai_CaoDenThap()
    {
        var sort = new ColumnSort();

        sort.Toggle("so");
        Assert.Equal([1, 2, 3], Apply(sort));
        Assert.Equal("ascending", sort.AriaSort("so"));

        sort.Toggle("so");
        Assert.Equal([3, 2, 1], Apply(sort));
        Assert.Equal("desc", sort.Direction("so"));
    }

    [Fact]
    public void DoiSangCotKhac_BatDauLaiTuThapDenCao()
    {
        var sort = new ColumnSort();
        sort.Toggle("a");
        sort.Toggle("a");

        sort.Toggle("b");

        Assert.Equal("b", sort.Key);
        Assert.False(sort.Descending);
    }

    [Fact]
    public void Reset_VeThuTuMacDinh()
    {
        var sort = new ColumnSort();
        sort.Toggle("so");

        sort.Reset();

        Assert.False(sort.Active);
        Assert.Equal(Items, Apply(sort));
    }

    [Fact]
    public void CotKhongCoTrongDanhSach_GiuNguyenThuTu()
    {
        var sort = new ColumnSort();
        sort.Toggle("khac");

        Assert.Equal(Items, Apply(sort));
    }
}
