# 4. Trang Món (Món · Danh mục · Topping & Size)

← [Mục lục](README.md)

- **URL:** `/products` · **Quyền:** chỉ Admin (`Products.razor:2` → `[Authorize(Roles = "Admin")]`).
- **Giao diện:** `Pages/Products/Products.razor` (1183 dòng)
- **Logic:** `Pages/Products/Products.razor.cs` (813 dòng), `partial class Products`.
- **Model:** `Models/Product.cs`, `Models/Category.cs`, `Models/Topping.cs`, `Models/Recipeline.cs` (`RecipeLine`: `IngredientId` + `Quantity`).

> ⚠️ **Trạng thái hiện tại — cần nói đúng khi bị hỏi:**
> Trang **chưa nối service**. Danh mục, nguyên liệu, topping/size và món được tạo sẵn trong `OnInitializedAsync` (`Products.razor.cs:120–292`). Thêm / sửa / xóa chỉ thay đổi danh sách trong bộ nhớ, **F5 là mất**.
> Phần nối đã viết sẵn: `ProductService`, `CategoryService`, `ToppingService` (`api/products`, `api/categories`, `api/toppings`, `PUT …/{code}/recipe`), và API giả xử lý CRUD chung ở `MockApiHandler.cs:129–135`.

---

## 4.1. Ý tưởng thiết kế: **một trang, 3 tab, dùng chung bộ khung**

Ba loại dữ liệu có thao tác giống nhau (tìm, lọc, thêm, sửa, xóa, phân trang), nên trang dùng **chung** thanh lọc, thanh nút, bảng, phân trang và hộp thoại. Biến `activeTab` quyết định đang làm việc với loại nào.

```
activeTab = "products" | "categories" | "toppings"        (Products.razor.cs:22)
categoryTab => activeTab == "categories"                  (.cs:23)
toppingTab  => activeTab == "toppings"                    (.cs:24)
```

```
┌ .filters-panel (razor 9–121) ───────────────────────────────────────────┐
│ [☕ Món] [▦ Danh mục] [Topping & Size]      ← tab, razor 12–38           │
│ [🔍 Tìm…] [Danh mục▾] [Trạng thái▾] [Lọc] [Đặt lại]  ← đổi theo tab     │
├ .action-bar (razor 125–163) ────────────────────────────────────────────┤
│ [+ Thêm món] [Sửa] [Xóa] [Công thức]   ← chữ nút đổi theo tab           │
├ .list-panel (razor 173–428) ───────────────────────────────────────────┤
│ Bảng: 3 phiên bản (topping 219 / danh mục 267 / món 300)                 │
│ 12 món · Trang 1/2                                    [‹] 1 [›]          │
└─────────────────────────────────────────────────────────────────────────┘
 <Modal> dùng chung (razor 432–1183): dialog = "product" | "category" | "topping"
                                              | "delete" | "recipe" | "notice"
```

> Thầy hỏi *"Sao 3 bảng mà chỉ có 1 trang?"*: "Em dùng biến `activeTab`. Trong `.razor` có `@if (toppingTab) … else if (categoryTab) … else …` để chọn vẽ bảng / ô lọc / form nào. Các nút Thêm, Sửa, Xóa gọi chung một hàm (`Add`, `Edit`, `AskDelete`), trong hàm lại kiểm tra tab để biết làm việc với danh sách nào. Nhờ vậy không phải viết 3 trang gần giống nhau."

---

## 4.2. Chuyển tab

`Products.razor:14–36`: 3 nút `class="tab @(activeTab == "products" ? "active" : "")"` → `SwitchTab("products" | "categories" | "toppings")` (`.cs:340`).
`SwitchTab` gán `activeTab` rồi gọi `Reset()` để xóa điều kiện lọc của tab cũ và về trang 1. CSS: `.tabs` `app.css:665`, `.tab` 673.

---

## 4.3. Tìm kiếm & lọc — **có nút Lọc thật sự** (khác trang Đơn hàng)

