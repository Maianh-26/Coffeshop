using System.Net.Http.Json;
using CoffeeShop.Admin.Models;

namespace CoffeeShop.Admin.Services;

/// <summary>
/// Service gọi API đơn hàng và cập nhật trạng thái đơn.
/// </summary>
public class OrderService
{
    private readonly HttpClient _http;

    public OrderService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Order>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<Order>>("api/orders")
               ?? [];
    }

    public async Task<List<Order>> GetByDateRangeAsync(
        DateTime from,
        DateTime to
    )
    {
        return await _http.GetFromJsonAsync<List<Order>>(
            $"api/orders?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"
        ) ?? [];
    }

    public async Task<Order?> GetByCodeAsync(string code)
    {
        return await _http.GetFromJsonAsync<Order>(
            $"api/orders/{code}"
        );
    }

    public async Task<Order> CreateAsync(Order order)
    {
        var response = await _http.PostAsJsonAsync("api/orders", order);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<Order>())!;
    }

    public async Task UpdateAsync(Order order)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/orders/{order.Code}",
            order
        );

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string code)
    {
        var response = await _http.DeleteAsync($"api/orders/{code}");
        response.EnsureSuccessStatusCode();
    }

    public async Task AcceptOrderAsync(string code)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/orders/{code}/accept",
            new { status = "Đang pha chế" }
        );

        response.EnsureSuccessStatusCode();
    }

    public async Task ConfirmPaymentAsync(string code, string paymentMethod)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/orders/{code}/pay",
            new { paymentMethod, paymentStatus = "Đã thanh toán" }
        );

        response.EnsureSuccessStatusCode();
    }
}
