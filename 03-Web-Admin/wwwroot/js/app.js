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
     * In riêng hóa đơn cho khách (khổ giấy nhiệt 80mm) qua một iframe ẩn —
     * chỉ in nội dung hóa đơn, không in giao diện trang / nút bấm.
     * @param {string} selector CSS selector của phần tử chứa mẫu hóa đơn
     */
    printReceipt: function (selector) {
        var source = document.querySelector(selector);
        if (!source) throw new Error("Không tìm thấy mẫu hóa đơn: " + selector);

        var css =
            "@page { size: 80mm auto; margin: 4mm; }" +
            "* { box-sizing: border-box; }" +
            "body { margin: 0; width: 72mm; font: 12px/1.4 'Segoe UI', Arial, sans-serif; color: #000; }" +
            ".r-center { text-align: center; }" +
            ".r-shop { font-size: 16px; font-weight: 700; }" +
            ".r-title { font-size: 13px; font-weight: 700; margin: 2px 0 8px; }" +
            ".r-row { display: flex; justify-content: space-between; gap: 8px; }" +
            ".r-sub { font-size: 11px; padding-left: 6px; }" +
            ".r-item { margin-bottom: 6px; }" +
            ".r-total { font-size: 14px; font-weight: 700; margin: 4px 0; }" +
            ".r-thanks { margin-top: 10px; font-style: italic; }" +
            "hr { border: 0; border-top: 1px dashed #000; margin: 6px 0; }";

        var frame = document.createElement("iframe");
        frame.setAttribute("aria-hidden", "true");
        frame.style.position = "fixed";
        frame.style.width = "0";
        frame.style.height = "0";
        frame.style.border = "0";
        frame.style.right = "0";
        frame.style.bottom = "0";
        document.body.appendChild(frame);

        var doc = frame.contentWindow.document;
        doc.open();
        doc.write("<!doctype html><html lang='vi'><head><meta charset='utf-8'><title>Hóa đơn</title><style>" +
            css + "</style></head><body>" + source.innerHTML + "</body></html>");
        doc.close();

        var cleanup = function () {
            setTimeout(function () { frame.remove(); }, 500);
        };
        frame.contentWindow.onafterprint = cleanup;
        frame.contentWindow.focus();
        frame.contentWindow.print();
        // Một số trình duyệt không bắn afterprint trong iframe → dọn sau 1 phút
        setTimeout(function () { if (frame.isConnected) frame.remove(); }, 60000);
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

