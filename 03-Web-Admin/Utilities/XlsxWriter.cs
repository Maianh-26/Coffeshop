using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace CoffeeShop.Admin.Utilities;

/// <summary>
/// Dựng file Excel .xlsx (Office Open XML) tối giản, không cần thư viện ngoài — chạy được trong Blazor WebAssembly.
/// Hỗ trợ: nhiều trang tính, chữ / số / công thức, vài kiểu định dạng cố định (<see cref="XlsxStyle"/>),
/// độ rộng cột, gộp ô, cố định dòng tiêu đề. Số được ghi là số thật để Excel cộng / lọc / vẽ biểu đồ được.
/// </summary>
public sealed class XlsxWriter
{
    private readonly List<XlsxSheet> _sheets = [];

    public XlsxSheet AddSheet(string name)
    {
        var sheet = new XlsxSheet(SheetName(name, _sheets.Count + 1));
        _sheets.Add(sheet);
        return sheet;
    }

    public byte[] ToBytes()
    {
        if (_sheets.Count == 0) AddSheet("Sheet1");

        try
        {
            return Pack(CompressionLevel.Optimal);
        }
        catch (PlatformNotSupportedException)
        {
            // Môi trường không có nén Deflate → ghi không nén (file lớn hơn nhưng vẫn hợp lệ)
            return Pack(CompressionLevel.NoCompression);
        }
    }

    private byte[] Pack(CompressionLevel level)
    {
        using var stream = new MemoryStream();

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", ContentTypes(), level);
            Add(zip, "_rels/.rels", RootRels, level);
            Add(zip, "xl/workbook.xml", Workbook(), level);
            Add(zip, "xl/_rels/workbook.xml.rels", WorkbookRels(), level);
            Add(zip, "xl/styles.xml", Styles, level);

            for (int i = 0; i < _sheets.Count; i++)
                Add(zip, $"xl/worksheets/sheet{i + 1}.xml", _sheets[i].ToXml(), level);
        }

