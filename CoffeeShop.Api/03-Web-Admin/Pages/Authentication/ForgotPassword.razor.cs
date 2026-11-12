using CoffeeShop.Admin.Components;
using CoffeeShop.Admin.Models;
using CoffeeShop.Admin.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace CoffeeShop.Admin.Pages.Authentication;

/// <summary>
/// Quên mật khẩu: (1) nhập email → backend gửi mã qua Gmail,
/// (2) nhập mã 6 số → nhận token, (3) đặt mật khẩu mới.
/// </summary>
public partial class ForgotPassword : IDisposable
{
    private enum Step { Email, Code, NewPassword, Done }

    [Inject]
    private AuthenticationService AuthService { get; set; } = default!;

    [Inject]
    private IConfiguration Configuration { get; set; } = default!;

    private Step step = Step.Email;

    private readonly ForgotPasswordRequest emailModel = new();
    private readonly VerifyResetCodeRequest codeModel = new();
    private ResetPasswordForm passwordModel = new();

    private string? resetToken;
    private string? info;
    private string? devCode;
    private string? error;
    private bool busy;
    private bool showPassword;

    // Hủy mọi request đang chạy khi rời trang
    private readonly CancellationTokenSource pageCts = new();

    // Đếm ngược "Gửi lại mã"
    private int resendIn;
    private CancellationTokenSource? countdown;

    /// <summary>
    /// Chỉ hiện mã giả lập khi app đang chạy với API giả — kể cả backend thật lỡ trả "devCode" cũng không hiện.
    /// </summary>
    private bool IsMockMode => Configuration.GetValue("Api:UseMock", true);

    // Mã hiện tại đã bị máy chủ hủy (hết hạn / sai quá số lần) → khóa ô nhập, chỉ cho gửi lại
    private bool codeLocked;

