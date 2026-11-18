namespace CoffeeShop.Admin.Utilities;

/// <summary>
/// Sắp xếp theo cột dùng chung cho các trang danh sách (mẫu W3C APG "Sortable Table", giống trang Đơn hàng):
/// bấm cột mới → thấp đến cao; bấm lại cùng cột → đổi chiều; nút Làm mới gọi <see cref="Reset"/> về thứ tự mặc định.
/// Sắp xếp ổn định (LINQ OrderBy): bản ghi bằng nhau giữ thứ tự máy chủ trả về.
/// </summary>
public sealed class ColumnSort
{
    /// <summary>Cột đang sắp xếp; "" = thứ tự mặc định (máy chủ trả về).</summary>
    public string Key { get; private set; } = "";

    public bool Descending { get; private set; }

    public bool Active => Key != "";

    public void Toggle(string key)
    {
        if (Key == key)
        {
            Descending = !Descending;
        }
        else
        {
            Key = key;
            Descending = false;
        }
    }

    public void Reset()
    {
        Key = "";
        Descending = false;
    }

    public bool Is(string key) => Key == key;

    /// <summary>Giá trị aria-sort của thẻ th (null khi cột không được sắp xếp).</summary>
    public string? AriaSort(string key) => Key != key ? null : Descending ? "descending" : "ascending";

    /// <summary>"" / "asc" / "desc" cho biểu tượng SortIcon.</summary>
    public string Direction(string key) => Key != key ? "" : Descending ? "desc" : "asc";

    public string Title(string key, string name) =>
        Key != key
            ? $"Sắp xếp theo {name}: thấp đến cao"
            : Descending
                ? $"Đang xếp {name} cao đến thấp. Bấm để xếp thấp đến cao"
                : $"Đang xếp {name} thấp đến cao. Bấm để xếp cao đến thấp";

    /// <summary>Áp dụng cột đang chọn; cột không có trong <paramref name="columns"/> → giữ nguyên thứ tự.</summary>
    public IEnumerable<T> Apply<T>(IEnumerable<T> items, params (string Key, Func<T, IComparable> Value)[] columns)
    {
        foreach (var (key, value) in columns)
        {
            if (key == Key) return Descending ? items.OrderByDescending(value) : items.OrderBy(value);
        }

        return items;
    }
}