        return stream.ToArray();
    }

    private static void Add(ZipArchive zip, string path, string xml, CompressionLevel level)
    {
        var entry = zip.CreateEntry(path, level);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(xml);
    }

    /// <summary>Tên trang tính hợp lệ: ≤ 31 ký tự, không có : \ / ? * [ ] và không trùng.</summary>
    private string SheetName(string name, int index)
    {
        var clean = new string(name.Where(c => c is not (':' or '\\' or '/' or '?' or '*' or '[' or ']')).ToArray()).Trim();
        if (clean.Length == 0) clean = $"Sheet{index}";
        if (clean.Length > 31) clean = clean[..31];

        string unique = clean;
        for (int n = 2; _sheets.Any(s => s.Name.Equals(unique, StringComparison.OrdinalIgnoreCase)); n++)
        {
            string suffix = $" ({n})";
            unique = clean[..Math.Min(clean.Length, 31 - suffix.Length)] + suffix;
        }

        return unique;
    }

    private string ContentTypes()
    {
        var sb = new StringBuilder(
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""");

        for (int i = 1; i <= _sheets.Count; i++)
            sb.Append($"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");

        return sb.Append("</Types>").ToString();
    }

    private const string RootRels =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";

    private string Workbook()
    {
        var sb = new StringBuilder(
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><bookViews><workbookView/></bookViews><sheets>""");

        for (int i = 0; i < _sheets.Count; i++)
            sb.Append($"""<sheet name="{Xml(_sheets[i].Name)}" sheetId="{i + 1}" r:id="rId{i + 1}"/>""");

        // Excel tính lại công thức (dòng Tổng) khi mở file
        return sb.Append("""</sheets><calcPr calcId="191029" fullCalcOnLoad="1"/></workbook>""").ToString();
    }

    private string WorkbookRels()
    {
        var sb = new StringBuilder(
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");

        for (int i = 1; i <= _sheets.Count; i++)
            sb.Append($"""<Relationship Id="rId{i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i}.xml"/>""");

        int stylesId = _sheets.Count + 1;
        sb.Append($"""<Relationship Id="rId{stylesId}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""");

        return sb.Append("</Relationships>").ToString();
    }

    // Thứ tự cellXfs phải khớp enum XlsxStyle.
    // numFmt tự định nghĩa: 164 = "#,##0 đ" (tiền), 165 = "dd/mm/yyyy" (ngày), 166 = "mm/yyyy" (tháng), 167 = "0.0%" (tỷ trọng);
    // 3 = "#,##0" (số nguyên có phân cách) là mã có sẵn của Excel.
    private const string Styles =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""" +
        """<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""" +
        """<numFmts count="4"><numFmt numFmtId="164" formatCode="#,##0 &quot;đ&quot;"/><numFmt numFmtId="165" formatCode="dd/mm/yyyy"/><numFmt numFmtId="166" formatCode="mm/yyyy"/><numFmt numFmtId="167" formatCode="0.0%"/></numFmts>""" +
        """<fonts count="5">""" +
        """<font><sz val="11"/><name val="Calibri"/><family val="2"/></font>""" +
        """<font><b/><sz val="11"/><name val="Calibri"/><family val="2"/></font>""" +
        """<font><b/><sz val="15"/><color rgb="FF5B3A29"/><name val="Calibri"/><family val="2"/></font>""" +
        """<font><i/><sz val="10"/><color rgb="FF7A6A60"/><name val="Calibri"/><family val="2"/></font>""" +
        """<font><b/><sz val="12"/><color rgb="FF5B3A29"/><name val="Calibri"/><family val="2"/></font>""" +
        """</fonts>""" +
        """<fills count="4">""" +
        """<fill><patternFill patternType="none"/></fill>""" +
        """<fill><patternFill patternType="gray125"/></fill>""" +
        """<fill><patternFill patternType="solid"><fgColor rgb="FFF1E4D6"/><bgColor indexed="64"/></patternFill></fill>""" +
        """<fill><patternFill patternType="solid"><fgColor rgb="FFFAF5EF"/><bgColor indexed="64"/></patternFill></fill>""" +
        """</fills>""" +
        """<borders count="2">""" +
        """<border><left/><right/><top/><bottom/><diagonal/></border>""" +
        """<border><left style="thin"><color rgb="FFD9CBBE"/></left><right style="thin"><color rgb="FFD9CBBE"/></right><top style="thin"><color rgb="FFD9CBBE"/></top><bottom style="thin"><color rgb="FFD9CBBE"/></bottom><diagonal/></border>""" +
        """</borders>""" +
        """<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>""" +
        """<cellXfs count="15">""" +
        // 0 Default
        """<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>""" +
        // 1 Title
        """<xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1"/>""" +
        // 2 Note
        """<xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyFont="1"/>""" +
        // 3 Header
        """<xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf>""" +
        // 4 Text
        """<xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1"/>""" +
        // 5 Integer
        """<xf numFmtId="3" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1"/>""" +
        // 6 Money
        """<xf numFmtId="164" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1"/>""" +
        // 7 TotalText
        """<xf numFmtId="0" fontId="1" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1"/>""" +
        // 8 TotalInteger
        """<xf numFmtId="3" fontId="1" fillId="3" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1"/>""" +
        // 9 TotalMoney
        """<xf numFmtId="164" fontId="1" fillId="3" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1"/>""" +
        // 10 Section
        """<xf numFmtId="0" fontId="4" fillId="0" borderId="0" xfId="0" applyFont="1"/>""" +
        // 11 Date
        """<xf numFmtId="165" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left"/></xf>""" +
        // 12 Month
        """<xf numFmtId="166" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left"/></xf>""" +
        // 13 Percent
        """<xf numFmtId="167" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1"/>""" +
        // 14 TotalPercent
        """<xf numFmtId="167" fontId="1" fillId="3" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1"/>""" +
        """</cellXfs>""" +
        """<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>""" +
        """</styleSheet>""";

    /// <summary>Thoát ký tự XML và bỏ ký tự điều khiển (không hợp lệ trong XML 1.0).</summary>
    internal static string Xml(string value)
    {
        var sb = new StringBuilder(value.Length);

        foreach (char c in value)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\t' or '\n' or '\r': sb.Append(c); break;
                case < ' ' or '\uFFFE' or '\uFFFF': break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }
}

/// <summary>Kiểu ô có sẵn trong file (khớp thứ tự cellXfs trong styles.xml).</summary>
public enum XlsxStyle
{
    Default = 0,
    Title = 1,
    Note = 2,
    Header = 3,
    Text = 4,
    Integer = 5,
    Money = 6,
    TotalText = 7,
    TotalInteger = 8,
    TotalMoney = 9,
    Section = 10,
    Date = 11,
    Month = 12,
    Percent = 13,
    TotalPercent = 14
}

/// <summary>Một ô: chữ, số hoặc công thức (kèm giá trị tính sẵn để trình xem file không tính công thức vẫn thấy số).</summary>
public readonly record struct XlsxCell(string? Text, decimal? Number, string? Formula, XlsxStyle Style)
{
    public static XlsxCell Str(string text, XlsxStyle style = XlsxStyle.Text) => new(text, null, null, style);

    public static XlsxCell Num(decimal value, XlsxStyle style = XlsxStyle.Integer) => new(null, value, null, style);

    public static XlsxCell Sum(string range, decimal cached, XlsxStyle style) => new(null, cached, $"SUM({range})", style);

    /// <summary>Công thức bất kỳ (không có dấu "=" đầu) kèm giá trị tính sẵn.</summary>
    public static XlsxCell Calc(string formula, decimal cached, XlsxStyle style) => new(null, cached, formula, style);

    /// <summary>Ô ngày thật của Excel (số ngày tính từ 30/12/1899) — lọc / sắp xếp theo ngày được.</summary>
    public static XlsxCell Day(DateTime value, XlsxStyle style = XlsxStyle.Date) =>
        new(null, (decimal)value.Date.ToOADate(), null, style);

    public static XlsxCell Empty(XlsxStyle style = XlsxStyle.Default) => new(null, null, null, style);
}

