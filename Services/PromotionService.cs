using System.Net.Http.Json;
using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Service gọi API khuyến mãi.
/// </summary>
public class PromotionService
{
    private readonly HttpClient _http;

    public PromotionService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Promotion>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<Promotion>>(
            "api/promotions"
        ) ?? [];
    }

    public async Task<Promotion> CreateAsync(Promotion promotion)
    {
        var response = await _http.PostAsJsonAsync(
            "api/promotions",
            promotion
        );

        response.EnsureSuccessStatusCode();

        return (await response.Content
            .ReadFromJsonAsync<Promotion>())!;
    }

    public async Task UpdateAsync(Promotion promotion)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/promotions/{promotion.Code}",
            promotion
        );

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string code)
    {
        var response = await _http.DeleteAsync(
            $"api/promotions/{code}"
        );

        response.EnsureSuccessStatusCode();
    }
}

