using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoffeeShop.Admin.Models;
using CoffeeShop.Admin.Utilities;
using static CoffeeShop.Admin.Services.Mock.MockDatabase;

namespace CoffeeShop.Admin.Services.Mock;

/// <summary>
/// Chặn mọi request "api/..." của HttpClient và trả lời bằng <see cref="MockDatabase"/>,
/// theo đúng hợp đồng API của backend (CoffeeShop.Api, tiền tố api/v1, openapi.yaml):
/// cùng route, cùng JSON, cùng phân quyền (QuanLy / NhanVien / công khai),
/// cùng định dạng lỗi { error: { code, message, details } } và mã HTTP.
/// Các service và trang không biết mình đang chạy với dữ liệu giả.
/// Bật/tắt bằng "Api:UseMock" trong wwwroot/appsettings.json.
/// Riêng quên mật khẩu (api/auth/forgot-password…) backend CHƯA có — mock giữ đề xuất cũ.
/// </summary>
public class MockApiHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Giống backend: gửi trường lạ trong body → 400
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly MockDatabase _db;
    private readonly TimeSpan _latency;
    private readonly TimeSpan? _newTabletOrderEvery;

    public MockApiHandler(MockDatabase db, int latencyMs, int newTabletOrderEverySeconds)
    {
        _db = db;
        _latency = TimeSpan.FromMilliseconds(Math.Max(0, latencyMs));
        _newTabletOrderEvery = newTabletOrderEverySeconds > 0
            ? TimeSpan.FromSeconds(newTabletOrderEverySeconds)
            : null;
    }

    /// <summary>Lỗi nghiệp vụ trong mock → JSON lỗi chung của backend.</summary>
    private sealed class MockError(int status, string code, string message, object? details = null) : Exception(message)
    {
        public int Status { get; } = status;
        public string Code { get; } = code;
        public object? Details { get; } = details;
    }

    private static MockError BadRequest(string msg) => new(400, ApiErrorCodes.BadRequest, msg);
    private static MockError Invalid() => new(400, ApiErrorCodes.Validation, "Dữ liệu gửi lên không hợp lệ");
    private static MockError NotFound(string what) => new(404, ApiErrorCodes.NotFound, $"{what} không tồn tại");
    private static MockError Conflict(string msg) => new(409, ApiErrorCodes.Conflict, msg);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Giả lập độ trễ mạng để thấy được trạng thái "Đang tải...".
        if (_latency > TimeSpan.Zero)
        {
            await Task.Delay(_latency, cancellationToken);
        }

        var uri = request.RequestUri!;
        string[] segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        int apiIndex = Array.FindIndex(segments, s => s.Equals("api", StringComparison.OrdinalIgnoreCase));
        string[] route = apiIndex < 0
            ? []
            : segments[(apiIndex + 1)..].Select(Uri.UnescapeDataString).ToArray();

        var query = ParseQuery(uri.Query);
        string? body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            HttpResponseMessage? response = route switch
            {
                ["auth", var action] when request.Method == HttpMethod.Post => ForgotPasswordRoutes(action, body),
                ["v1", .. var rest] => Route(request.Method, rest, query, body, CurrentUser(request)),
                _ => null
            };

            return response ?? Error(404, ApiErrorCodes.NotFound, $"Không có endpoint {request.Method} {uri.AbsolutePath}");
        }
        catch (MockError e)
        {
            return Error(e.Status, e.Code, e.Message, e.Details);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return Error(400, ApiErrorCodes.Validation, "Dữ liệu gửi lên không hợp lệ");
        }
    }

    private HttpResponseMessage? Route(
        HttpMethod m,
        string[] r,
        Dictionary<string, string> q,
        string? body,
        NguoiDung? user)
    {
        bool get = m == HttpMethod.Get, post = m == HttpMethod.Post, put = m == HttpMethod.Put,
             patch = m == HttpMethod.Patch;

        switch (r)
        {
            // ------------------------------------------------ Hệ thống & xác thực
            case ["health"] when get:
                return Ok(new { status = "ok", database = "mock", time = DateTime.Now });
            case ["auth", "dang-nhap"] when post:
                return Login(body);
            case ["auth", "toi"] when get:
                return Ok(RequireUser(user));
            case ["auth", "doi-mat-khau"] when post:
                return ChangePassword(RequireUser(user), body);

            // ------------------------------------------------ Người dùng (QuanLy)
            case ["nguoi-dung", ..]:
                RequireAdmin(user);
                return Accounts(m, r, q, body, user!);

            // ------------------------------------------------ Danh mục & kích thước
            case ["danh-muc"] when get:
                return Ok(_db.DanhMucs.OrderBy(d => d.TenDanhMuc).Select(DanhMucDto).ToList());
            case ["danh-muc", var id] when get:
                return Ok(DanhMucDto(FindDanhMuc(Id(id))));
            case ["danh-muc", ..]:
                RequireAdmin(user);
                return Categories(m, r, body);
            case ["kich-thuoc"] when get:
                return Ok(_db.KichThuocs.OrderBy(k => k.MaKichThuoc.Length).ThenByDescending(k => k.MaKichThuoc).ToList());
            case ["kich-thuoc", ..]:
                RequireAdmin(user);
                return Sizes(m, r, body);

            // ------------------------------------------------ Món
            case ["mon"] when get:
                return MenuList(q);
            case ["mon", var id] when get:
                return Ok(MonDto(FindMon(Id(id))));
            case ["mon", var id, "con-hang"] when patch:
                RequireUser(user);
                FindMon(Id(id)).ConHang = Read<ConHangRequest>(body).ConHang ?? throw Invalid();
                return Ok(MonDto(FindMon(Id(id))));
            case ["mon", ..]:
                RequireAdmin(user);
                return Products(m, r, body);
            case ["chi-tiet-mon", var id, "cong-thuc"] when get:
                RequireUser(user);
                FindChiTietMon(Id(id));
                return Ok(RecipeOf(Id(id), null));
            case ["chi-tiet-mon", var id, "cong-thuc"] when put:
                RequireAdmin(user);
                FindChiTietMon(Id(id));
                SaveRecipe(Id(id), null, body);
                return Ok(RecipeOf(Id(id), null));

            // ------------------------------------------------ Topping
            case ["topping"] when get:
                bool all = q.TryGetValue("baoGomNgungBan", out var v) && bool.TryParse(v, out bool b) && b;
                return Ok(_db.Toppings.Where(t => all || !t.NgungBan).OrderBy(t => t.TenTopping).Select(ToppingDto).ToList());
            case ["topping", var id] when get:
                return Ok(ToppingDto(FindTopping(Id(id))));
            case ["topping", var id, "con-hang"] when patch:
                RequireUser(user);
                FindTopping(Id(id)).ConHang = Read<ConHangRequest>(body).ConHang ?? throw Invalid();
                return Ok(ToppingDto(FindTopping(Id(id))));
            case ["topping", var id, "cong-thuc"] when get:
                RequireUser(user);
                FindTopping(Id(id));
                return Ok(RecipeOf(null, Id(id)));
            case ["topping", ..]:
                RequireAdmin(user);
                return ToppingsAdmin(m, r, body);

            // ------------------------------------------------ Khuyến mãi
            case ["khuyen-mai"] when get:
                return PromotionList(q);
            case ["khuyen-mai", var id] when get:
                return Ok(KhuyenMaiDto(FindKhuyenMai(Id(id))));
            case ["khuyen-mai", ..]:
                RequireAdmin(user);
                return Promotions(m, r, body);

            // ------------------------------------------------ Đơn hàng
            case ["don-hang"] when post:
                return Json(HttpStatusCode.Created, PlaceOrder(Read<DatMonRequest>(body), user?.MaNguoiDung));
            case ["don-hang"] when get:
                RequireUser(user);
                return OrderList(q);
            case ["don-hang", var id] when get:
                RequireUser(user);
                return Ok(FindDonHang(Id(id)));
            case ["don-hang", var id, "thanh-toan"] when post:
                RequireUser(user);
                return Ok(Pay(FindDonHang(Id(id)), Read<ThanhToanRequest>(body), user!));
            case ["don-hang", var id, "huy"] when post:
                RequireAdmin(user);
                return Ok(Cancel(FindDonHang(Id(id)), Read<HuyDonRequest>(body)));

            // ------------------------------------------------ Kho (QuanLy)
            case ["nguyen-lieu", ..]:
                RequireAdmin(user);
                return Ingredients(m, r, q, body, user!);
            case ["lich-su-kho"] when get:
                RequireAdmin(user);
                return StockHistory(q);

            // ------------------------------------------------ Báo cáo (QuanLy)
            case ["bao-cao", var report] when get:
                RequireAdmin(user);
                return Report(report, q);
        }

        return null;
    }

    // ==================================================================
    // XÁC THỰC & PHÂN QUYỀN
    // ==================================================================

    private NguoiDung? CurrentUser(HttpRequestMessage request)
    {
        string? token = request.Headers.Authorization?.Parameter;
        const string prefix = "mock-token-";

        if (token is null || !token.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var user = _db.NguoiDungs.FirstOrDefault(u =>
            u.TenDangNhap.Equals(token[prefix.Length..], StringComparison.OrdinalIgnoreCase));

        return user is { TrangThai: true } ? user : null;
    }

    private static NguoiDung RequireUser(NguoiDung? user) =>
        user ?? throw new MockError(401, ApiErrorCodes.Unauthorized, "Chưa đăng nhập hoặc token không hợp lệ");

    private static NguoiDung RequireAdmin(NguoiDung? user)
    {
        RequireUser(user);

        return user!.VaiTro == Roles.Admin
            ? user
            : throw new MockError(403, ApiErrorCodes.Forbidden, "Bạn không có quyền thực hiện thao tác này");
    }

    private HttpResponseMessage Login(string? body)
    {
        var req = Read<DangNhapRequest>(body);
        var user = _db.NguoiDungs.FirstOrDefault(u => u.TenDangNhap == req.TenDangNhap);

        if (user is null || !_db.MatKhaus.TryGetValue(user.TenDangNhap, out var pass) || pass != req.MatKhau)
        {
            throw new MockError(401, ApiErrorCodes.Unauthorized, "Tên đăng nhập hoặc mật khẩu không đúng");
        }

        if (!user.TrangThai)
        {
            throw new MockError(403, ApiErrorCodes.Forbidden, "Tài khoản đã bị khóa, liên hệ quản lý");
        }

        return Ok(new DangNhapResponse
        {
            Token = $"mock-token-{user.TenDangNhap}",
            HetHan = DateTime.Now.AddHours(12),
            NguoiDung = user
        });
    }

    private HttpResponseMessage ChangePassword(NguoiDung me, string? body)
    {
        var req = Read<DoiMatKhauRequest>(body);

        if (req.MatKhauMoi.Length is < 6 or > 100) throw Invalid();
        if (_db.MatKhaus.GetValueOrDefault(me.TenDangNhap) != req.MatKhauCu) throw BadRequest("Mật khẩu cũ không đúng");

        _db.MatKhaus[me.TenDangNhap] = req.MatKhauMoi;
        return NoContent();
    }

    // ==================================================================
    // NGƯỜI DÙNG
    // ==================================================================

    private HttpResponseMessage? Accounts(HttpMethod m, string[] r, Dictionary<string, string> q, string? body, NguoiDung me)
    {
        switch (r)
        {
            case [_] when m == HttpMethod.Get:
                IEnumerable<NguoiDung> list = _db.NguoiDungs;
                if (q.TryGetValue("vaiTro", out var role) && role != "") list = list.Where(u => u.VaiTro == role);
                if (q.TryGetValue("trangThai", out var s) && bool.TryParse(s, out bool active)) list = list.Where(u => u.TrangThai == active);
                return Ok(list.OrderBy(u => u.MaNguoiDung).ToList());

            case [_, var id] when m == HttpMethod.Get:
                return Ok(FindNguoiDung(Id(id)));

            case [_] when m == HttpMethod.Post:
            {
                var req = Read<NguoiDungTaoRequest>(body);

                if (!System.Text.RegularExpressions.Regex.IsMatch(req.TenDangNhap, "^[a-zA-Z0-9_.]{3,50}$")
                    || req.MatKhau.Length is < 6 or > 100
                    || string.IsNullOrWhiteSpace(req.HoTen) || !req.Email.Contains('@')
                    || req.VaiTro is not (Roles.Admin or Roles.Staff))
                {
                    throw Invalid();
                }

                if (_db.NguoiDungs.Any(u => u.TenDangNhap == req.TenDangNhap)) throw Conflict("Tên đăng nhập đã tồn tại");
                if (_db.NguoiDungs.Any(u => u.Email.Equals(req.Email, StringComparison.OrdinalIgnoreCase))) throw Conflict("Email đã được sử dụng");

                var user = new NguoiDung
                {
                    MaNguoiDung = NextId(_db.NguoiDungs, u => u.MaNguoiDung),
                    TenDangNhap = req.TenDangNhap,
                    HoTen = req.HoTen,
                    Email = req.Email.ToLowerInvariant(),
                    VaiTro = req.VaiTro,
                    TrangThai = true
                };

                _db.NguoiDungs.Add(user);
                _db.MatKhaus[user.TenDangNhap] = req.MatKhau;
                return Json(HttpStatusCode.Created, user);
            }

            case [_, var id] when m == HttpMethod.Patch:
            {
                var user = FindNguoiDung(Id(id));
                var req = Read<NguoiDungCapNhatRequest>(body);

                if (req.VaiTro is not (null or Roles.Admin or Roles.Staff) || req.MatKhauMoi is { Length: < 6 or > 100 }) throw Invalid();
                if (user.MaNguoiDung == me.MaNguoiDung && (req.TrangThai == false || req.VaiTro == Roles.Staff))
                    throw Conflict("Không thể tự khóa hoặc tự hạ quyền tài khoản của chính mình");
                if (req.Email is not null && _db.NguoiDungs.Any(u => u.Email.Equals(req.Email, StringComparison.OrdinalIgnoreCase) && u.MaNguoiDung != user.MaNguoiDung))
                    throw Conflict("Email đã được sử dụng");

                user.HoTen = req.HoTen ?? user.HoTen;
                user.Email = req.Email?.ToLowerInvariant() ?? user.Email;
                user.VaiTro = req.VaiTro ?? user.VaiTro;
                user.TrangThai = req.TrangThai ?? user.TrangThai;
                if (req.MatKhauMoi is not null) _db.MatKhaus[user.TenDangNhap] = req.MatKhauMoi;
                return Ok(user);
            }
        }

        return null;
    }

    // ==================================================================
    // DANH MỤC & KÍCH THƯỚC
    // ==================================================================

    private DanhMuc DanhMucDto(DanhMuc d) => new()
    {
        MaDanhMuc = d.MaDanhMuc,
        TenDanhMuc = d.TenDanhMuc,
        SoMon = _db.Mons.Count(x => x.MaDanhMuc == d.MaDanhMuc && !x.NgungBan)
    };

    private HttpResponseMessage? Categories(HttpMethod m, string[] r, string? body)
    {
        switch (r)
        {
            case [_] when m == HttpMethod.Post:
            {
                var req = Read<DanhMucRequest>(body);
                if (string.IsNullOrWhiteSpace(req.TenDanhMuc) || req.TenDanhMuc.Length > 100) throw Invalid();
                var d = new DanhMuc { MaDanhMuc = NextId(_db.DanhMucs, x => x.MaDanhMuc), TenDanhMuc = req.TenDanhMuc };
                _db.DanhMucs.Add(d);
                return Json(HttpStatusCode.Created, DanhMucDto(d));
            }
            case [_, var id] when m == HttpMethod.Put:
            {
                var d = FindDanhMuc(Id(id));
                var req = Read<DanhMucRequest>(body);
                if (string.IsNullOrWhiteSpace(req.TenDanhMuc) || req.TenDanhMuc.Length > 100) throw Invalid();
                d.TenDanhMuc = req.TenDanhMuc;
                return Ok(DanhMucDto(d));
            }
            case [_, var id] when m == HttpMethod.Delete:
            {
                var d = FindDanhMuc(Id(id));
                int n = _db.Mons.Count(x => x.MaDanhMuc == d.MaDanhMuc);
                if (n > 0) throw Conflict($"Danh mục còn {n} món (kể cả món ngừng bán), hãy chuyển món sang danh mục khác trước");
                _db.DanhMucs.Remove(d);
                return NoContent();
            }
        }

        return null;
    }

    private HttpResponseMessage? Sizes(HttpMethod m, string[] r, string? body)
    {
        switch (r)
        {
            case [_] when m == HttpMethod.Post:
            {
                var req = Read<KichThuocRequest>(body);
                if (!System.Text.RegularExpressions.Regex.IsMatch(req.MaKichThuoc, "^[A-Za-z0-9]{1,10}$")
                    || string.IsNullOrWhiteSpace(req.TenKichThuoc)) throw Invalid();
                string ma = req.MaKichThuoc.ToUpperInvariant();
                if (_db.KichThuocs.Any(k => k.MaKichThuoc == ma)) throw Conflict($"Kích thước {ma} đã tồn tại");
                var k = new KichThuoc { MaKichThuoc = ma, TenKichThuoc = req.TenKichThuoc };
                _db.KichThuocs.Add(k);
                return Json(HttpStatusCode.Created, k);
            }
            case [_, var ma] when m == HttpMethod.Put:
            {
                var k = FindKichThuoc(ma);
                var req = Read<KichThuocRequest>(body);
                if (string.IsNullOrWhiteSpace(req.TenKichThuoc)) throw Invalid();
                k.TenKichThuoc = req.TenKichThuoc;
                return Ok(k);
            }
            case [_, var ma] when m == HttpMethod.Delete:
            {
                var k = FindKichThuoc(ma);
                if (_db.Mons.Any(x => x.Sizes.Any(s => s.MaKichThuoc == k.MaKichThuoc)))
                    throw Conflict("Kích thước đang được dùng trong bảng giá món");
                _db.KichThuocs.Remove(k);
                return NoContent();
            }
        }

        return null;
    }

    // ==================================================================
    // MÓN, TOPPING, CÔNG THỨC
    // ==================================================================

    private static decimal GiaSauGiam(decimal giaBan, KhuyenMaiRow? km, DateTime now) =>
        km is not null && km.DangApDung(now) ? Math.Round(giaBan * (100 - km.PhanTramGiam) / 100, 0) : giaBan;

    private bool DuNguyenLieu(int? maChiTietMon, int? maTopping) =>
        _db.CongThucs
            .Where(c => c.MaChiTietMon == maChiTietMon && c.MaTopping == maTopping)
            .All(c => _db.NguyenLieus.FirstOrDefault(n => n.MaNguyenLieu == c.MaNguyenLieu) is { } nl
                      && nl.SoLuongTon >= c.LuongSuDung);

    private Mon MonDto(MonRow m)
    {
        var now = DateTime.Now;
        var km = _db.KhuyenMais.FirstOrDefault(k => k.MaKhuyenMai == m.MaKhuyenMai);

        var sizes = m.Sizes.OrderBy(s => s.GiaBan).Select(s => new GiaTheoSize
        {
            MaChiTietMon = s.MaChiTietMon,
            MaKichThuoc = s.MaKichThuoc,
            TenKichThuoc = _db.KichThuocs.FirstOrDefault(k => k.MaKichThuoc == s.MaKichThuoc)?.TenKichThuoc ?? s.MaKichThuoc,
            GiaBan = s.GiaBan,
            GiaSauGiam = GiaSauGiam(s.GiaBan, km, now),
            DangApDung = s.DangApDung,
            DuNguyenLieu = DuNguyenLieu(s.MaChiTietMon, null)
        }).ToList();

        return new Mon
        {
            MaTenMon = m.MaTenMon,
            MaDanhMuc = m.MaDanhMuc,
            TenDanhMuc = _db.DanhMucs.FirstOrDefault(d => d.MaDanhMuc == m.MaDanhMuc)?.TenDanhMuc ?? "",
            TenMon = m.TenMon,
            HinhAnh = m.HinhAnh,
            ConHang = m.ConHang,
            NgungBan = m.NgungBan,
            CoTheBan = m.ConHang && !m.NgungBan && sizes.Any(s => s.DangApDung && s.DuNguyenLieu),
            KhuyenMai = km is null ? null : new KhuyenMaiNgan
            {
                MaKhuyenMai = km.MaKhuyenMai,
                Code = km.Code,
                PhanTramGiam = km.PhanTramGiam,
                DangApDung = km.DangApDung(now),
                GioBatDau = Hhmm(km.GioBatDau),
                GioKetThuc = Hhmm(km.GioKetThuc)
            },
            KichThuoc = sizes
        };
    }

    private Topping ToppingDto(ToppingRow t) => new()
    {
        MaTopping = t.MaTopping,
        TenTopping = t.TenTopping,
        GiaThem = t.GiaThem,
        ConHang = t.ConHang,
        NgungBan = t.NgungBan,
        DuNguyenLieu = DuNguyenLieu(null, t.MaTopping)
    };

    private HttpResponseMessage MenuList(Dictionary<string, string> q)
    {
        IEnumerable<MonRow> rows = _db.Mons;

        if (!Bool(q, "baoGomNgungBan")) rows = rows.Where(x => !x.NgungBan);
        if (Int(q, "maDanhMuc") is { } dm) rows = rows.Where(x => x.MaDanhMuc == dm);
        if (q.TryGetValue("tuKhoa", out var kw) && !string.IsNullOrWhiteSpace(kw))
            rows = rows.Where(x => x.TenMon.Contains(kw, StringComparison.OrdinalIgnoreCase));

        IEnumerable<Mon> items = rows.Select(MonDto);
        if (q.TryGetValue("coTheBan", out var ctb) && bool.TryParse(ctb, out bool coTheBan))
            items = items.Where(x => x.CoTheBan == coTheBan);
        if (q.TryGetValue("dangKhuyenMai", out var dkm) && bool.TryParse(dkm, out bool dangKm))
            items = items.Where(x => (x.KhuyenMai?.DangApDung ?? false) == dangKm);

        return Ok(Page(items.OrderBy(x => x.TenMon), q));
    }

    private HttpResponseMessage? Products(HttpMethod m, string[] r, string? body)
    {
        switch (r)
        {
            case [_] when m == HttpMethod.Post:
            {
                var req = Read<MonTaoRequest>(body);
                if (req.MaDanhMuc is null || string.IsNullOrWhiteSpace(req.TenMon) || req.TenMon.Length > 150
                    || req.KichThuoc.Count is < 1 or > 10 || req.KichThuoc.Any(k => k.GiaBan is null or < 0)
                    || req.HinhAnh is { Length: > 255 }) throw Invalid();
                CheckRefs(req.MaDanhMuc, req.MaKhuyenMai, req.KichThuoc.Select(k => k.MaKichThuoc));

                int ct = NextId(_db.Mons.SelectMany(x => x.Sizes), s => s.MaChiTietMon);
                var mon = new MonRow
                {
                    MaTenMon = NextId(_db.Mons, x => x.MaTenMon),
                    MaDanhMuc = req.MaDanhMuc.Value,
                    TenMon = req.TenMon,
                    HinhAnh = req.HinhAnh,
                    MaKhuyenMai = req.MaKhuyenMai,
                    Sizes = req.KichThuoc.Select(k => new ChiTietMonRow
                    {
                        MaChiTietMon = ct++,
                        MaKichThuoc = k.MaKichThuoc.ToUpperInvariant(),
                        GiaBan = k.GiaBan!.Value,
                        DangApDung = k.DangApDung
                    }).ToList()
                };

                _db.Mons.Add(mon);
                return Json(HttpStatusCode.Created, MonDto(mon));
            }
            case [_, var id] when m == HttpMethod.Patch:
            {
                var mon = FindMon(Id(id));
                var req = Read<MonCapNhatRequest>(body);
                if (req.TenMon is { Length: 0 or > 150 } || req.HinhAnh is { Length: > 255 } || req.MaKhuyenMai < 0) throw Invalid();
                CheckRefs(req.MaDanhMuc, req.MaKhuyenMai, null);

                mon.MaDanhMuc = req.MaDanhMuc ?? mon.MaDanhMuc;
                mon.TenMon = req.TenMon ?? mon.TenMon;
                mon.HinhAnh = req.HinhAnh ?? mon.HinhAnh;
                if (req.MaKhuyenMai is { } km) mon.MaKhuyenMai = km == 0 ? null : km;
                mon.NgungBan = req.NgungBan ?? mon.NgungBan;
                return Ok(MonDto(mon));
            }
            case [_, var id] when m == HttpMethod.Delete:
                FindMon(Id(id)).NgungBan = true;
                return NoContent();

            case [_, var id, "kich-thuoc"] when m == HttpMethod.Put:
            {
                var mon = FindMon(Id(id));
                var req = Read<List<GiaSizeRequest>>(body);
                if (req.Count is < 1 or > 10 || req.Any(k => k.GiaBan is null or < 0)) throw Invalid();
                CheckRefs(null, null, req.Select(k => k.MaKichThuoc));

                int ct = NextId(_db.Mons.SelectMany(x => x.Sizes), s => s.MaChiTietMon);
                foreach (var k in req)
                {
                    string ma = k.MaKichThuoc.ToUpperInvariant();
                    var size = mon.Sizes.FirstOrDefault(s => s.MaKichThuoc == ma);
                    if (size is null) mon.Sizes.Add(new ChiTietMonRow { MaChiTietMon = ct++, MaKichThuoc = ma, GiaBan = k.GiaBan!.Value, DangApDung = k.DangApDung });
                    else (size.GiaBan, size.DangApDung) = (k.GiaBan!.Value, k.DangApDung);
                }

                var keep = req.Select(k => k.MaKichThuoc.ToUpperInvariant()).ToHashSet();
                foreach (var s in mon.Sizes.Where(s => !keep.Contains(s.MaKichThuoc))) s.DangApDung = false;
                return Ok(MonDto(mon));
            }
        }

        return null;
    }

    private void CheckRefs(int? maDanhMuc, int? maKhuyenMai, IEnumerable<string>? sizes)
    {
        if (maDanhMuc is { } dm && _db.DanhMucs.All(d => d.MaDanhMuc != dm)) throw NotFound("Danh mục");
        if (maKhuyenMai is > 0 and var km && _db.KhuyenMais.All(k => k.MaKhuyenMai != km)) throw NotFound("Khuyến mãi");

        if (sizes is not null)
        {
            var list = sizes.Select(s => s.ToUpperInvariant()).ToList();
            if (list.Distinct().Count() != list.Count) throw BadRequest("Mỗi kích thước chỉ khai báo một lần");
            var missing = list.Where(s => _db.KichThuocs.All(k => k.MaKichThuoc != s)).ToList();
            if (missing.Count > 0) throw NotFound($"Kích thước {string.Join(", ", missing)}");
        }
    }

    private HttpResponseMessage? ToppingsAdmin(HttpMethod m, string[] r, string? body)
    {
        switch (r)
        {
            case [_] when m == HttpMethod.Post:
            {
                var req = ValidTopping(Read<ToppingRequest>(body));
                var t = new ToppingRow
                {
                    MaTopping = NextId(_db.Toppings, x => x.MaTopping),
                    TenTopping = req.TenTopping, GiaThem = req.GiaThem!.Value, ConHang = req.ConHang, NgungBan = req.NgungBan
                };
                _db.Toppings.Add(t);
                return Json(HttpStatusCode.Created, ToppingDto(t));
            }
            case [_, var id] when m == HttpMethod.Put:
            {
                var t = FindTopping(Id(id));
                var req = ValidTopping(Read<ToppingRequest>(body));
                (t.TenTopping, t.GiaThem, t.ConHang, t.NgungBan) = (req.TenTopping, req.GiaThem!.Value, req.ConHang, req.NgungBan);
                return Ok(ToppingDto(t));
            }
            case [_, var id] when m == HttpMethod.Delete:
                FindTopping(Id(id)).NgungBan = true;
                return NoContent();

            case [_, var id, "cong-thuc"] when m == HttpMethod.Put:
                FindTopping(Id(id));
                SaveRecipe(null, Id(id), body);
                return Ok(RecipeOf(null, Id(id)));
        }

        return null;
    }

    private static ToppingRequest ValidTopping(ToppingRequest req) =>
        string.IsNullOrWhiteSpace(req.TenTopping) || req.TenTopping.Length > 100 || req.GiaThem is null or < 0
            ? throw Invalid()
            : req;

    private List<CongThucDong> RecipeOf(int? maChiTietMon, int? maTopping) =>
        _db.CongThucs
            .Where(c => c.MaChiTietMon == maChiTietMon && c.MaTopping == maTopping)
            .OrderBy(c => c.MaCongThuc)
            .Select(c =>
            {
                var nl = _db.NguyenLieus.First(n => n.MaNguyenLieu == c.MaNguyenLieu);
                return new CongThucDong
                {
                    MaCongThuc = c.MaCongThuc,
                    MaNguyenLieu = c.MaNguyenLieu,
                    TenNguyenLieu = nl.TenNguyenLieu,
                    DonViTinh = nl.DonViTinh,
                    LuongSuDung = c.LuongSuDung
                };
            })
            .ToList();

    private void SaveRecipe(int? maChiTietMon, int? maTopping, string? body)
    {
        var req = Read<List<CongThucDongRequest>>(body);

        if (req.Count > 50 || req.Any(x => x.MaNguyenLieu is null or < 1 || x.LuongSuDung is null or < 0.01m)) throw Invalid();

        var ids = req.Select(x => x.MaNguyenLieu!.Value).ToList();
        if (ids.Distinct().Count() != ids.Count) throw BadRequest("Mỗi nguyên liệu chỉ xuất hiện một lần trong công thức");
        var missing = ids.Where(id => _db.NguyenLieus.All(n => n.MaNguyenLieu != id)).ToList();
        if (missing.Count > 0) throw NotFound($"Nguyên liệu #{string.Join(", #", missing)}");

        _db.CongThucs.RemoveAll(c => c.MaChiTietMon == maChiTietMon && c.MaTopping == maTopping);
        int next = NextId(_db.CongThucs, c => c.MaCongThuc);

        _db.CongThucs.AddRange(req.Select(x => new CongThucRow
        {
            MaCongThuc = next++,
            MaChiTietMon = maChiTietMon,
            MaTopping = maTopping,
            MaNguyenLieu = x.MaNguyenLieu!.Value,
            LuongSuDung = x.LuongSuDung!.Value
        }));
    }

    // ==================================================================
    // KHUYẾN MÃI
    // ==================================================================

    private static string? Hhmm(TimeSpan? t) => t?.ToString(@"hh\:mm");

    private KhuyenMai KhuyenMaiDto(KhuyenMaiRow k) => new()
    {
        MaKhuyenMai = k.MaKhuyenMai,
        Code = k.Code,
        PhanTramGiam = k.PhanTramGiam,
        NgayBatDau = k.NgayBatDau,
        NgayKetThuc = k.NgayKetThuc,
        GioBatDau = Hhmm(k.GioBatDau),
        GioKetThuc = Hhmm(k.GioKetThuc),
        DangApDung = k.DangApDung(DateTime.Now),
        SoMonApDung = _db.Mons.Count(x => x.MaKhuyenMai == k.MaKhuyenMai)
    };

    private HttpResponseMessage PromotionList(Dictionary<string, string> q)
    {
        IEnumerable<KhuyenMai> list = _db.KhuyenMais.OrderByDescending(k => k.NgayBatDau).Select(KhuyenMaiDto);
        if (q.TryGetValue("dangApDung", out var s) && bool.TryParse(s, out bool d)) list = list.Where(k => k.DangApDung == d);
        return Ok(list.ToList());
    }

    private HttpResponseMessage? Promotions(HttpMethod m, string[] r, string? body)
    {
        switch (r)
        {
            case [_] when m == HttpMethod.Post:
            {
                var req = Read<KhuyenMaiRequest>(body);
                var (from, to) = ValidPromotion(req);
                string code = req.Code.ToUpperInvariant();
                if (_db.KhuyenMais.Any(k => k.Code == code)) throw Conflict("Code khuyến mãi đã tồn tại");

                var k = new KhuyenMaiRow
                {
                    MaKhuyenMai = NextId(_db.KhuyenMais, x => x.MaKhuyenMai),
                    Code = code, PhanTramGiam = req.PhanTramGiam!.Value,
                    NgayBatDau = req.NgayBatDau!.Value, NgayKetThuc = req.NgayKetThuc!.Value,
                    GioBatDau = from, GioKetThuc = to
                };
                AttachProducts(k.MaKhuyenMai, req.MaTenMons);
                _db.KhuyenMais.Add(k);
                return Json(HttpStatusCode.Created, KhuyenMaiDto(k));
            }
            case [_, var id] when m == HttpMethod.Put:
            {
                var k = FindKhuyenMai(Id(id));
                var req = Read<KhuyenMaiRequest>(body);
                var (from, to) = ValidPromotion(req);
                string code = req.Code.ToUpperInvariant();
                if (_db.KhuyenMais.Any(x => x.Code == code && x.MaKhuyenMai != k.MaKhuyenMai)) throw Conflict("Code khuyến mãi đã tồn tại");

                AttachProducts(k.MaKhuyenMai, req.MaTenMons);
                (k.Code, k.PhanTramGiam, k.NgayBatDau, k.NgayKetThuc, k.GioBatDau, k.GioKetThuc) =
                    (code, req.PhanTramGiam!.Value, req.NgayBatDau!.Value, req.NgayKetThuc!.Value, from, to);
                return Ok(KhuyenMaiDto(k));
            }
            case [_, var id] when m == HttpMethod.Delete:
            {
                var k = FindKhuyenMai(Id(id));
                AttachProducts(k.MaKhuyenMai, []);
                _db.KhuyenMais.Remove(k);
                return NoContent();
            }
        }

        return null;
    }

    private static (TimeSpan?, TimeSpan?) ValidPromotion(KhuyenMaiRequest r)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(r.Code, "^[A-Za-z0-9_-]{3,20}$")
            || r.PhanTramGiam is null or < 0.01m or > 100 || r.NgayBatDau is null || r.NgayKetThuc is null) throw Invalid();
        if (r.NgayBatDau >= r.NgayKetThuc) throw BadRequest("ngayBatDau phải trước ngayKetThuc");
        if ((r.GioBatDau is null) != (r.GioKetThuc is null)) throw BadRequest("Phải nhập đủ cả gioBatDau và gioKetThuc (hoặc bỏ trống cả hai)");

        TimeSpan? from = r.GioBatDau is null ? null : TimeSpan.Parse(r.GioBatDau, CultureInfo.InvariantCulture);
        TimeSpan? to = r.GioKetThuc is null ? null : TimeSpan.Parse(r.GioKetThuc, CultureInfo.InvariantCulture);
        if (from >= to) throw BadRequest("gioBatDau phải trước gioKetThuc");
        return (from, to);
    }

    private void AttachProducts(int maKhuyenMai, List<int>? maTenMons)
    {
        if (maTenMons is null) return;

        var missing = maTenMons.Where(id => _db.Mons.All(x => x.MaTenMon != id)).ToList();
        if (missing.Count > 0) throw NotFound($"Món #{string.Join(", #", missing)}");

        foreach (var mon in _db.Mons.Where(x => maTenMons.Contains(x.MaTenMon) || x.MaKhuyenMai == maKhuyenMai))
        {
            mon.MaKhuyenMai = maTenMons.Contains(mon.MaTenMon) ? maKhuyenMai : null;
        }
    }

    // ==================================================================
    // ĐƠN HÀNG
    // ==================================================================

    private HttpResponseMessage OrderList(Dictionary<string, string> q)
    {
        SimulateTabletOrders();

        IEnumerable<DonHang> list = _db.DonHangs;

        if (q.TryGetValue("trangThai", out var status) && status != "")
        {
            if (status is not (TrangThaiDon.Pending or TrangThaiDon.Paid or TrangThaiDon.Cancelled)) throw Invalid();
            list = list.Where(d => d.TrangThaiThanhToan == status);
        }

        if (Date(q, "tuNgay") is { } from) list = list.Where(d => d.ThoiGianTao >= from);
        if (Date(q, "denNgay") is { } to) list = list.Where(d => d.ThoiGianTao <= to);

        return Ok(Page(list.OrderByDescending(d => d.ThoiGianTao).ThenByDescending(d => d.MaDonHang), q));
    }

    private DonHang PlaceOrder(DatMonRequest req, int? maNhanVien)
    {
        if (req.ChiTiet.Count is < 1 or > 50
            || req.PhuongThucThanhToan is not (null or PhuongThucThanhToans.Cash or PhuongThucThanhToans.QrTransfer)
            || req.ChiTiet.Any(c => c.MaChiTietMon is null or < 1 || c.SoLuong is null or < 1 or > 50
                                    || c.GhiChu is { Length: > 255 } || c.Toppings is { Count: > 10 }))
        {
            throw Invalid();
        }

        var now = DateTime.Now;
        var need = new Dictionary<int, decimal>();

        void Add(int? maChiTietMon, int? maTopping, int soLuong)
        {
            foreach (var c in _db.CongThucs.Where(c => c.MaChiTietMon == maChiTietMon && c.MaTopping == maTopping))
            {
                need[c.MaNguyenLieu] = need.GetValueOrDefault(c.MaNguyenLieu) + c.LuongSuDung * soLuong;
            }
        }

        int lineId = NextId(_db.DonHangs.SelectMany(d => d.ChiTiet ?? []), c => c.MaChiTietDon);
        var order = new DonHang
        {
            MaDonHang = NextId(_db.DonHangs, d => d.MaDonHang),
            MaNguoiDung = maNhanVien,
            TenNhanVien = _db.NguoiDungs.FirstOrDefault(u => u.MaNguoiDung == maNhanVien)?.HoTen,
            PhuongThucThanhToan = req.PhuongThucThanhToan ?? PhuongThucThanhToans.Cash,
            TrangThaiThanhToan = TrangThaiDon.Pending,
            ThoiGianTao = now,
            ChiTiet = []
        };

        foreach (var line in req.ChiTiet)
        {
            var mon = _db.Mons.FirstOrDefault(x => x.Sizes.Any(s => s.MaChiTietMon == line.MaChiTietMon))
                      ?? throw NotFound($"Chi tiết món #{line.MaChiTietMon}");
            var size = mon.Sizes.First(s => s.MaChiTietMon == line.MaChiTietMon);

            if (mon.NgungBan) throw new MockError(422, "MON_NGUNG_BAN", $"Món \"{mon.TenMon}\" đã ngừng kinh doanh");
            if (!mon.ConHang) throw new MockError(422, "MON_HET_HANG", $"Món \"{mon.TenMon}\" tạm hết hàng");
            if (!size.DangApDung) throw new MockError(422, "SIZE_KHONG_AP_DUNG", $"Size {size.MaKichThuoc} của \"{mon.TenMon}\" không còn bán");

            var toppingIds = line.Toppings ?? [];
            if (toppingIds.Distinct().Count() != toppingIds.Count) throw BadRequest("Mỗi topping chỉ được chọn tối đa 1 phần/ly");

            int qty = line.SoLuong!.Value;
            var km = _db.KhuyenMais.FirstOrDefault(k => k.MaKhuyenMai == mon.MaKhuyenMai);
            var detail = new ChiTietDon
            {
                MaChiTietDon = lineId++,
                MaChiTietMon = size.MaChiTietMon,
                TenMonLuu = $"{mon.TenMon} - {FindKichThuoc(size.MaKichThuoc).TenKichThuoc}",
                DonGiaBan = GiaSauGiam(size.GiaBan, km, now),
                SoLuong = qty,
                GhiChu = line.GhiChu
            };
            Add(size.MaChiTietMon, null, qty);

            foreach (int id in toppingIds)
            {
                var t = _db.Toppings.FirstOrDefault(x => x.MaTopping == id) ?? throw NotFound($"Topping #{id}");
                if (t.NgungBan || !t.ConHang) throw new MockError(422, "TOPPING_HET_HANG", $"Topping \"{t.TenTopping}\" hiện không phục vụ");
                detail.Toppings.Add(new ToppingDaChon { MaTopping = t.MaTopping, TenToppingLuu = t.TenTopping, DonGiaTopping = t.GiaThem });
                Add(null, t.MaTopping, qty);
            }

            detail.ThanhTien = (detail.DonGiaBan + detail.Toppings.Sum(t => t.DonGiaTopping)) * qty;
            order.ChiTiet.Add(detail);
        }

        var shortages = need
            .Select(kv => (Need: kv.Value, Row: _db.NguyenLieus.First(n => n.MaNguyenLieu == kv.Key)))
            .Where(x => x.Row.SoLuongTon < x.Need)
            .Select(x => new { tenNguyenLieu = x.Row.TenNguyenLieu, canDung = x.Need, conTon = x.Row.SoLuongTon, donViTinh = x.Row.DonViTinh })
            .ToList();

        if (shortages.Count > 0)
        {
            throw new MockError(422, ApiErrorCodes.OutOfIngredients, "Không đủ nguyên liệu để pha chế đơn này", shortages);
        }

        order.TongTien = order.ChiTiet.Sum(c => c.ThanhTien);
        _db.DonHangs.Add(order);
        return order;
    }

    /// <summary>
    /// Mô phỏng sp_ThanhToanVaTruKho: PAID + trừ kho theo công thức + ghi LichSuKho (TieuHaoTheoDon).
    /// </summary>
    private ThanhToanResponse Pay(DonHang order, ThanhToanRequest req, NguoiDung me)
    {
        if (req.PhuongThucThanhToan is not (PhuongThucThanhToans.Cash or PhuongThucThanhToans.QrTransfer)
            || req.TienKhachDua is < 0) throw Invalid();
        if (order.TrangThaiThanhToan != TrangThaiDon.Pending)
            throw Conflict($"Đơn đang ở trạng thái {order.TrangThaiThanhToan}, không thể thanh toán");
        if (req.TienKhachDua is { } given && given < order.TongTien)
            throw BadRequest($"Tiền khách đưa ({given:N0}) nhỏ hơn tổng tiền ({order.TongTien:N0})");

        var use = new Dictionary<int, decimal>();
        foreach (var line in order.ChiTiet ?? [])
        {
            foreach (var c in _db.CongThucs.Where(c => c.MaChiTietMon == line.MaChiTietMon && c.MaTopping == null))
                use[c.MaNguyenLieu] = use.GetValueOrDefault(c.MaNguyenLieu) + c.LuongSuDung * line.SoLuong;

            foreach (var tp in line.Toppings)
                foreach (var c in _db.CongThucs.Where(c => c.MaTopping == tp.MaTopping))
                    use[c.MaNguyenLieu] = use.GetValueOrDefault(c.MaNguyenLieu) + c.LuongSuDung * line.SoLuong;
        }

        // Giống sp_ThanhToanVaTruKho của backend: KHÔNG chặn khi thiếu kho (tồn kho có thể âm).
        // Thiếu nguyên liệu chỉ bị chặn lúc đặt món (422 HET_NGUYEN_LIEU).

        var now = DateTime.Now;
        int logId = NextId(_db.LichSuKhos, l => l.MaLichSu);

        foreach (var (id, qty) in use)
        {
            var nl = _db.NguyenLieus.First(n => n.MaNguyenLieu == id);
            nl.SoLuongTon -= qty;
            _db.LichSuKhos.Add(new LichSuKho
            {
                MaLichSu = logId++, MaNguyenLieu = id, TenNguyenLieu = nl.TenNguyenLieu, MaDonHang = order.MaDonHang,
                MaNguoiDung = null, LoaiGiaoDich = LoaiGiaoDichKho.TieuHaoTheoDon, // stored procedure ghi NULL
                SoLuongThayDoi = -qty, ThoiGian = now, GhiChu = $"Đơn #{order.MaDonHang}"
            });
        }

        order.PhuongThucThanhToan = req.PhuongThucThanhToan;
        order.TrangThaiThanhToan = TrangThaiDon.Paid;

        return new ThanhToanResponse
        {
            DonHang = order,
            TienThua = req.TienKhachDua is { } t ? t - order.TongTien : 0,
            CanhBaoKho = _db.NguyenLieus
                .Where(n => n.DangSuDung && n.SoLuongTon <= n.MucCanhBaoToiThieu)
                .OrderBy(n => n.TenNguyenLieu)
                .Select(n => new CanhBaoKho
                {
                    MaNguyenLieu = n.MaNguyenLieu, TenNguyenLieu = n.TenNguyenLieu, SoLuongTon = n.SoLuongTon,
                    MucCanhBaoToiThieu = n.MucCanhBaoToiThieu, DonViTinh = n.DonViTinh
                })
                .ToList()
        };
    }

    private static DonHang Cancel(DonHang order, HuyDonRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.LyDoHuy) || req.LyDoHuy.Length > 255) throw Invalid();
        if (order.TrangThaiThanhToan == TrangThaiDon.Cancelled) throw Conflict("Đơn đã bị hủy trước đó");

        if (order.TrangThaiThanhToan == TrangThaiDon.Paid)
        {
            order.DaHoanTien = true;
            order.GioHoanTien = DateTime.Now;
        }

        order.TrangThaiThanhToan = TrangThaiDon.Cancelled;
        order.LyDoHuy = req.LyDoHuy;
        return order;
    }

    /// <summary>Khách tự đặt trên tablet: thỉnh thoảng thêm một đơn mới khi trang tải danh sách.</summary>
    private void SimulateTabletOrders()
    {
        bool due = _db.TabletOrderRequested
                   || (_newTabletOrderEvery is { } every && DateTime.Now - _db.LastTabletOrder >= every);

        if (!due) return;

        _db.TabletOrderRequested = false;
        _db.LastTabletOrder = DateTime.Now;

        var sellable = _db.Mons
            .Where(x => !x.NgungBan && x.ConHang)
            .SelectMany(x => x.Sizes.Where(s => s.DangApDung && DuNguyenLieu(s.MaChiTietMon, null)))
            .ToList();

        if (sellable.Count == 0) return;

        try
        {
            PlaceOrder(new DatMonRequest
            {
                PhuongThucThanhToan = Random.Shared.Next(2) == 0 ? PhuongThucThanhToans.Cash : PhuongThucThanhToans.QrTransfer,
                ChiTiet = [new() { MaChiTietMon = sellable[Random.Shared.Next(sellable.Count)].MaChiTietMon, SoLuong = Random.Shared.Next(1, 3) }]
            }, null);
        }
        catch (MockError)
        {
            // Hết nguyên liệu → bỏ qua lần mô phỏng này
        }
    }

    // ==================================================================
    // KHO
    // ==================================================================

    private NguyenLieu NguyenLieuDto(NguyenLieuRow n)
    {
        var from = DateTime.Now.AddDays(-7);
        decimal used = -_db.LichSuKhos
            .Where(l => l.MaNguyenLieu == n.MaNguyenLieu && l.LoaiGiaoDich == LoaiGiaoDichKho.TieuHaoTheoDon && l.ThoiGian >= from)
            .Sum(l => l.SoLuongThayDoi);
        decimal perDay = Math.Round(used / 7, 2);
        decimal? daysLeft = perDay > 0 ? Math.Round(n.SoLuongTon / perDay, 1) : null;

        return new NguyenLieu
        {
            MaNguyenLieu = n.MaNguyenLieu,
            TenNguyenLieu = n.TenNguyenLieu,
            DonViTinh = n.DonViTinh,
            SoLuongTon = n.SoLuongTon,
            MucCanhBaoToiThieu = n.MucCanhBaoToiThieu,
            DangSuDung = n.DangSuDung,
            SapHet = n.SoLuongTon <= n.MucCanhBaoToiThieu || daysLeft <= 2,
            TieuHaoTrungBinhNgay = perDay,
            SoNgayConLai = daysLeft
        };
    }

    private HttpResponseMessage? Ingredients(HttpMethod m, string[] r, Dictionary<string, string> q, string? body, NguoiDung me)
    {
        switch (r)
        {
            case [_] when m == HttpMethod.Get:
            {
                IEnumerable<NguyenLieu> list = _db.NguyenLieus.OrderBy(n => n.TenNguyenLieu).Select(NguyenLieuDto);
                if (q.TryGetValue("dangSuDung", out var d) && bool.TryParse(d, out bool active)) list = list.Where(n => n.DangSuDung == active);
                if (q.TryGetValue("sapHet", out var s) && bool.TryParse(s, out bool low)) list = list.Where(n => n.SapHet == low);
                return Ok(list.ToList());
            }
            case [_, var id] when m == HttpMethod.Get:
                return Ok(NguyenLieuDto(FindNguyenLieu(Id(id))));

            case [_] when m == HttpMethod.Post:
            {
                var req = Read<NguyenLieuRequest>(body);
                if (string.IsNullOrWhiteSpace(req.TenNguyenLieu) || req.TenNguyenLieu.Length > 100
                    || string.IsNullOrWhiteSpace(req.DonViTinh) || req.DonViTinh.Length > 20
                    || req.SoLuongTon < 0 || req.MucCanhBaoToiThieu < 0) throw Invalid();

                var n = new NguyenLieuRow
                {
                    MaNguyenLieu = NextId(_db.NguyenLieus, x => x.MaNguyenLieu),
                    TenNguyenLieu = req.TenNguyenLieu, DonViTinh = req.DonViTinh, SoLuongTon = req.SoLuongTon,
                    MucCanhBaoToiThieu = req.MucCanhBaoToiThieu, DangSuDung = req.DangSuDung
                };
                _db.NguyenLieus.Add(n);

                if (req.SoLuongTon > 0)
                {
                    Log(n, LoaiGiaoDichKho.NhapHang, req.SoLuongTon, "Tồn kho ban đầu", me);
                }

                return Json(HttpStatusCode.Created, NguyenLieuDto(n));
            }
            case [_, var id] when m == HttpMethod.Patch:
            {
                var n = FindNguyenLieu(Id(id));
                var req = Read<NguyenLieuCapNhatRequest>(body);
                if (req.TenNguyenLieu is { Length: 0 or > 100 } || req.DonViTinh is { Length: 0 or > 20 } || req.MucCanhBaoToiThieu < 0) throw Invalid();

                n.TenNguyenLieu = req.TenNguyenLieu ?? n.TenNguyenLieu;
                n.DonViTinh = req.DonViTinh ?? n.DonViTinh;
                n.MucCanhBaoToiThieu = req.MucCanhBaoToiThieu ?? n.MucCanhBaoToiThieu;
                n.DangSuDung = req.DangSuDung ?? n.DangSuDung;
                return Ok(NguyenLieuDto(n));
            }
            case [_, var id] when m == HttpMethod.Delete:
                FindNguyenLieu(Id(id)).DangSuDung = false;
                return NoContent();

            case [_, var id, var action and ("nhap-kho" or "xuat-huy")] when m == HttpMethod.Post:
            {
                var n = FindNguyenLieu(Id(id));
                var req = Read<GiaoDichKhoRequest>(body);
                if (req.SoLuong is null or < 0.01m || req.GhiChu is { Length: > 255 }) throw Invalid();

                bool import = action == "nhap-kho";
                decimal change = import ? req.SoLuong.Value : -req.SoLuong.Value;
                if (n.SoLuongTon + change < 0)
                    throw Conflict($"Chỉ còn {n.SoLuongTon} {n.DonViTinh}, không thể xuất hủy {req.SoLuong}");

                n.SoLuongTon += change;
                Log(n, import ? LoaiGiaoDichKho.NhapHang : LoaiGiaoDichKho.XuatHuy, change, req.GhiChu, me);
                return Ok(NguyenLieuDto(n));
            }
        }

        return null;
    }

    private void Log(NguyenLieuRow n, string type, decimal change, string? note, NguoiDung me)
    {
        _db.LichSuKhos.Add(new LichSuKho
        {
            MaLichSu = NextId(_db.LichSuKhos, l => l.MaLichSu),
            MaNguyenLieu = n.MaNguyenLieu,
            TenNguyenLieu = n.TenNguyenLieu,
            MaNguoiDung = me.MaNguoiDung,
            LoaiGiaoDich = type,
            SoLuongThayDoi = change,
            ThoiGian = DateTime.Now,
            GhiChu = note
        });
    }

    private HttpResponseMessage StockHistory(Dictionary<string, string> q)
    {
        IEnumerable<LichSuKho> list = _db.LichSuKhos;

        if (Int(q, "maNguyenLieu") is { } id) list = list.Where(l => l.MaNguyenLieu == id);
        if (q.TryGetValue("loaiGiaoDich", out var type) && type != "")
        {
            if (type is not (LoaiGiaoDichKho.NhapHang or LoaiGiaoDichKho.XuatHuy or LoaiGiaoDichKho.TieuHaoTheoDon)) throw Invalid();
            list = list.Where(l => l.LoaiGiaoDich == type);
        }
        if (Date(q, "tuNgay") is { } from) list = list.Where(l => l.ThoiGian >= from);
        if (Date(q, "denNgay") is { } to) list = list.Where(l => l.ThoiGian <= to);

        return Ok(Page(list.OrderByDescending(l => l.ThoiGian).ThenByDescending(l => l.MaLichSu), q));
    }

    // ==================================================================
    // BÁO CÁO
    // ==================================================================

    private HttpResponseMessage? Report(string report, Dictionary<string, string> q)
    {
        IEnumerable<DonHang> orders = _db.DonHangs;
        DateTime? from = Date(q, "tuNgay"), to = Date(q, "denNgay");
        if (from is { } f) orders = orders.Where(d => d.ThoiGianTao >= f);
        if (to is { } t) orders = orders.Where(d => d.ThoiGianTao <= t);

        var all = orders.ToList();
        var paid = all.Where(d => d.TrangThaiThanhToan == TrangThaiDon.Paid).ToList();

        switch (report)
        {
            case "doanh-thu":
                decimal revenue = paid.Sum(d => d.TongTien);
                return Ok(new BaoCaoDoanhThu
                {
                    TuNgay = from,
                    DenNgay = to,
                    TongSoDon = all.Count,
                    SoDonDaThanhToan = paid.Count,
                    SoDonChoThanhToan = all.Count(d => d.TrangThaiThanhToan == TrangThaiDon.Pending),
                    SoDonHuy = all.Count(d => d.TrangThaiThanhToan == TrangThaiDon.Cancelled),
                    SoDonHoanTien = all.Count(d => d.DaHoanTien),
                    DoanhThu = revenue,
                    GiaTriDonTrungBinh = paid.Count == 0 ? 0 : Math.Round(revenue / paid.Count, 0),
                    TheoPhuongThuc = paid.GroupBy(d => d.PhuongThucThanhToan).ToDictionary(g => g.Key, g => g.Sum(d => d.TongTien)),
                    TheoNgay = paid.GroupBy(d => DateOnly.FromDateTime(d.ThoiGianTao)).OrderBy(g => g.Key)
                        .Select(g => new DoanhThuNgay { Ngay = g.Key, SoDon = g.Count(), DoanhThu = g.Sum(d => d.TongTien) })
                        .ToList()
                });

            case "mon-ban-chay":
                int top = Int(q, "soLuong") ?? 10;
                if (top is < 1 or > 50) throw Invalid();
                return Ok(paid
                    .SelectMany(d => d.ChiTiet ?? [])
                    .Select(c => (Line: c, Mon: _db.Mons.FirstOrDefault(x => x.Sizes.Any(s => s.MaChiTietMon == c.MaChiTietMon))))
                    .Where(x => x.Mon is not null)
                    .GroupBy(x => x.Mon!.MaTenMon)
                    .Select(g => new MonBanChay
                    {
                        MaTenMon = g.Key,
                        TenMon = g.First().Mon!.TenMon,
                        SoLuong = g.Sum(x => x.Line.SoLuong),
                        DoanhThu = g.Sum(x => x.Line.DonGiaBan * x.Line.SoLuong)
                    })
                    .OrderByDescending(x => x.SoLuong).ThenByDescending(x => x.DoanhThu)
                    .Take(top)
                    .ToList());

            case "gio-cao-diem":
                var byHour = paid.GroupBy(d => d.ThoiGianTao.Hour).ToDictionary(g => g.Key);
                return Ok(Enumerable.Range(0, 24)
                    .Select(h => byHour.TryGetValue(h, out var g)
                        ? new GioCaoDiem { Gio = h, SoDon = g.Count(), DoanhThu = g.Sum(d => d.TongTien) }
                        : new GioCaoDiem { Gio = h })
                    .ToList());
        }

        return null;
    }

    // ==================================================================
    // QUÊN MẬT KHẨU — backend CHƯA có; mock giữ đề xuất api/auth/* (ProblemDetails)
    // ==================================================================

    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(15);
    private const int MaxCodeAttempts = 5;
    private const int MaxCodeRequestsPerHour = 5;

    private HttpResponseMessage? ForgotPasswordRoutes(string action, string? body) => action switch
    {
        "forgot-password" => ForgotPassword(body),
        "verify-reset-code" => VerifyResetCode(body),
        "reset-password" => ResetPassword(body),
        _ => null
    };

    /// <summary>
    /// Bước 1: sinh mã 6 số cho email.
    /// Quy tắc chống dò email: phản hồi GIỐNG HỆT nhau dù email có tài khoản hay không —
    /// kể cả giới hạn gửi lại (60 giây) và giới hạn 5 lần/giờ đều áp cho MỌI email.
    /// </summary>
    private HttpResponseMessage ForgotPassword(string? body)
    {
        var request = JsonSerializer.Deserialize<ForgotPasswordRequest>(body ?? "{}", JsonOptions)!;
        string email = (request.Email ?? "").Trim();

        if (string.IsNullOrEmpty(email))
        {
            return Problem(HttpStatusCode.BadRequest, "Vui lòng nhập email.");
        }

        // Nhật ký xin mã theo email — giữ lại cả khi mã bị hủy, nên không lách được giới hạn
        var log = _db.ResetRequestLog.TryGetValue(email, out var times) ? times : _db.ResetRequestLog[email] = [];
        log.RemoveAll(t => t < DateTime.Now.AddHours(-1));

        if (log.Count > 0)
        {
            var wait = log[^1] + ResendCooldown - DateTime.Now;

            if (wait > TimeSpan.Zero)
            {
                var tooSoon = Problem((HttpStatusCode)429,
                    $"Mã vừa được gửi. Vui lòng chờ {Math.Ceiling(wait.TotalSeconds)} giây trước khi gửi lại.",
                    ResetErrorCodes.ResendTooSoon);

                // Header chuẩn HTTP: giao diện dùng để đếm ngược đúng số giây còn lại
                tooSoon.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                    TimeSpan.FromSeconds(Math.Ceiling(wait.TotalSeconds)));

                return tooSoon;
            }
        }

        if (log.Count >= MaxCodeRequestsPerHour)
        {
            return Problem((HttpStatusCode)429,
                "Bạn đã yêu cầu mã quá nhiều lần. Vui lòng thử lại sau 1 giờ.", "too_many_requests");
        }

        log.Add(DateTime.Now);

        var response = new ForgotPasswordResponse
        {
            Message = "Nếu email thuộc một tài khoản, mã xác minh đã được gửi. Vui lòng kiểm tra hộp thư (cả mục Spam).",
            ResendAfterSeconds = (int)ResendCooldown.TotalSeconds
        };

        var account = _db.NguoiDungs.FirstOrDefault(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

        if (account is not null)
        {
            string code = Random.Shared.Next(0, 1_000_000).ToString("D6");

            _db.ResetCodes[email] = new ResetCode
            {
                Username = account.TenDangNhap,
                Code = code,
                SentAt = DateTime.Now,
                ExpiresAt = DateTime.Now + CodeLifetime
            };

            response.DevCode = code; // CHỈ có ở giả lập — backend thật gửi mã qua Gmail
        }

        return Ok(response);
    }

    /// <summary>
    /// Bước 2: kiểm tra mã — đúng thì cấp token đặt lại mật khẩu (dùng một lần).
    /// </summary>
    private HttpResponseMessage VerifyResetCode(string? body)
    {
        var request = JsonSerializer.Deserialize<VerifyResetCodeRequest>(body ?? "{}", JsonOptions)!;
        string email = (request.Email ?? "").Trim();
        string code = (request.Code ?? "").Trim();

        if (!_db.ResetCodes.TryGetValue(email, out var entry) || entry.ExpiresAt < DateTime.Now)
        {
            _db.ResetCodes.Remove(email);
            return Problem(HttpStatusCode.BadRequest,
                "Mã không hợp lệ hoặc đã hết hạn. Kiểm tra lại email đã nhập, rồi gửi lại mã.", ResetErrorCodes.CodeExpired);
        }

        if (entry.Code != code)
        {
            entry.FailedAttempts++;
            int left = MaxCodeAttempts - entry.FailedAttempts;

            if (left <= 0)
            {
                _db.ResetCodes.Remove(email);
                return Problem(HttpStatusCode.BadRequest,
                    "Nhập sai quá số lần cho phép. Vui lòng gửi lại mã mới.", ResetErrorCodes.TooManyAttempts);
            }

            return Problem(HttpStatusCode.BadRequest,
                $"Mã xác minh không đúng. Còn {left} lần thử.", ResetErrorCodes.InvalidCode);
        }

        _db.ResetCodes.Remove(email);

        string token = Guid.NewGuid().ToString("N");
        _db.ResetTokens[token] = (entry.Username, DateTime.Now + ResetTokenLifetime);

        return Ok(new VerifyResetCodeResponse { ResetToken = token });
    }

    /// <summary>
    /// Bước 3: đặt mật khẩu mới bằng token. Kiểm tra dữ liệu TRƯỚC, chỉ hủy token khi đổi thành công.
    /// </summary>
    private HttpResponseMessage ResetPassword(string? body)
    {
        var request = JsonSerializer.Deserialize<ResetPasswordRequest>(body ?? "{}", JsonOptions)!;
        string token = request.ResetToken ?? "";
        string password = request.NewPassword ?? "";

        if (!_db.ResetTokens.TryGetValue(token, out var entry) || entry.ExpiresAt < DateTime.Now)
        {
            _db.ResetTokens.Remove(token);
            return Problem(HttpStatusCode.BadRequest,
                "Phiên đặt lại mật khẩu đã hết hạn. Vui lòng làm lại từ đầu.", ResetErrorCodes.TokenExpired);
        }

        if (password.Length is < 6 or > 100)
        {
            return Problem(HttpStatusCode.BadRequest, "Mật khẩu từ 6 đến 100 ký tự.");
        }

        _db.ResetTokens.Remove(token);
        _db.MatKhaus[entry.Username] = password;
        return NoContent();
    }

    // ==================================================================
    // TIỆN ÍCH
    // ==================================================================

    private NguoiDung FindNguoiDung(int id) => _db.NguoiDungs.FirstOrDefault(x => x.MaNguoiDung == id) ?? throw NotFound("Người dùng");
    private DanhMuc FindDanhMuc(int id) => _db.DanhMucs.FirstOrDefault(x => x.MaDanhMuc == id) ?? throw NotFound("Danh mục");
    private KichThuoc FindKichThuoc(string ma) => _db.KichThuocs.FirstOrDefault(x => x.MaKichThuoc.Equals(ma, StringComparison.OrdinalIgnoreCase)) ?? throw NotFound("Kích thước");
    private MonRow FindMon(int id) => _db.Mons.FirstOrDefault(x => x.MaTenMon == id) ?? throw NotFound("Món");
    private ChiTietMonRow FindChiTietMon(int id) => _db.Mons.SelectMany(x => x.Sizes).FirstOrDefault(s => s.MaChiTietMon == id) ?? throw NotFound("Chi tiết món");
    private ToppingRow FindTopping(int id) => _db.Toppings.FirstOrDefault(x => x.MaTopping == id) ?? throw NotFound("Topping");
    private KhuyenMaiRow FindKhuyenMai(int id) => _db.KhuyenMais.FirstOrDefault(x => x.MaKhuyenMai == id) ?? throw NotFound("Khuyến mãi");
    private DonHang FindDonHang(int id) => _db.DonHangs.FirstOrDefault(x => x.MaDonHang == id) ?? throw NotFound("Đơn hàng");
    private NguyenLieuRow FindNguyenLieu(int id) => _db.NguyenLieus.FirstOrDefault(x => x.MaNguyenLieu == id) ?? throw NotFound("Nguyên liệu");

    /// <summary>Route {id:int} của backend: không phải số → 404 như ASP.NET.</summary>
    private static int Id(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int id)
            ? id
            : throw new MockError(404, ApiErrorCodes.NotFound, $"Không có endpoint cho \"{value}\"");

    private static T Read<T>(string? body) =>
        JsonSerializer.Deserialize<T>(string.IsNullOrWhiteSpace(body) ? "null" : body, JsonOptions) ?? throw Invalid();

    private static PagedResult<T> Page<T>(IEnumerable<T> items, Dictionary<string, string> q)
    {
        int page = Int(q, "page") ?? 1;
        int limit = Int(q, "limit") ?? 20;
        if (page < 1 || limit is < 1 or > 100) throw Invalid();

        var list = items.ToList();
        return new PagedResult<T>
        {
            Data = list.Skip((page - 1) * limit).Take(limit).ToList(),
            Meta = new PageMeta { Page = page, Limit = limit, Total = list.Count, TotalPages = (int)Math.Ceiling(list.Count / (double)limit) }
        };
    }

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .GroupBy(kv => Uri.UnescapeDataString(kv[0]), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => Uri.UnescapeDataString(g.Last().Length > 1 ? g.Last()[1].Replace('+', ' ') : ""),
                StringComparer.OrdinalIgnoreCase);

    private static int? Int(Dictionary<string, string> q, string key) =>
        q.TryGetValue(key, out var v) && v != ""
            ? int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : throw Invalid()
            : null;

    private static bool Bool(Dictionary<string, string> q, string key) =>
        q.TryGetValue(key, out var v) && bool.TryParse(v, out bool b) && b;

    private static DateTime? Date(Dictionary<string, string> q, string key) =>
        q.TryGetValue(key, out var v) && v != ""
            ? DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : throw Invalid()
            : null;

    private static HttpResponseMessage Ok<T>(T value) => Json(HttpStatusCode.OK, value);

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T value) =>
        new(status) { Content = JsonContent.Create(value, options: new JsonSerializerOptions(JsonSerializerDefaults.Web)) };

    private static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);

    /// <summary>Lỗi chung của backend: { error: { code, message, details } }.</summary>
    private static HttpResponseMessage Error(int status, string code, string message, object? details = null) =>
        Json((HttpStatusCode)status, new { error = new { code, message, details } });

    /// <summary>ProblemDetails — chỉ dùng cho API quên mật khẩu (đề xuất, backend chưa có).</summary>
    private static HttpResponseMessage Problem(HttpStatusCode status, string title, string? errorCode = null) =>
        Json(status, new { title, status = (int)status, errorCode });
}
