using System.Text.Json.Serialization;
using CoffeeShop.Api.Common;
using CoffeeShop.Api.Data;
using CoffeeShop.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------------------------------------------------------------- Cấu hình JWT
var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Secret.Length < 32) throw new InvalidOperationException("Jwt:Secret phải dài tối thiểu 32 ký tự (appsettings.json)");
builder.Services.AddSingleton(jwt);

// ---------------------------------------------------------------- Database: SQL Server — QuanCaPheDB
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(config.GetConnectionString("QuanCaPheDB")));

// ---------------------------------------------------------------- Services
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<MenuService>();
builder.Services.AddScoped<DonHangService>();
builder.Services.AddScoped<KhoService>();

// ---------------------------------------------------------------- Controllers + JSON theo hợp đồng
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Trường lạ trong body → 400 (tương đương additionalProperties: false trong OpenAPI)
        o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    })
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ErrorHandlingMiddleware.InvalidModel);

// ---------------------------------------------------------------- Xác thực JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Issuer,
            IssuerSigningKey = jwt.Key,
            RoleClaimType = "role",
            NameClaimType = "name",
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        // 401/403 cũng trả JSON đúng định dạng lỗi chung
        o.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                return ErrorHandlingMiddleware.Write(ctx.HttpContext, 401, "UNAUTHORIZED", "Chưa đăng nhập hoặc token không hợp lệ");
            },
            OnForbidden = ctx =>
                ErrorHandlingMiddleware.Write(ctx.HttpContext, 403, "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này"),
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors(); 

// ---------------------------------------------------------------- Dữ liệu mẫu (DB phải được tạo trước bằng database/QuanCaPheDB.sql)
if (config.GetValue("SeedSampleData", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        SeedData.Run(db);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Không kết nối/ghi được QuanCaPheDB. Đã chạy database/QuanCaPheDB.sql và kiểm tra ConnectionStrings chưa?");
        throw;
    }
}

// ---------------------------------------------------------------- Pipeline
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// API-first: Swagger UI hiển thị CHÍNH FILE HỢP ĐỒNG openapi.yaml (không sinh từ code)
var specPath = Path.Combine(AppContext.BaseDirectory, "openapi", "openapi.yaml");
app.MapGet("/openapi.yaml", () => Results.File(specPath, "application/yaml")).AllowAnonymous();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/openapi.yaml", "API Quán Cà Phê v1");
    o.RoutePrefix = "docs";
    o.DocumentTitle = "API Quán Cà Phê";
});
app.MapGet("/", () => Results.Redirect("/docs"));

app.MapControllers();
app.MapFallback(ctx => ErrorHandlingMiddleware.Write(ctx, 404, "NOT_FOUND", $"Không có endpoint {ctx.Request.Method} {ctx.Request.Path}"));

app.Run();

public partial class Program;