Trang này tách **giá trị đang gõ** và **giá trị đã áp dụng**:

| Đang gõ / chọn (`.cs:33–37`) | Đã áp dụng (`.cs:39–43`) |
|------------------------------|--------------------------|
| `search`, `category`, `status`, `toppingType`, `toppingStatus` | `appliedSearch`, `appliedCategory`, `appliedStatus`, `appliedToppingType`, `appliedToppingStatus` |

- Ô tìm (`razor 46–53`) `@bind="search" @bind:event="oninput"` + `@onkeydown="SearchKey"`: nhấn **Enter** cũng là lọc (`SearchKey`, `.cs:300`).
- Ô lọc đổi theo tab: **Topping** → Phân loại + Trạng thái (razor 57–76). **Món** → Danh mục + Trạng thái (razor 78–102). **Danh mục** → chỉ tìm.
- Nút **Lọc** (razor 105) → `Filter()` (`.cs:316`): chép giá trị đang gõ sang `applied*`, bỏ chọn dòng, về trang 1.
- Nút **Đặt lại** (razor 113) → `Reset()` (`.cs:329`): xóa ô rồi gọi `Filter()`.
- Kết quả lọc là thuộc tính tính toán (`.cs:52–76`): `Matches` (món), `CategoryMatches`, `ToppingMatches`. Tìm theo **mã hoặc tên**, không phân biệt hoa thường.
- Phân trang **8 dòng / trang** (`Size = 8`, `.cs:31`): `VisibleProducts = Matches.Skip((page-1)*8).Take(8)`. `Pages` (`.cs:92`), nút ‹ › → `Move` (`.cs:352`). Hết trang thì hiện hộp "Không có trang dữ liệu khác.".

> *"Sao trang Món phải bấm Lọc mới lọc, còn trang Đơn hàng lọc ngay?"*: "Trang Món lọc theo biến `applied*`, chỉ đổi khi bấm Lọc / Enter. Trang Đơn hàng lọc thẳng theo biến đang gõ. Đây là hai cách thiết kế khác nhau, nhóm nên thống nhất một kiểu."

---

## 4.4. Bảng & chọn dòng

- 3 vòng lặp: `VisibleToppings` (razor 221), `VisibleCategories` (269), `VisibleProducts` (302). Món có ảnh nhỏ (`<img class="product-image">`, razor 316) và cột Công thức.
- Bấm dòng → `selected = item.Code`. Dòng có `tabindex="0"`, nên bàn phím **Enter / Space** cũng chọn được (`SelectKey`, `.cs:308`).
- Bấm ra vùng trống của trang thì bỏ chọn: `<div class="products-page" @onclick="() => selected = null">` (razor 6–7). Các khung bên trong có `@onclick:stopPropagation="true"`, để bấm trong bảng không bị tính là bấm ra ngoài.
- Màu badge trạng thái: `Color()` (`.cs:110`): `Đang bán` / `Đang phục vụ` → xanh, `Tạm hết` → vàng, còn lại → đỏ.

---

## 4.5. Thêm / Sửa (món, danh mục, topping) — một hộp thoại cho tất cả

### Mở hộp thoại
| Nút | Hàm | Làm gì |
|-----|-----|--------|
| **+ Thêm …** (razor 128, chữ đổi theo tab) | `Add()` (`.cs:380`) | `editing = false`, tạo model rỗng đúng loại, đặt `dialog = "product" / "category" / "topping"` |
| **Sửa** (razor 137) | `Edit()` (`.cs:403`) | `editing = true`, `target = selected`, rồi `LoadEditTarget()` (`.cs:423`) **chép** dữ liệu sang model riêng để sửa (bấm Hủy thì bản gốc không đổi) |

