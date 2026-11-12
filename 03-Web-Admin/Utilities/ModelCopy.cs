using System.Text.Json;

namespace CoffeeShop.Admin.Utilities;

/// <summary>
/// Sao chép sâu (deep copy) một model bằng JSON serialize/deserialize.
/// Dùng khi cần clone đối tượng trước khi chỉnh sửa để giữ bản gốc nguyên vẹn.
/// </summary>
public static class ModelCopy
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Tạo bản sao sâu của đối tượng nguồn.
    /// </summary>
    public static T Clone<T>(T source) where T : class
    {
        string json = JsonSerializer.Serialize(source, Options);

        return JsonSerializer.Deserialize<T>(json, Options)!;
    }

    /// <summary>
    /// Sao chép giá trị từ nguồn sang đích (cùng kiểu).
    /// </summary>
    public static void CopyTo<T>(T source, T target) where T : class
    {
        string json = JsonSerializer.Serialize(source, Options);

        T copy = JsonSerializer.Deserialize<T>(json, Options)!;

        foreach (var property in typeof(T).GetProperties())
        {
            if (property.CanWrite)
            {
                property.SetValue(target, property.GetValue(copy));
            }
        }
    }
}

