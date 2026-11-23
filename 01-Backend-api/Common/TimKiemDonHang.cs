using System.Linq.Expressions;
using CoffeeShop.Api.Data;

namespace CoffeeShop.Api.Common;

/// <summary>
/// Điều kiện của ô tìm kiếm đơn hàng: mã đơn BẮT ĐẦU BẰNG chuỗi số, HOẶC số nhận món BẰNG ĐÚNG số đó.
///
/// Không dùng <c>MaDonHang.ToString().StartsWith(...)</c>: SQL phải đổi từng mã sang chuỗi nên quét cả bảng
/// (đo trên 1 triệu đơn: 2–13 giây). Mã đơn là số nguyên nên "bắt đầu bằng 52" tương đương các KHOẢNG
/// [52, 52], [520, 529], [5200, 5299], … — mỗi khoảng là một lần tìm trên khóa chính (vài trang đọc).
/// int tối đa 10 chữ số → chuỗi n chữ số cho tối đa 11 − n khoảng; luôn dựng đủ <see cref="SoKhoang"/>
/// khoảng (khoảng thừa = rỗng) để câu SQL cùng một dạng, chỉ khác tham số → SQL Server dùng lại kế hoạch.
/// </summary>
public static class TimKiemDonHang
{
    public const int SoKhoang = 10;

    public static Expression<Func<DonHang, bool>> DieuKien(string chuoiSo)
    {
        var (tu, den) = KhoangMaDon(chuoiSo);
        Expression<Func<DonHang, bool>> theoMa = d =>
            (d.MaDonHang >= tu[0] && d.MaDonHang <= den[0]) || (d.MaDonHang >= tu[1] && d.MaDonHang <= den[1]) ||
            (d.MaDonHang >= tu[2] && d.MaDonHang <= den[2]) || (d.MaDonHang >= tu[3] && d.MaDonHang <= den[3]) ||
            (d.MaDonHang >= tu[4] && d.MaDonHang <= den[4]) || (d.MaDonHang >= tu[5] && d.MaDonHang <= den[5]) ||
            (d.MaDonHang >= tu[6] && d.MaDonHang <= den[6]) || (d.MaDonHang >= tu[7] && d.MaDonHang <= den[7]) ||
            (d.MaDonHang >= tu[8] && d.MaDonHang <= den[8]) || (d.MaDonHang >= tu[9] && d.MaDonHang <= den[9]);

        // Số nhận món chỉ có 1–50 (CHECK trong DB) → ngoài khoảng đó không thêm điều kiện, để SQL khỏi xét cột này
        if (!int.TryParse(chuoiSo, out var so) || so < 1 || so > 50) return theoMa;
        Expression<Func<DonHang, bool>> theoSo = d => d.SoNhanMon == so;
        var p = theoMa.Parameters[0];
        var than = Expression.OrElse(theoMa.Body, new DoiThamSo(theoSo.Parameters[0], p).Visit(theoSo.Body));
        return Expression.Lambda<Func<DonHang, bool>>(than, p);
    }

    /// <summary>Các khoảng mã đơn có dạng thập phân bắt đầu bằng <paramref name="chuoiSo"/>; khoảng rỗng = [1, 0].</summary>
    public static (int[] Tu, int[] Den) KhoangMaDon(string chuoiSo)
    {
        var tu = Enumerable.Repeat(1, SoKhoang).ToArray();
        var den = new int[SoKhoang];
        // Mã đơn ≥ 1 không có số 0 đứng đầu → chuỗi bắt đầu bằng "0" không khớp mã nào
        if (chuoiSo.Length == 0 || chuoiSo[0] == '0' || !long.TryParse(chuoiSo, out var n)) return (tu, den);

        long boi = 1;
        for (var k = 0; k < SoKhoang && n * boi <= int.MaxValue; k++, boi *= 10)
        {
            tu[k] = (int)(n * boi);
            den[k] = (int)Math.Min((n + 1) * boi - 1, int.MaxValue);
        }
        return (tu, den);
    }

    private sealed class DoiThamSo(ParameterExpression cu, ParameterExpression moi) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == cu ? moi : base.VisitParameter(node);
    }
}
