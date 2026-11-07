using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoffeeShop.Admin.Models;
using CoffeeShop.Admin.Utilities;

namespace CoffeeShop.Admin.Services.Mock;

/// <summary>
/// Chặn mọi request "api/..." của HttpClient và trả lời bằng <see cref="MockDatabase"/>,
/// theo đúng hợp đồng API đã thống nhất với nhóm backend.
/// Các service và trang không biết mình đang chạy với dữ liệu giả.
/// Bật/tắt bằng "Api:UseMock" trong wwwroot/appsettings.json.
/// </summary>
public class MockApiHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly MockDatabase _db;
    private readonly TimeSpan _latency;
    private readonly TimeSpan? _newTabletOrderEvery;
    private DateTime _lastTabletOrder = DateTime.Now;

    public MockApiHandler(MockDatabase db, int latencyMs, int newTabletOrderEverySeconds)
    {
        _db = db;
        _latency = TimeSpan.FromMilliseconds(Math.Max(0, latencyMs));
        _newTabletOrderEvery = newTabletOrderEverySeconds > 0
            ? TimeSpan.FromSeconds(newTabletOrderEverySeconds)
            : null;
    }

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

        if (apiIndex < 0)
        {
            return Problem(HttpStatusCode.NotFound, $"Mock không xử lý đường dẫn {uri.AbsolutePath}.");
        }

        string[] route = segments[(apiIndex + 1)..].Select(Uri.UnescapeDataString).ToArray();
        var query = ParseQuery(uri.Query);
        string? body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        // Giống backend thật: mọi API (trừ đăng nhập và quên mật khẩu) yêu cầu header "Authorization: Bearer <token>" hợp lệ
        bool isAnonymous = route.Length >= 2
                           && route[0].Equals("auth", StringComparison.OrdinalIgnoreCase)
                           && AnonymousAuthRoutes.Contains(route[1]);

        if (!isAnonymous && !IsValidToken(request))
        {
            return Problem(HttpStatusCode.Unauthorized, "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        }

        // Phân quyền phía máy chủ (backend thật BẮT BUỘC làm tương tự — chặn ở giao diện không bảo vệ được dữ liệu)
        if (!isAnonymous && CurrentAccount(request)?.Role != Roles.Admin && !StaffMayCall(request.Method, route))
        {
            return Problem(HttpStatusCode.Forbidden, "Bạn không có quyền thực hiện thao tác này.");
        }

        // Tài khoản của chính người đang đăng nhập (mọi vai trò) — cần biết ai gọi nên xử lý ở đây
        if (route.Length == 2 && route[0].Equals("auth", StringComparison.OrdinalIgnoreCase))
        {
            var me = CurrentAccount(request)!; // đã qua kiểm tra token ở trên

            if (route[1].Equals("me", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
            {
                return Ok(me);
            }

            if (route[1].Equals("change-password", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                try
                {
                    return ChangePassword(me, body);
                }
                catch (JsonException)
                {
                    return Problem(HttpStatusCode.BadRequest, "Dữ liệu gửi lên không đúng định dạng.");
                }
            }
        }

        try
        {
            return Route(request.Method, route, query, body)
                   ?? Problem(HttpStatusCode.NotFound, $"Mock chưa hỗ trợ {request.Method} {uri.AbsolutePath}.");
        }
        catch (JsonException)
        {
            return Problem(HttpStatusCode.BadRequest, "Dữ liệu gửi lên không đúng định dạng.");
        }
    }

    private HttpResponseMessage? Route(
        HttpMethod method,
        string[] route,
        Dictionary<string, string> query,
        string? body)
    {
        if (route.Length == 0) return null;

        string resource = route[0].ToLowerInvariant();
        string? code = route.Length > 1 ? route[1] : null;
        string? action = route.Length > 2 ? route[2].ToLowerInvariant() : null;

        return resource switch
        {
            "auth" when code == "login" && method == HttpMethod.Post => Login(body),
            "auth" when code == "forgot-password" && method == HttpMethod.Post => ForgotPassword(body),
            "auth" when code == "verify-reset-code" && method == HttpMethod.Post => VerifyResetCode(body),
            "auth" when code == "reset-password" && method == HttpMethod.Post => ResetPassword(body),
            "accounts" when code is null && method == HttpMethod.Post => CreateAccount(body),
            "accounts" when code is not null && action is null && method == HttpMethod.Put => UpdateAccount(code, body),
            "orders" => Orders(method, code, action, query, body),
            "products" when action == "recipe" && method == HttpMethod.Put =>
                SaveRecipe(_db.Products, p => p.Code, code!, body, (p, r) => p.Recipe = r),
            "toppings" when action == "recipe" && method == HttpMethod.Put =>
                SaveRecipe(_db.Toppings, t => t.Code, code!, body, (t, r) => t.Recipe = r),
            "products" => Crud(_db.Products, p => p.Code, (p, c) => p.Code = c, "SP", method, code, body),
            "categories" => Crud(_db.Categories, c => c.Code, (c, v) => c.Code = v, "DM", method, code, body),
            "toppings" => Crud(_db.Toppings, t => t.Code, (t, c) => t.Code = c, "TP", method, code, body),
            "ingredients" => Crud(_db.Ingredients, i => i.Code, (i, c) => i.Code = c, "NL", method, code, body),
            "promotions" => Crud(_db.Promotions, p => p.Code, (p, c) => p.Code = c, "KM", method, code, body),
            "accounts" => Crud(_db.Accounts, a => a.Code, (a, c) => a.Code = c, "TK", method, code, body),
            "inventory" when code == "transactions" => Transactions(method, body),
            "dashboard" when code == "report" && method == HttpMethod.Get => Report(query),
            _ => null
        };
    }

    // ------------------------------------------------------------------
    // Xác thực
    // ------------------------------------------------------------------

    private HttpResponseMessage Login(string? body)
    {
        var request = Deserialize<LoginRequest>(body);
        var account = _db.Accounts.FirstOrDefault(a =>
            a.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase));

        bool ok = account is not null
                  && _db.Passwords.TryGetValue(account.Username, out string? password)
                  && password == request.Password;

        return Ok(ok
            ? new LoginResponse
            {
                Success = true,
                Token = $"mock-token-{account!.Username}",
                FullName = account.FullName,
                Role = account.Role
            }
            : new LoginResponse
            {
                Success = false,
                Message = "Sai tên đăng nhập hoặc mật khẩu."
            });
    }

    // ------------------------------------------------------------------
    // Quên mật khẩu (backend thật: sinh mã, gửi Gmail, lưu mã đã băm)
    // ------------------------------------------------------------------

    private static readonly HashSet<string> AnonymousAuthRoutes =
        new(StringComparer.OrdinalIgnoreCase) { "login", "forgot-password", "verify-reset-code", "reset-password" };

    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(15);
    private const int MaxCodeAttempts = 5;
    private const int MaxCodeRequestsPerHour = 5;

    /// <summary>
    /// Bước 1: sinh mã 6 số cho email.
    /// Quy tắc chống dò email: phản hồi GIỐNG HỆT nhau dù email có tài khoản hay không —
    /// kể cả giới hạn gửi lại (60 giây) và giới hạn 5 lần/giờ đều áp cho MỌI email.
    /// </summary>
    private HttpResponseMessage ForgotPassword(string? body)
    {
        var request = Deserialize<ForgotPasswordRequest>(body);
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

        var account = _db.Accounts.FirstOrDefault(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

        if (account is not null)
        {
            string code = Random.Shared.Next(0, 1_000_000).ToString("D6");

            _db.ResetCodes[email] = new MockDatabase.ResetCode
            {
                Username = account.Username,
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
        var request = Deserialize<VerifyResetCodeRequest>(body);
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
        var request = Deserialize<ResetPasswordRequest>(body);
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
        _db.Passwords[entry.Username] = password;
        return NoContent();
    }

    /// <summary>
    /// Đổi mật khẩu của chính mình: phải đúng mật khẩu hiện tại; mật khẩu mới 6–100 ký tự và khác mật khẩu cũ.
    /// </summary>
    private HttpResponseMessage ChangePassword(Account me, string? body)
    {
        var request = Deserialize<ChangePasswordRequest>(body);
        string current = request.CurrentPassword ?? "";
        string next = request.NewPassword ?? "";

        if (!_db.Passwords.TryGetValue(me.Username, out string? saved) || saved != current)
        {
            return Problem(HttpStatusCode.BadRequest, "Mật khẩu hiện tại không đúng.", ResetErrorCodes.WrongPassword);
        }

        if (next.Length is < 6 or > 100)
        {
            return Problem(HttpStatusCode.BadRequest, "Mật khẩu mới từ 6 đến 100 ký tự.");
        }

        if (next == current)
        {
            return Problem(HttpStatusCode.BadRequest, "Mật khẩu mới phải khác mật khẩu hiện tại.");
        }

        _db.Passwords[me.Username] = next;
        return NoContent();
    }

    // ------------------------------------------------------------------
    // Tài khoản (chỉ Admin — đã kiểm tra quyền trước khi vào Route)
    // ------------------------------------------------------------------

    /// <summary>
    /// Kiểm tra dữ liệu tài khoản: bắt buộc tên đăng nhập + email; tên đăng nhập và email không trùng.
    /// </summary>
    private HttpResponseMessage? ValidateAccount(Account account, string? exceptCode)
    {
        if (string.IsNullOrWhiteSpace(account.Username) || string.IsNullOrWhiteSpace(account.Email))
        {
            return Problem(HttpStatusCode.BadRequest, "Vui lòng nhập tên đăng nhập và email.");
        }

        var others = _db.Accounts.Where(a => !a.Code.Equals(exceptCode, StringComparison.OrdinalIgnoreCase)).ToList();

        if (others.Any(a => a.Username.Equals(account.Username.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Problem(HttpStatusCode.Conflict, $"Tên đăng nhập {account.Username} đã tồn tại.");
        }

        if (others.Any(a => a.Email.Equals(account.Email.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Problem(HttpStatusCode.Conflict, $"Email {account.Email} đã được dùng cho tài khoản khác.");
        }

        return null;
    }

    /// <summary>
    /// Chủ quán tạo tài khoản. Mock gán mật khẩu mặc định để tài khoản mới đăng nhập được ngay khi test.
    /// (Backend thật: cần thống nhất cách đặt mật khẩu ban đầu.)
    /// </summary>
    private HttpResponseMessage? CreateAccount(string? body)
    {
        var account = Deserialize<Account>(body);

        if (ValidateAccount(account, exceptCode: null) is { } invalid)
        {
            return invalid;
        }

        var response = Crud(_db.Accounts, a => a.Code, (a, c) => a.Code = c, "TK", HttpMethod.Post, null, body);

        if (response?.IsSuccessStatusCode == true)
        {
            _db.Passwords[account.Username.Trim()] = MockDatabase.DefaultPassword;
        }

        return response;
    }

    /// <summary>
    /// Sửa tài khoản: nếu đổi tên đăng nhập thì chuyển mật khẩu sang tên mới (không làm mất khả năng đăng nhập).
    /// </summary>
    private HttpResponseMessage? UpdateAccount(string code, string? body)
    {
        var existing = _db.Accounts.FirstOrDefault(a => a.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            return Problem(HttpStatusCode.NotFound, $"Không tìm thấy mã {code}.");
        }

        var updated = Deserialize<Account>(body);

        if (ValidateAccount(updated, exceptCode: code) is { } invalid)
        {
            return invalid;
        }

        string oldUsername = existing.Username;
        var response = Crud(_db.Accounts, a => a.Code, (a, c) => a.Code = c, "TK", HttpMethod.Put, code, body);

        if (response?.IsSuccessStatusCode == true
            && !oldUsername.Equals(updated.Username, StringComparison.OrdinalIgnoreCase)
            && _db.Passwords.Remove(oldUsername, out string? password))
        {
            _db.Passwords[updated.Username.Trim()] = password;
        }

        return response;
    }

    /// <summary>
    /// Nhân viên được gọi API nào: xử lý đơn hàng, ĐỌC thực đơn / khuyến mãi (để tạo đơn),
    /// và tài khoản của chính mình. Mọi thứ khác (ghi thực đơn, kho, báo cáo, quản lý tài khoản) → 403.
    /// </summary>
    private static bool StaffMayCall(HttpMethod method, string[] route)
    {
        if (route.Length == 0) return false;

        string resource = route[0].ToLowerInvariant();

        return resource switch
        {
            "orders" => true,
            "auth" => true, // me, change-password (đăng nhập / quên mật khẩu là API công khai)
            "products" or "categories" or "toppings" or "promotions" => method == HttpMethod.Get,
            _ => false
        };
    }

    /// <summary>
    /// Token giả có dạng "mock-token-{username}" và tài khoản đó phải còn tồn tại.
    /// </summary>
    private bool IsValidToken(HttpRequestMessage request) => CurrentAccount(request) is not null;

    /// <summary>
    /// Tài khoản ứng với token trong header, hoặc null nếu token thiếu/sai.
    /// </summary>
    private Account? CurrentAccount(HttpRequestMessage request)
    {
        var auth = request.Headers.Authorization;

        if (auth is null
            || !auth.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            || auth.Parameter is not { } token
            || !token.StartsWith("mock-token-", StringComparison.Ordinal))
        {
            return null;
        }

        string username = token["mock-token-".Length..];
        return _db.Accounts.FirstOrDefault(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------
    // CRUD dùng chung (định danh bằng Code)
    // ------------------------------------------------------------------

    private static HttpResponseMessage? Crud<T>(
        List<T> table,
        Func<T, string> getCode,
        Action<T, string> setCode,
        string prefix,
        HttpMethod method,
        string? code,
        string? body) where T : class
    {
        T? Find(string c) => table.FirstOrDefault(x =>
            getCode(x).Equals(c, StringComparison.OrdinalIgnoreCase));

        if (code is null)
        {
            if (method == HttpMethod.Get) return Ok(table);

            if (method == HttpMethod.Post)
            {
                var item = Deserialize<T>(body);

                if (string.IsNullOrWhiteSpace(getCode(item)))
                {
                    setCode(item, MockDatabase.NextCode(table.Select(getCode), prefix));
                }
                else if (Find(getCode(item)) is not null)
                {
                    return Problem(HttpStatusCode.Conflict, $"Mã {getCode(item)} đã tồn tại.");
                }

                table.Add(item);
                return Json(HttpStatusCode.Created, item);
            }

            return null;
        }

        T? existing = Find(code);

        if (existing is null)
        {
            return Problem(HttpStatusCode.NotFound, $"Không tìm thấy mã {code}.");
        }

        if (method == HttpMethod.Get) return Ok(existing);

        if (method == HttpMethod.Put)
        {
            var updated = Deserialize<T>(body);
            setCode(updated, getCode(existing));
            table[table.IndexOf(existing)] = updated;
            return NoContent();
        }

        if (method == HttpMethod.Delete)
        {
            table.Remove(existing);
            return NoContent();
        }

        return null;
    }

    private static HttpResponseMessage SaveRecipe<T>(
        List<T> table,
        Func<T, string> getCode,
        string code,
        string? body,
        Action<T, List<RecipeLine>> apply) where T : class
    {
        T? item = table.FirstOrDefault(x =>
            getCode(x).Equals(code, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            return Problem(HttpStatusCode.NotFound, $"Không tìm thấy mã {code}.");
        }

        apply(item, Deserialize<List<RecipeLine>>(body));
        return NoContent();
    }

    // ------------------------------------------------------------------
    // Đơn hàng
    // ------------------------------------------------------------------

    private HttpResponseMessage? Orders(
        HttpMethod method,
        string? code,
        string? action,
        Dictionary<string, string> query,
        string? body)
    {
        if (code is null && method == HttpMethod.Get)
        {
            SimulateTabletOrders();

            IEnumerable<Order> result = _db.Orders;

            if (TryDate(query, "from", out DateTime from))
                result = result.Where(o => o.CreatedAt.Date >= from);
            if (TryDate(query, "to", out DateTime to))
                result = result.Where(o => o.CreatedAt.Date <= to);
            if (query.TryGetValue("status", out string? status))
                result = result.Where(o => o.Status == status);

            return Ok(result.OrderByDescending(o => o.CreatedAt).ToList());
        }

        if (code is not null && action is not null && method == HttpMethod.Put)
        {
            var order = _db.Orders.FirstOrDefault(o => o.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

            if (order is null)
            {
                return Problem(HttpStatusCode.NotFound, $"Không tìm thấy đơn {code}.");
            }

            switch (action)
            {
                case "accept":
                    if (order.Status != "Chờ tiếp nhận")
                    {
                        return Problem(HttpStatusCode.Conflict, $"Đơn {code} đang ở trạng thái '{order.Status}', không thể tiếp nhận.");
                    }

                    order.Status = "Đang pha chế";
                    order.AcceptedAt = DateTime.Now;
                    return NoContent();

                case "pay":
                    if (order.PaymentStatus == "Đã thanh toán" || order.Status == "Đã hủy")
                    {
                        return Problem(HttpStatusCode.Conflict, $"Đơn {code} không thể thanh toán.");
                    }

                    var payment = Deserialize<JsonElement>(body);

                    if (payment.TryGetProperty("paymentMethod", out JsonElement methodValue))
                    {
                        order.PaymentMethod = methodValue.GetString() ?? order.PaymentMethod;
                    }

                    order.PaymentStatus = "Đã thanh toán";
                    order.PaidAt = DateTime.Now;

                    if (order.Status == "Chờ tiếp nhận")
                    {
                        order.Status = "Đang pha chế";
                        order.AcceptedAt = DateTime.Now;
                    }

                    return NoContent();
            }

            return null;
        }

        return Crud(_db.Orders, o => o.Code, (o, c) => o.Code = c, "DH", method, code, body);
    }

    private void SimulateTabletOrders()
    {
        if (_newTabletOrderEvery is null) return;

        if (DateTime.Now - _lastTabletOrder >= _newTabletOrderEvery)
        {
            _db.AddRandomTabletOrder();
            _lastTabletOrder = DateTime.Now;
        }
    }

    // ------------------------------------------------------------------
    // Kho
    // ------------------------------------------------------------------

    private HttpResponseMessage? Transactions(HttpMethod method, string? body)
    {
        if (method == HttpMethod.Get)
        {
            return Ok(_db.Transactions.OrderByDescending(t => t.CreatedAt).ToList());
        }

        if (method == HttpMethod.Post)
        {
            var transaction = Deserialize<InventoryTransaction>(body);
            var ingredient = _db.Ingredients.FirstOrDefault(i => i.Code == transaction.IngredientId);

            if (ingredient is null)
            {
                return Problem(HttpStatusCode.BadRequest, "Nguyên liệu không tồn tại.");
            }

            transaction.Code = MockDatabase.NextCode(_db.Transactions.Select(t => t.Code), "PK");
            transaction.IngredientName = ingredient.Name;
            transaction.Unit = ingredient.Unit;
            transaction.CreatedAt = DateTime.Now;

            _db.Transactions.Add(transaction);
            return Json(HttpStatusCode.Created, transaction);
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Báo cáo
    // ------------------------------------------------------------------

    private HttpResponseMessage Report(Dictionary<string, string> query)
    {
        bool hasFrom = TryDate(query, "from", out DateTime from);
        bool hasTo = TryDate(query, "to", out DateTime to);

        bool InRange(DateTime d) =>
            (!hasFrom || d.Date >= from) && (!hasTo || d.Date <= to);

        var paid = _db.Orders
            .Where(o => o.PaymentStatus == "Đã thanh toán" && InRange(o.CreatedAt))
            .GroupBy(o => o.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => (Orders: g.Count(), Revenue: g.Sum(o => o.Total)));

        var costs = _db.Transactions
            .Where(t => t.Type == "Nhập mua" && InRange(t.CreatedAt))
            .GroupBy(t => t.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Cost));

        var rows = paid.Keys.Union(costs.Keys)
            .OrderBy(d => d)
            .Select(d => new DailyReport
            {
                Date = d,
                Orders = paid.TryGetValue(d, out var p) ? p.Orders : 0,
                Revenue = paid.TryGetValue(d, out var r) ? r.Revenue : 0,
                Cost = costs.GetValueOrDefault(d)
            })
            .ToList();

        return Ok(new DashboardReport { Rows = rows });
    }

    // ------------------------------------------------------------------
    // Tiện ích
    // ------------------------------------------------------------------

    private static T Deserialize<T>(string? body) =>
        JsonSerializer.Deserialize<T>(body ?? "", JsonOptions)
        ?? throw new JsonException("Body rỗng.");

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                kv => Uri.UnescapeDataString(kv[0]),
                kv => kv.Length > 1 ? Uri.UnescapeDataString(kv[1].Replace('+', ' ')) : "",
                StringComparer.OrdinalIgnoreCase);

    private static bool TryDate(Dictionary<string, string> query, string key, out DateTime value)
    {
        value = default;
        return query.TryGetValue(key, out string? raw) && DateTime.TryParse(raw, out value);
    }

    private static HttpResponseMessage Ok<T>(T value) => Json(HttpStatusCode.OK, value);

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T value) =>
        new(status) { Content = JsonContent.Create(value, options: JsonOptions) };

    private static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);

    /// <summary>
    /// ProblemDetails: "title" là câu tiếng Việt cho người dùng; "errorCode" (tùy chọn) cho giao diện xử lý theo trường hợp.
    /// </summary>
    private static HttpResponseMessage Problem(HttpStatusCode status, string title, string? errorCode = null) =>
        new(status)
        {
            Content = JsonContent.Create(
                new { type = "about:blank", title, status = (int)status, errorCode },
                mediaType: new("application/problem+json"),
                options: JsonOptions)
        };
}
