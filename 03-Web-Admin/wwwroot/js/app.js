// CoffeeShop.Admin JavaScript Helper Utilities

window.appHelpers = {
    /**
     * Tải file CSV về máy người dùng (hỗ trợ hiển thị đúng tiếng Việt trên Excel nhờ BOM UTF-8)
     * @param {string} fileName Tên file cần tải về
     * @param {string} content Nội dung CSV
     */
    downloadCsv: function (fileName, content) {
        var bom = "\uFEFF";
        var blob = new Blob([bom + content], { type: "text/csv;charset=utf-8;" });
        var url = URL.createObjectURL(blob);
        var link = document.createElement("a");
        link.setAttribute("href", url);
        link.setAttribute("download", fileName);
        link.style.visibility = "hidden";
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    },

    /**
     * Mở hộp thoại in trang hoặc lưu file PDF
     */
    printPage: function () {
        window.print();
    },

    /**
     * Cuộn tới phần tử khi màn hình hẹp (khung chi tiết nằm dưới danh sách thay vì bên cạnh).
     * @param {string} selector CSS selector của phần tử cần cuộn tới
     * @param {number} maxWidth Chỉ cuộn khi độ rộng cửa sổ nhỏ hơn hoặc bằng giá trị này (px)
     */
    scrollIntoViewOnNarrow: function (selector, maxWidth) {
        if (window.innerWidth > maxWidth) return;
        var el = document.querySelector(selector);
        if (!el) return;
        var reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        el.scrollIntoView({ behavior: reduce ? "auto" : "smooth", block: "start" });
    }
};

