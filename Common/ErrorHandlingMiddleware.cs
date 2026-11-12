using CoffeeShop.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Api.Common;

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (ApiException ex)
        {
            await Write(ctx, ex.Status, ex.Code, ex.Message, ex.Details);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true)
        {
            await Write(ctx, 409, "CONFLICT", "Dữ liệu bị trùng (mã/tên/email đã tồn tại)");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error");
            await Write(ctx, 500, "INTERNAL_ERROR", "Lỗi hệ thống");
        }
    }

    public static Task Write(HttpContext ctx, int status, string code, string message, object? details = null)
    {
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new ErrorBody(new ErrorInfo(code, message, details)));
    }

    /// <summary>Lỗi validate model (sai kiểu, thiếu trường, trường lạ...) → 400 VALIDATION_ERROR.</summary>
    public static IActionResult InvalidModel(ActionContext ctx)
    {
        var details = ctx.ModelState
            .Where(kv => kv.Value?.ValidationState == ModelValidationState.Invalid)
            .SelectMany(kv => kv.Value!.Errors.Select(e => new
            {
                path = kv.Key,
                message = string.IsNullOrEmpty(e.ErrorMessage) ? e.Exception?.Message ?? "Invalid" : e.ErrorMessage,
            }))
            .ToList();
        return new BadRequestObjectResult(new ErrorBody(new ErrorInfo("VALIDATION_ERROR", "Dữ liệu gửi lên không hợp lệ", details)));
    }
}
