using CoffeeShop.Api.Common;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Services;

public class MenuService(AppDbContext db)
{
    /// <summary>Truy vấn món kèm đủ dữ liệu để tính giá khuyến mãi & tồn kho.</summary>
    public IQueryable<Mon> MonDayDu() => db.Mons
        .Include(m => m.DanhMuc)
        .Include(m => m.KhuyenMai)
        .Include(m => m.ChiTietMons).ThenInclude(c => c.KichThuoc)
        .Include(m => m.ChiTietMons).ThenInclude(c => c.CongThucs).ThenInclude(ct => ct.NguyenLieu)
        .AsSplitQuery();

    public async Task<Mon> TimMon(int id) =>
        await MonDayDu().FirstOrDefaultAsync(m => m.MaTenMon == id) ?? throw ApiException.NotFound("Món");

    public async Task<MonDto> LayMonDto(int id) => (await TimMon(id)).ToDto(DateTime.Now);

    /// <summary>Số lượng đã bán theo MaTenMon (đơn chưa hủy) — dùng cho sắp xếp "bán chạy" và gợi ý.</summary>
    public Task<Dictionary<int, int>> SoLuongDaBan(DateTime? tu = null)
    {
        var q = from ct in db.ChiTietDonHangs
                join d in db.DonHangs on ct.MaDonHang equals d.MaDonHang
                join cm in db.ChiTietMons on ct.MaChiTietMon equals cm.MaChiTietMon
                where d.TrangThaiThanhToan != TrangThais.Cancelled && (tu == null || d.ThoiGianTao >= tu)
                select new { cm.MaTenMon, ct.SoLuong };
        return q.GroupBy(x => x.MaTenMon).Select(g => new { g.Key, Sl = g.Sum(x => x.SoLuong) })
            .ToDictionaryAsync(x => x.Key, x => x.Sl);
    }
}
