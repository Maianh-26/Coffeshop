namespace CoffeeShop.Admin.Services;

/// <summary>
/// Quản lý trạng thái đăng nhập của ứng dụng.
/// </summary>
public class AppState
{
    public bool IsLoggedIn { get; private set; }

    public string? Token { get; private set; }

    public string? FullName { get; private set; }

    public string? Role { get; private set; }

    public event Action? OnChange;

    public void Login(string token, string fullName, string role)
    {
        Token = token;
        FullName = fullName;
        Role = role;
        IsLoggedIn = true;
        OnChange?.Invoke();
    }

    public void Logout()
    {
        Token = null;
        FullName = null;
        Role = null;
        IsLoggedIn = false;
        OnChange?.Invoke();
    }
}

