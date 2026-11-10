using CoffeeShop.Admin.Components;
using CoffeeShop.Admin.Models;
using CoffeeShop.Admin.Utilities;
using CoffeeShop.Admin.Services;
using Microsoft.AspNetCore.Components;

namespace CoffeeShop.Admin.Pages.MyAccount;

/// <summary>
/// Tài khoản của tôi: xem thông tin cá nhân và tự đổi mật khẩu (nhân viên và chủ quán).
/// </summary>
public partial class MyAccount : IDisposable
{
    [Inject]
    private AuthenticationService AuthService { get; set; } = default!;

    private readonly CancellationTokenSource pageCts = new();

    private NguoiDung? account;
    private bool loading = true;
    private string? loadError;
    private string? success;

    // Hộp thoại đổi mật khẩu
    private bool changing;
    private bool saving;
    private bool showPassword;
    private string? saveError;
    private string? currentPasswordError; // lỗi sai mật khẩu hiện tại — hiện ngay dưới ô đó
    private ChangePasswordForm form = new();

    // Đưa con trỏ vào ô "Mật khẩu hiện tại" khi mở hộp thoại / khi báo sai
    private InputTextLive? currentInput;
    private bool focusCurrent;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        loading = true;
        loadError = null;

        var result = await AuthService.GetMyAccountAsync(pageCts.Token);

        if (result.Success)
        {
            account = result.Data;
        }
        else
        {
            loadError = result.Error;
        }

        loading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!focusCurrent || currentInput?.Element is not { } element) return;

        focusCurrent = false;

        try
        {
            await element.FocusAsync();
        }
        catch (Microsoft.JSInterop.JSException)
        {
            // Hộp thoại vừa đóng — bỏ qua
        }
    }

    /// <summary>
    /// Người dùng sửa mật khẩu hiện tại → lỗi "sai mật khẩu" cũ không còn đúng nữa.
    /// </summary>
    private void ClearErrors()
    {
        currentPasswordError = null;
        saveError = null;
    }

    private void ClearSaveError() => saveError = null;

    private void OpenChangePassword()
    {
        form = new ChangePasswordForm();
        saveError = null;
        currentPasswordError = null;
        focusCurrent = true;
        success = null;
        showPassword = false;
        changing = true;
    }

    private void CloseChangePassword()
    {
        if (saving) return;

        // Đóng là xóa nội dung đã nhập
        form = new ChangePasswordForm();
        changing = false;
    }

    private async Task SavePassword()
    {
        if (saving) return;

        saving = true;
        ClearErrors();

        try
        {
            var result = await AuthService.ChangePasswordAsync(form.CurrentPassword, form.NewPassword, pageCts.Token);

            if (!result.Success)
            {
                if (result.ErrorCode == ResetErrorCodes.WrongPassword)
                {
                    // Báo ngay dưới ô sai, xóa ô và đưa con trỏ về đó để gõ lại
                    currentPasswordError = result.Error;
                    form.CurrentPassword = "";
                    focusCurrent = true;
                }
                else
                {
                    saveError = result.Error;
                }

                return;
            }

            form = new ChangePasswordForm();
            changing = false;
            success = "Đã đổi mật khẩu. Lần đăng nhập sau hãy dùng mật khẩu mới.";
        }
        finally
        {
            saving = false;
        }
    }

    public void Dispose()
    {
        pageCts.Cancel();
        pageCts.Dispose();
    }
}