public sealed class XlsxSheet
{
    private readonly List<XlsxCell[]> _rows = [];
    private readonly List<string> _merges = [];
    private double[] _widths = [];
    private int _frozenRows;

    internal XlsxSheet(string name) => Name = name;

    public string Name { get; }

    /// <summary>Số dòng đã ghi (dòng tiếp theo = RowCount + 1, đánh số từ 1 như Excel).</summary>
    public int RowCount => _rows.Count;

    public XlsxSheet Row(params XlsxCell[] cells)
    {
        _rows.Add(cells);
        return this;
    }

    public XlsxSheet BlankRow() => Row();

    /// <summary>Gộp ô trên dòng vừa ghi, từ cột <paramref name="fromCol"/> tới <paramref name="toCol"/> (đánh số từ 1).</summary>
    public XlsxSheet MergeLastRow(int fromCol, int toCol)
    {
        int row = _rows.Count;
        _merges.Add($"{Ref(fromCol, row)}:{Ref(toCol, row)}");
        return this;
    }

    /// <summary>Độ rộng cột (đơn vị ký tự của Excel).</summary>
    public XlsxSheet Widths(params double[] widths)
    {
        _widths = widths;
        return this;
    }

    /// <summary>Cố định các dòng phía trên dòng <paramref name="rows"/> + 1 khi cuộn.</summary>
    public XlsxSheet FreezeRows(int rows)
    {
        _frozenRows = rows;
        return this;
    }

    /// <summary>Tham chiếu ô kiểu Excel: cột 1 dòng 1 → "A1".</summary>
    public static string Ref(int col, int row) => ColumnName(col) + row.ToString(CultureInfo.InvariantCulture);

    private static string ColumnName(int col)
    {
        string name = "";
        for (; col > 0; col = (col - 1) / 26)
            name = (char)('A' + (col - 1) % 26) + name;
        return name;
    }

    internal string ToXml()
    {
        var sb = new StringBuilder(
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""");

        if (_frozenRows > 0)
        {
            string topLeft = Ref(1, _frozenRows + 1);
            sb.Append($"""<sheetViews><sheetView workbookViewId="0"><pane ySplit="{_frozenRows}" topLeftCell="{topLeft}" activePane="bottomLeft" state="frozen"/><selection pane="bottomLeft" activeCell="{topLeft}" sqref="{topLeft}"/></sheetView></sheetViews>""");
        }
        else
        {
            sb.Append("""<sheetViews><sheetView workbookViewId="0"/></sheetViews>""");
        }

        if (_widths.Length > 0)
        {
            sb.Append("<cols>");
            for (int i = 0; i < _widths.Length; i++)
            {
                string w = _widths[i].ToString("0.##", CultureInfo.InvariantCulture);
                sb.Append($"""<col min="{i + 1}" max="{i + 1}" width="{w}" customWidth="1"/>""");
            }
            sb.Append("</cols>");
        }

        sb.Append("<sheetData>");

        for (int r = 0; r < _rows.Count; r++)
        {
            int rowNumber = r + 1;
            sb.Append($"""<row r="{rowNumber}">""");

            for (int c = 0; c < _rows[r].Length; c++)
                AppendCell(sb, _rows[r][c], Ref(c + 1, rowNumber));

            sb.Append("</row>");
        }

        sb.Append("</sheetData>");

        if (_merges.Count > 0)
        {
            sb.Append($"""<mergeCells count="{_merges.Count}">""");
            foreach (string m in _merges) sb.Append($"""<mergeCell ref="{m}"/>""");
            sb.Append("</mergeCells>");
        }

        sb.Append("""<pageMargins left="0.5" right="0.5" top="0.6" bottom="0.6" header="0.3" footer="0.3"/>""");

        return sb.Append("</worksheet>").ToString();
    }

    private static void AppendCell(StringBuilder sb, XlsxCell cell, string reference)
    {
        int s = (int)cell.Style;

        if (cell.Formula is not null)
        {
            string cached = (cell.Number ?? 0).ToString(CultureInfo.InvariantCulture);
            sb.Append($"""<c r="{reference}" s="{s}"><f>{XlsxWriter.Xml(cell.Formula)}</f><v>{cached}</v></c>""");
        }
        else if (cell.Number is { } number)
        {
            sb.Append($"""<c r="{reference}" s="{s}"><v>{number.ToString(CultureInfo.InvariantCulture)}</v></c>""");
        }
        else if (cell.Text is { } text)
        {
            sb.Append($"""<c r="{reference}" s="{s}" t="inlineStr"><is><t xml:space="preserve">{XlsxWriter.Xml(text)}</t></is></c>""");
        }
        else
        {
            sb.Append($"""<c r="{reference}" s="{s}"/>""");
        }
    }
}
