// CoffeeShop.Admin JavaScript Helper Utilities

window.appHelpers = {
    /**
     * Tải một file (ví dụ báo cáo Excel .xlsx) về máy người dùng.
     * @param {string} fileName Tên file
     * @param {Uint8Array} bytes Nội dung (Blazor gửi byte[] dưới dạng Uint8Array)
     * @param {string} mimeType Kiểu file
     */
    downloadFile: function (fileName, bytes, mimeType) {
        var blob = new Blob([bytes], { type: mimeType });
        var url = URL.createObjectURL(blob);
        var link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        link.style.display = "none";
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        // Thu hồi sau một nhịp để trình duyệt kịp bắt đầu tải
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    },

    /**
     * Dựng hóa đơn trong một iframe ẩn và tính khổ giấy (chưa in). Tách riêng để kiểm thử được.
     *
     * CHUẨN KHỔ GIẤY — giấy in nhiệt dạng cuộn của máy in hóa đơn:
     *  - K80 (mặc định): rộng 80mm, vùng in được 72mm (~48 ký tự/dòng font chuẩn). K58: rộng 58mm, vùng in 48mm.
     *  - Rộng cố định theo máy; DÀI không cố định (cuộn liên tục, in xong máy tự cắt) → đo chiều cao thật của
     *    hóa đơn rồi đặt @page đúng "rộng × cao" → mỗi hóa đơn là MỘT trang liền, đơn nhiều món thì dài ra.
     *    ("size: 80mm auto" là cú pháp không hợp lệ — trình duyệt bỏ qua cả dòng và in ra A4.)
     *
     * KHÔNG IN DỮ LIỆU THỪA: Chrome tự in header/footer (ngày giờ, tiêu đề trang, đường dẫn https) vào vùng LỀ trang.
     * Đặt @page margin: 0 thì không còn chỗ cho header/footer; lề thật của hóa đơn chuyển vào padding của <body>.
     * Tài liệu in cũng không có <title>.
     *
     * @param {string} selector CSS selector của phần tử chứa mẫu hóa đơn
     * @param {number} paperWidthMm 80 (K80) hoặc 58 (K58)
     * @returns {{frame: HTMLIFrameElement, paperWidthMm: number, contentWidthMm: number, heightMm: number}}
     */
    buildReceiptFrame: function (selector, paperWidthMm) {
        var source = document.querySelector(selector);
        if (!source) throw new Error("Không tìm thấy mẫu hóa đơn: " + selector);

        var paper = paperWidthMm === 58 ? 58 : 80;
        var content = paper === 58 ? 48 : 72;       // vùng in được của máy in nhiệt
        var side = (paper - content) / 2;           // lề trái/phải → nội dung nằm giữa vùng in
        var top = 2, bottom = 8;                     // lề trên; lề dưới chừa chỗ cho dao cắt giấy
        var base = paper === 58 ? 11 : 12;           // cỡ chữ (px) — K58 hẹp hơn nên nhỏ hơn 1px
        // Cột số của bảng món (mm): SL | Đ.GIÁ | T.TIỀN; cột TÊN HÀNG lấy phần còn lại
        var colQty = paper === 58 ? 5 : 7, colPrice = paper === 58 ? 13 : 15, colTotal = paper === 58 ? 15 : 16;

        var css =
            "@page { margin: 0; }" +
            "* { box-sizing: border-box; }" +
            "html { margin: 0; padding: 0; }" +
            "body { margin: 0; width: " + paper + "mm; padding: " + top + "mm " + side + "mm " + bottom + "mm;" +
            " font: " + base + "px/1.5 Arial, 'Segoe UI', sans-serif; color: #000; overflow-wrap: anywhere;" +
            " -webkit-print-color-adjust: exact; print-color-adjust: exact; }" +
            ".r-center { text-align: center; }" +
            ".r-right { text-align: right; }" +
            ".r-head { margin-bottom: 8px; }" +
            ".r-shop { font-size: " + (base + 4) + "px; font-weight: 700; text-transform: uppercase; margin-bottom: 2px; }" +
            ".r-small { font-size: " + (base - 1) + "px; }" +
            ".r-title { font-size: " + (base + 3) + "px; font-weight: 700; margin-top: 6px; }" +
            "table { width: 100%; border-collapse: collapse; table-layout: fixed; }" +
            "td, th { padding: 1px 0; vertical-align: top; }" +
            ".r-info { margin: 2px 0 6px; }" +
            ".r-info td { padding-bottom: 2px; }" +
            (paper === 58 ? ".r-info td { white-space: normal; }" : ".r-info td { white-space: nowrap; }") +
            ".r-items .c-qty { width: " + colQty + "mm; }" +
            ".r-items .c-price { width: " + colPrice + "mm; }" +
            ".r-items .c-total { width: " + colTotal + "mm; }" +
            ".r-items thead th { text-align: left; font-weight: 700; border-top: 1px dashed #000; border-bottom: 1px dashed #000; padding: 3px 0; }" +
            // Tên hàng cách cột số một khoảng; SL căn giữa, tiền căn phải
            ".r-items td:first-child, .r-items th:first-child { padding-right: " + (paper === 58 ? 1 : 2) + "mm; }" +
            ".r-items .r-num { text-align: right; white-space: nowrap; font-variant-numeric: tabular-nums; padding-left: 2px; }" +
            ".r-items thead th.r-num { text-align: right; }" +
            ".r-items .r-num:nth-child(2) { text-align: center; padding-left: 0; }" +
            (paper === 58 ? ".r-items .r-num { font-size: " + (base - 1) + "px; }" : "") +
            ".r-line td { padding-top: 6px; }" +
            ".r-topping td { padding-top: 2px; }" +
            ".r-line, .r-topping, .r-note { break-inside: avoid; page-break-inside: avoid; }" +
            ".r-topping td:first-child { padding-left: 8px; }" +
            ".r-topping td { font-size: " + (base - 1) + "px; }" +
            ".r-note td { padding: 2px 0 2px 8px; font-style: italic; font-size: " + (base - 1) + "px; }" +
            ".r-sum td { font-weight: 700; border-top: 1px dashed #000; padding: 6px 0 4px; }" +
            ".r-row { display: flex; justify-content: space-between; align-items: baseline; gap: 8px; }" +
            ".r-money { white-space: nowrap; font-variant-numeric: tabular-nums; }" +
            ".r-total { font-size: " + (base + 4) + "px; font-weight: 700; margin-top: 6px; }" +
            ".r-words { font-style: italic; font-size: " + (base - 1) + "px; margin: 2px 0 6px; }" +
            ".r-paid { font-weight: 700; }" +
            ".r-thanks { margin-top: 10px; padding-top: 6px; border-top: 1px dashed #000; font-style: italic; }";

        var frame = document.createElement("iframe");
        frame.setAttribute("aria-hidden", "true");
        frame.setAttribute("tabindex", "-1");
        // Iframe rộng đúng khổ giấy để bố cục như khi in, nhưng nằm ngoài màn hình
        frame.style.cssText = "position:fixed;left:-10000px;top:0;width:" + paper + "mm;height:10px;border:0;";
        document.body.appendChild(frame);

        try {
            var doc = frame.contentWindow.document;
            doc.open();
            doc.write("<!doctype html><html lang='vi'><head><meta charset='utf-8'><title></title><style>" +
                css + "</style></head><body>" + source.innerHTML + "</body></html>");
            doc.close();

            // Chiều cao hóa đơn (đã gồm lề trong body): px CSS → mm (96px = 25,4mm), làm tròn lên để không mất dòng cuối
            var heightPx = Math.ceil(doc.body.getBoundingClientRect().height);
            var heightMm = Math.ceil(heightPx * 25.4 / 96);

            var page = doc.createElement("style");
            page.textContent = "@page { size: " + paper + "mm " + heightMm + "mm; margin: 0; }";
            doc.head.appendChild(page);

            return { frame: frame, paperWidthMm: paper, contentWidthMm: content, heightMm: heightMm };
        } catch (e) {
            frame.remove();   // lỗi khi dựng hóa đơn → không để lại iframe ẩn
            throw e;
        }
    },

    /**
     * In riêng hóa đơn cho khách (giấy nhiệt K80/K58) — chỉ in nội dung hóa đơn, không in giao diện trang.
     * @param {string} selector CSS selector của phần tử chứa mẫu hóa đơn
     * @param {number} paperWidthMm 80 (mặc định) hoặc 58
     */
    printReceipt: function (selector, paperWidthMm) {
        var built = window.appHelpers.buildReceiptFrame(selector, paperWidthMm);
        var frame = built.frame;

        var cleanup = function () {
            setTimeout(function () { if (frame.isConnected) frame.remove(); }, 500);
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

