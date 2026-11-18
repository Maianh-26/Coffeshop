using CoffeeShop.Api.Common;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Services;

public class KhoService(AppDbContext db)
{
    private const int SoNgayThongKe = 7;
    private const decimal NguongNgayConLai = 2; // còn ≤ 2 ngày dùng → coi như sắp hết

    /// <summary>Danh sách nguyên liệu kèm dự báo số ngày còn dùng được (dựa trên LichSuKho 7 ngày gần nhất).</summary>
    public async Task<List<NguyenLieuDto>> DanhSach(bool? sapHet, bool? dangSuDung)
    {
        var tu = DateTime.Now.AddDays(-SoNgayThongKe);
        var tieuHao = await db.LichSuKhos
            .Where(l => l.LoaiGiaoDich == LoaiGiaoDichs.TieuHaoTheoDon && l.ThoiGian >= tu)
            .GroupBy(l => l.MaNguyenLieu)
            .Select(g => new { g.Key, Tong = -g.Sum(l => l.SoLuongThayDoi) })
            .ToDictionaryAsync(x => x.Key, x => x.Tong);

        var q = db.NguyenLieus.AsQueryable();
        if (dangSuDung is { } d) q = q.Where(n => n.DangSuDung == d);

        var list = (await q.OrderBy(n => n.TenNguyenLieu).ToListAsync()).Select(n =>
        {
            var tbNgay = Math.Round(tieuHao.GetValueOrDefault(n.MaNguyenLieu) / SoNgayThongKe, 2);
            decimal? conLai = tbNgay > 0 ? Math.Round(n.SoLuongTon / tbNgay, 1) : null;
            var sap = n.SoLuongTon <= n.MucCanhBaoToiThieu || conLai <= NguongNgayConLai;
            return new NguyenLieuDto(n.MaNguyenLieu, n.TenNguyenLieu, n.DonViTinh, n.SoLuongTon,
                n.MucCanhBaoToiThieu, n.DangSuDung, sap, tbNgay, conLai);
        });
        if (sapHet is { } s) list = list.Where(n => n.SapHet == s);
        return list.ToList();
    }

    public async Task<NguyenLieuDto> Mot(int id) =>
        (await DanhSach(null, null)).FirstOrDefault(n => n.MaNguyenLieu == id) ?? throw ApiException.NotFound("Nguyên liệu");

    /// <summary>Nhập hàng (+) hoặc xuất hủy (-), luôn ghi LichSuKho.</summary>
    public async Task<NguyenLieuDto> GiaoDich(int id, string loai, decimal soLuong, string? ghiChu, int? maNguoiDung)
    {
        var nl = await db.NguyenLieus.FindAsync(id) ?? throw ApiException.NotFound("Nguyên liệu");
        var thayDoi = loai == LoaiGiaoDichs.NhapHang ? soLuong : -soLuong;
        if (nl.SoLuongTon + thayDoi < 0)
            throw ApiException.Conflict($"Chỉ còn {nl.SoLuongTon} {nl.DonViTinh}, không thể xuất hủy {soLuong}");

        await using var tx = await db.Database.BeginTransactionAsync();
        nl.SoLuongTon += thayDoi;
        db.LichSuKhos.Add(new LichSuKho
        {
            MaNguyenLieu = id, MaNguoiDung = maNguoiDung, LoaiGiaoDich = loai,
            SoLuongThayDoi = thayDoi, ThoiGian = DateTime.Now, GhiChu = ghiChu,
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return await Mot(id);
    }
}
