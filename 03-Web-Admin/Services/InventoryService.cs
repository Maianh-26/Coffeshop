using System.Net.Http.Json;
using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Service gọi API kho nguyên liệu.
/// </summary>
public class InventoryService
{
    private readonly HttpClient _http;

    public InventoryService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Ingredient>> GetIngredientsAsync()
    {
        return await _http.GetFromJsonAsync<List<Ingredient>>(
            "api/ingredients"
        ) ?? [];
    }

    public async Task<Ingredient> CreateIngredientAsync(
        Ingredient ingredient
    )
    {
        var response = await _http.PostAsJsonAsync(
            "api/ingredients",
            ingredient
        );

        response.EnsureSuccessStatusCode();

        return (await response.Content
            .ReadFromJsonAsync<Ingredient>())!;
    }

    public async Task UpdateIngredientAsync(Ingredient ingredient)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/ingredients/{ingredient.Code}",
            ingredient
        );

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteIngredientAsync(string code)
    {
        var response = await _http.DeleteAsync(
            $"api/ingredients/{code}"
        );

        response.EnsureSuccessStatusCode();
    }

    public async Task<List<InventoryTransaction>> GetTransactionsAsync()
    {
        return await _http.GetFromJsonAsync<List<InventoryTransaction>>(
            "api/inventory/transactions"
        ) ?? [];
    }

    public async Task<InventoryTransaction> CreateTransactionAsync(
        InventoryTransaction transaction
    )
    {
        var response = await _http.PostAsJsonAsync(
            "api/inventory/transactions",
            transaction
        );

        response.EnsureSuccessStatusCode();

        return (await response.Content
            .ReadFromJsonAsync<InventoryTransaction>())!;
    }
}