- Khi sửa, đầu hộp thoại có ô **"Chọn món cần sửa"** (razor 440–488) → `PickEdit` (`.cs:467`). Nhờ vậy đổi được món cần sửa ngay trong hộp thoại, không cần chọn trước trên bảng.
- Tiêu đề hộp thoại lấy từ `DialogTitle` (`.cs:95`): "Thêm món mới" / "Sửa món" / "Sửa danh mục"…
- Component `Components/Modal.razor`: `Busy="@(saving || reading)"` (razor 436) để **không đóng được** trong lúc đang lưu / đọc ảnh.

### Form & kiểm tra dữ liệu
Mỗi form là `<EditForm Model=… OnValidSubmit=…>` + `<DataAnnotationsValidator />`. Quy tắc nằm trong model, không nằm trong trang:

| Form | Razor | Lưu bằng | Quy tắc (model) |
|------|-------|----------|-----------------|
| Món | 626–820 | `SaveProduct` (`.cs:558`) | `Product.cs`: tên bắt buộc ≤ 100 ký tự, **danh mục bắt buộc**, **giá 1 – 100.000.000 đ**, mô tả ≤ 500 |
| Danh mục | 823–918 | `SaveCategory` (`.cs:608`) | `Category.cs`: tên bắt buộc ≤ 80, thứ tự 1–999 |
| Topping / Size | 491–623 | `SaveTopping` (`.cs:646`) | `Topping.cs`: tên bắt buộc ≤ 100, phân loại bắt buộc, phụ thu 0 – 10.000.000 đ |

### Quy tắc nghiệp vụ: chưa có công thức thì không được "Đang bán"
`Products.razor:746–762`: ô Trạng thái món **luôn** có "Ngừng bán". Chỉ khi `model.Recipe.Count > 0` mới thêm "Đang bán" / "Tạm hết".
> Lý do: bán món thì kho phải trừ nguyên liệu theo công thức. Chưa khai báo công thức thì không cho bán.

### Lưu — hàm dùng chung `Execute` (`.cs:529`)
```
SaveProduct / SaveCategory / SaveTopping / SaveRecipe / Delete
   └─ Execute(action, "câu báo thành công")
        ├─ if (saving || reading) return;   ← chống bấm 2 lần
        ├─ saving = true                     ← nút Lưu bị khóa
        ├─ await action()                    ← thêm / sửa / xóa trong danh sách (sau này: gọi Service)
        ├─ Reload() → kẹp page trong khoảng hợp lệ
        ├─ đóng hộp thoại, bỏ chọn, message = "Đã thêm món mới." → banner xanh (razor 166–171)
        └─ finally saving = false
```
- Mã tự sinh khi thêm: món `SP0001…` (`.cs:589`), danh mục `DM0001…`, topping `TP-01…`, size `SZ-01…` (theo `Type`).

---

## 4.6. Ảnh món (chọn file, kiểm tra, xem trước)

| Bước | Ở đâu |
|------|-------|
| Nút "Chọn ảnh JPG / PNG" | razor ~706–716: một `<label for="p-image">` đóng vai nút, điều khiển `<InputFile class="native-file">` bị ẩn (CSS `.native-file` `app.css:912`). Cách này giúp nút có giao diện đẹp hơn ô chọn file mặc định |
| Chọn file | `OnChange="ReadImage"` → `ReadImage` (`.cs:774`) |
| Kiểm tra | chỉ `image/png`, `image/jpeg`, ≤ **2 MB**; sai thì hiện "Chọn ảnh JPG/PNG không quá 2 MB." (`imageError`) |
| Thu nhỏ | `file.RequestImageFileAsync("image/jpeg", 600, 600)`: trình duyệt tự thu ảnh về tối đa 600×600, nhẹ hơn |
| Lưu | chuyển thành chuỗi `data:image/jpeg;base64,...` gán vào `model.ImageUrl` → `<img>` xem trước (razor 723) |
| Xóa ảnh | nút "Xóa ảnh" → `RemoveImage` (`.cs:763`) |
| Đang đọc ảnh | `reading = true`: khóa nút Lưu và khóa đóng hộp thoại |

---

## 4.7. Xóa

