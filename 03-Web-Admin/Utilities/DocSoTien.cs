namespace CoffeeShop.Admin.Utilities;

/// <summary>
/// Đọc số tiền thành chữ tiếng Việt cho dòng "Bằng chữ" trên hóa đơn,
/// ví dụ 480000 → "Bốn trăm tám mươi nghìn đồng", 1250500 → "Một triệu hai trăm năm mươi nghìn năm trăm đồng".
/// Quy tắc: "mười / mươi", "mốt" (21, 31…), "lăm" (15, 25…), "tư" (24, 34…), "linh" (105), "không trăm" ở nhóm giữa;
/// phần từ hàng tỷ trở lên đọc như một số riêng rồi thêm "tỷ" (1.000.500.000.000 → "một nghìn không trăm linh năm… tỷ").
/// Số tiền làm tròn tới đồng; số âm có chữ "Âm" phía trước.
/// </summary>
public static class DocSoTien
{
    private static readonly string[] ChuSo =
        ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];

    private const long MotTy = 1_000_000_000;

    public static string Doc(decimal soTien)
    {
        decimal lamTron = Math.Round(soTien, MidpointRounding.AwayFromZero);
        if (lamTron == 0) return "Không đồng";

        // Tiền quán không bao giờ vượt long; chặn để không ném OverflowException
        long so = (long)Math.Min(Math.Abs(lamTron), long.MaxValue);
        string chu = DocSo(so, docDu: false) + " đồng";
        if (lamTron < 0) chu = "âm " + chu;

        return char.ToUpper(chu[0]) + chu[1..];
    }

    /// <summary>Đọc số nguyên dương (không kèm "đồng").</summary>
    /// <param name="docDu">true khi đứng sau một phần lớn hơn → đọc đủ "không trăm", "linh".</param>
    private static string DocSo(long so, bool docDu)
    {
        var phan = new List<string>();

        // Phần tỷ: đọc như một số riêng rồi thêm "tỷ" một lần (tránh "một nghìn tỷ năm trăm tỷ")
        long ty = so / MotTy;
        long duoiTy = so % MotTy;
        if (ty > 0)
        {
            phan.Add(DocSo(ty, docDu) + " tỷ");
            docDu = true;
        }

        // Dưới tỷ: 3 nhóm triệu / nghìn / đơn vị
        (long GiaTri, string Hang)[] nhom =
        [
            (duoiTy / 1_000_000, " triệu"),
            (duoiTy / 1_000 % 1_000, " nghìn"),
            (duoiTy % 1_000, "")
        ];

        foreach (var (giaTri, hang) in nhom)
        {
            if (giaTri == 0) continue;
            phan.Add(DocNhom((int)giaTri, docDu) + hang);
            docDu = true;   // các nhóm sau nhóm đầu tiên luôn đọc đủ
        }

        return string.Join(" ", phan);
    }

    /// <summary>Đọc một nhóm 1–999.</summary>
    private static string DocNhom(int n, bool docDu)
    {
        int tram = n / 100, chuc = n / 10 % 10, donVi = n % 10;
        var tu = new List<string>();

        if (tram > 0 || docDu)
        {
            tu.Add(ChuSo[tram] + " trăm");
        }

        if (chuc == 0)
        {
            if (donVi > 0 && (tram > 0 || docDu)) tu.Add("linh");
        }
        else if (chuc == 1)
        {
            tu.Add("mười");
        }
        else
        {
            tu.Add(ChuSo[chuc] + " mươi");
        }

        if (donVi > 0)
        {
            tu.Add(donVi switch
            {
                1 when chuc >= 2 => "mốt",
                4 when chuc >= 2 => "tư",
                5 when chuc >= 1 => "lăm",
                _ => ChuSo[donVi]
            });
        }

        return string.Join(" ", tu);
    }
}