    // Ô cần đưa con trỏ vào sau khi đổi bước (người dùng bàn phím / điện thoại mở sẵn bàn phím số)
    private InputTextLive? emailInput;
    private InputTextLive? codeInput;
    private InputTextLive? passwordInput;
    private bool focusPending;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender && !focusPending) return;

        focusPending = false;

        var target = step switch
        {
            Step.Email => emailInput?.Element,
            Step.Code => codeInput?.Element,
            Step.NewPassword => passwordInput?.Element,
            _ => null
        };

        if (target is { } element)
        {
            try
            {
                await element.FocusAsync();
            }
            catch (Microsoft.JSInterop.JSException)
            {
                // Phần tử đã bị thay trong lúc vẽ lại — bỏ qua
            }
        }
    }

    private void GoTo(Step next)
    {
        step = next;
        focusPending = true;
    }

    private async Task SendCode()
    {
        if (busy) return;

        bool isResend = step == Step.Code;

        busy = true;
        error = null;

        try
        {
            var result = await AuthService.RequestResetCodeAsync(emailModel.Email, pageCts.Token);

            if (!result.Success)
            {
                // Mã vừa được gửi cho email này (ví dụ người dùng bấm Back / Đổi email rồi nhập lại)
                // → vẫn cho sang bước nhập mã, đếm ngược đúng thời gian còn lại
                if (result.ErrorCode == ResetErrorCodes.ResendTooSoon && step == Step.Email)
                {
                    info = "Mã đã được gửi tới email này ít phút trước. Hãy nhập mã đã nhận.";
                    codeModel.Code = "";
                    codeLocked = false;
                    GoTo(Step.Code);
                    StartCountdown(result.RetryAfterSeconds ?? 60);
                    return;
                }

                error = result.Error;

                // Đang ở bước nhập mã mà máy chủ bắt chờ → đếm ngược theo đúng số giây máy chủ trả
                if (result.ErrorCode == ResetErrorCodes.ResendTooSoon && result.RetryAfterSeconds is { } retry)
                {
                    StartCountdown(retry);
                }

                return;
            }

            info = isResend
                ? $"Đã gửi mã mới tới {emailModel.Email}. Mã cũ không còn dùng được."
                : result.Data?.Message;
            devCode = IsMockMode ? result.Data?.DevCode : null;
            codeModel.Code = "";
            codeLocked = false;
            GoTo(Step.Code);
            StartCountdown(result.Data?.ResendAfterSeconds ?? 60);
        }
        finally
        {
            busy = false;
        }
    }

    private async Task ResendCode()
    {
        error = null;
        await SendCode();
    }

    private void ChangeEmail()
    {
        if (busy) return;

        StopCountdown();
        error = null;
        devCode = null;
        codeLocked = false;
        GoTo(Step.Email);
    }

    /// <summary>
    /// Chỉ giữ chữ số (dán "677 137" từ email vẫn đúng), tối đa 6 số.
    /// </summary>
    private void NormalizeCode()
    {
        string digits = new(codeModel.Code.Where(char.IsAsciiDigit).ToArray());
        codeModel.Code = digits.Length > 6 ? digits[..6] : digits;
    }

    private async Task VerifyCode()
    {
        if (busy) return;

        busy = true;
        error = null;

        try
        {
            var result = await AuthService.VerifyResetCodeAsync(emailModel.Email, codeModel.Code, pageCts.Token);

            // Người dùng đã chuyển bước khác trong lúc chờ → bỏ qua kết quả cũ
            if (step != Step.Code) return;

            if (!result.Success || string.IsNullOrEmpty(result.Data?.ResetToken))
            {
                error = result.Error ?? "Mã xác minh không đúng.";
                codeModel.Code = "";

                // Mã đã bị hủy phía máy chủ → khóa ô nhập, xóa mã giả lập cũ.
                // Giữ nguyên đếm ngược: máy chủ vẫn áp thời gian chờ gửi lại (chống dò mã liên tục).
                if (result.ErrorCode is ResetErrorCodes.CodeExpired or ResetErrorCodes.TooManyAttempts)
                {
                    devCode = null;
                    codeLocked = true;
                    info = null;
                }

                return;
            }

            resetToken = result.Data.ResetToken;
            StopCountdown();
            devCode = null;
            passwordModel = new ResetPasswordForm();
            GoTo(Step.NewPassword);
        }
        finally
        {
            busy = false;
        }
    }

    private async Task SavePassword()
    {
        if (busy || resetToken is null) return;

        busy = true;
        error = null;

        try
        {
            var result = await AuthService.ResetPasswordAsync(resetToken, passwordModel.NewPassword, pageCts.Token);

            if (!result.Success)
            {
                error = result.Error;

                // Token hết hạn → phải làm lại từ đầu
                if (result.ErrorCode == ResetErrorCodes.TokenExpired)
                {
                    resetToken = null;
                    GoTo(Step.Email);
                }

                return;
            }

            // Token chỉ dùng một lần — xóa khỏi bộ nhớ ngay
            resetToken = null;
            passwordModel = new ResetPasswordForm();
            step = Step.Done;
        }
        finally
        {
            busy = false;
        }
    }

    private void StartCountdown(int seconds)
    {
        StopCountdown();

        resendIn = seconds;
        countdown = new CancellationTokenSource();
        _ = RunCountdownAsync(countdown.Token);
    }

    private async Task RunCountdownAsync(CancellationToken token)
    {
        try
        {
            while (resendIn > 0)
            {
                await Task.Delay(1000, token);

                // Bộ đếm cũ có thể còn một nhịp trong hàng đợi sau khi bị hủy → không trừ vào bộ đếm mới
                if (token.IsCancellationRequested) return;

                resendIn--;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // Đã rời bước nhập mã / rời trang
        }
    }

    private void StopCountdown()
    {
        countdown?.Cancel();
        countdown?.Dispose();
        countdown = null;
        resendIn = 0;
    }

    public void Dispose()
    {
        StopCountdown();
        pageCts.Cancel();
        pageCts.Dispose();
    }
}
