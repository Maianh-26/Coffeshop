using System.Net.Http.Json;
using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Service gọi API tài khoản.
/// </summary>
public class AccountService
{
    private readonly HttpClient _http;

    public AccountService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Account>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<Account>>(
            "api/accounts"
        ) ?? [];
    }

    public async Task<Account> CreateAsync(Account account)
    {
        var response = await _http.PostAsJsonAsync(
            "api/accounts",
            account
        );

        response.EnsureSuccessStatusCode();

        return (await response.Content
            .ReadFromJsonAsync<Account>())!;
    }

    public async Task UpdateAsync(Account account)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/accounts/{account.Code}",
            account
        );

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string code)
    {
        var response = await _http.DeleteAsync(
            $"api/accounts/{code}"
        );

        response.EnsureSuccessStatusCode();
    }
}