Nút **Xóa** (razor 145) → `AskDelete()` (`.cs:481`) → `dialog = "delete"` (razor 921–1009):
- Có ô chọn **đối tượng cần xóa** (`<select @bind="target">`, razor 934), mặc định là dòng đang chọn.
- Nút đỏ "Xóa món" / "Xóa danh mục" / "Xóa Topping / Size" (razor 996–1006, đổi chữ thành "Đang xóa…" khi đang chạy) → `Delete()` (`.cs:722`) → `RemoveAll(x => x.Code == target)` trên đúng danh sách của tab.
- Chưa chọn gì → "Chọn đối tượng cần xóa.".

> Khi nối API, xóa danh mục **đang có món** thì backend nên trả **409**, và trang nên hiện câu tiếng Việt cho trường hợp này. API giả hiện **chưa** kiểm tra trường hợp này: nó chỉ trả 409 khi thêm trùng mã (`MockApiHandler.cs:509`).

---

## 4.8. Công thức (định mức nguyên liệu cho 1 phần)

Nút **Công thức** (razor 155–161, ẩn ở tab Danh mục) → `OpenRecipe()` (`.cs:487`) → `dialog = "recipe"` (razor 1012–1165):

| Phần | Ở đâu |
|------|-------|
| Chọn món / topping cần khai báo | `<select @onchange="PickRecipe">` (razor 1022) → `PickRecipe` (`.cs:508`) nạp công thức có sẵn vào biến `recipe` |
| Mỗi dòng: nguyên liệu + số lượng | razor ~1060–1120: `<select @bind="line.IngredientId">` (1073), `<input @bind="line.Quantity">` (1094). Đơn vị (g, ml, cái) lấy từ danh sách `ingredients` |
| Thêm dòng | nút "+ Thêm dòng" → `recipe.Add(new())` (razor 1131) |
| Bỏ dòng | nút thùng rác → `recipe.Remove(line)` (razor 1113) |
| Lưu | razor 1159 → `SaveRecipe()` (`.cs:691`): gán `Recipe` cho món / topping rồi gọi `Execute`. Sau này gọi `PUT api/products/{code}/recipe` (`ProductService.cs:61`) |

CSS: `.recipe-table` `app.css:947`.

> Thầy hỏi *"Công thức để làm gì?"*: "Mỗi món khai báo lượng nguyên liệu cho 1 phần, ví dụ Cà phê sữa đá = 20g cà phê + 30ml sữa đặc + 1 ly. Khi bán, kho trừ nguyên liệu theo định mức này. Topping và size cũng có công thức riêng, ví dụ size L dùng ly 700ml."

---

## 4.9. Lưu ý chất lượng code (biết để trả lời / sửa khi nối service)

- `Products.razor.cs:287, 548`: gán `error = exception.Message`, trái quy tắc dự án (không hiện lỗi kỹ thuật cho người dùng). Khi nối service, nên đổi sang câu cố định và ghi log bằng `ILogger`.
- `LoadEditTarget` (`.cs:423`) gọi `FirstOrDefault` lặp lại nhiều lần cho mỗi trường. Có thể gọn hơn bằng `Utilities/ModelCopy.Clone(...)`.

---

## 4.10. Tìm nhanh

| Muốn tìm | Gõ Ctrl+Shift+F |
|----------|-----------------|
| Chuyển tab | `SwitchTab` |
| Điều kiện lọc | `Matches =>`, `appliedSearch` |
| Nút Thêm / Sửa / Xóa | `Add()`, `Edit()`, `AskDelete` |
| Lưu (dùng chung) | `Execute(` |
| Quy tắc giá / tên | `Models/Product.cs` → `[Range`, `[Required` |
| Ảnh | `ReadImage`, `RequestImageFileAsync` |
| Công thức | `OpenRecipe`, `SaveRecipe`, `RecipeLine` |
| Trạng thái chỉ "Ngừng bán" khi chưa có công thức | `model.Recipe.Count > 0` |
| Dữ liệu mẫu | `OnInitializedAsync` trong `Products.razor.cs` |
