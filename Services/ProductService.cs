using System.Net.Http.Json;
using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Service gọi API sản phẩm.
/// </summary>
public class ProductService
{
    private readonly HttpClient _http;

    public ProductService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Product>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<Product>>("api/products")
               ?? [];
    }

    public async Task<Product?> GetByCodeAsync(string code)
    {
        return await _http.GetFromJsonAsync<Product>(
            $"api/products/{code}"
        );
    }

    public async Task<Product> CreateAsync(Product product)
    {
        var response = await _http.PostAsJsonAsync("api/products", product);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<Product>())!;
    }

    public async Task UpdateAsync(Product product)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/products/{product.Code}",
            product
        );

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string code)
    {
        var response = await _http.DeleteAsync($"api/products/{code}");
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveRecipeAsync(
        string productCode,
        List<RecipeLine> recipe
    )
    {
        var response = await _http.PutAsJsonAsync(
            $"api/products/{productCode}/recipe",
            recipe
        );

        response.EnsureSuccessStatusCode();
    }
}

