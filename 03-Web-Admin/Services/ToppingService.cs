using System.Net.Http.Json;
using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Service gọi API quản lý Topping và Kích cỡ size nước.
/// </summary>
public class ToppingService
{
    private readonly HttpClient _http;

    public ToppingService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Topping>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<Topping>>("api/toppings")
               ?? [];
    }

    public async Task<Topping?> GetByCodeAsync(string code)
    {
        return await _http.GetFromJsonAsync<Topping>(
            $"api/toppings/{code}"
        );
    }

    public async Task<Topping> CreateAsync(Topping topping)
    {
        var response = await _http.PostAsJsonAsync("api/toppings", topping);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<Topping>())!;
    }

    public async Task UpdateAsync(Topping topping)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/toppings/{topping.Code}",
            topping
        );

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string code)
    {
        var response = await _http.DeleteAsync($"api/toppings/{code}");
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveRecipeAsync(
        string toppingCode,
        List<RecipeLine> recipe
    )
    {
        var response = await _http.PutAsJsonAsync(
            $"api/toppings/{toppingCode}/recipe",
            recipe
        );

        response.EnsureSuccessStatusCode();
    }
}

