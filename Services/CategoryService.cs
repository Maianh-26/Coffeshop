using System.Net.Http.Json;
using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Service gọi API danh mục sản phẩm.
/// </summary>
public class CategoryService
{
    private readonly HttpClient _http;

    public CategoryService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Category>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<Category>>("api/categories")
               ?? [];
    }

    public async Task<Category> CreateAsync(Category category)
    {
        var response = await _http.PostAsJsonAsync(
            "api/categories",
            category
        );

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<Category>())!;
    }

    public async Task UpdateAsync(Category category)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/categories/{category.Code}",
            category
        );

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string code)
    {
        var response = await _http.DeleteAsync($"api/categories/{code}");
        response.EnsureSuccessStatusCode();
    }
}

