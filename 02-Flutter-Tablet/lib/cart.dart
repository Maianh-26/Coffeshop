import 'package:flutter/foundation.dart';

import 'models/menu_models.dart';
import 'services/order_api.dart';

class CartLine {
  /// Ảnh chụp món + size + topping tại lúc thêm vào giỏ (giống TenMonLuu / DonGiaBan của đơn hàng)
  final Mon product;
  final GiaSize size;
  final String ice;
  final String sugar;

  /// Topping đã chọn (theo thứ tự danh sách từ API)
  final List<ToppingItem> toppings;
  final int qty;

  const CartLine({
    required this.product,
    required this.size,
    required this.ice,
    required this.sugar,
    this.toppings = const [],
    required this.qty,
  });

  String get key =>
      '${size.maChiTietMon}|$ice|$sugar|${toppings.map((t) => t.maTopping).join(',')}';

  int get toppingTotal => toppings.fold(0, (s, t) => s + t.giaThem);

  /// Giá 1 phần sau khuyến mãi, gồm topping
  int get price => size.giaSauGiam + toppingTotal;

  /// Giá 1 phần gốc, gồm topping (topping không giảm giá)
  int get oldPrice => size.giaBan + toppingTotal;

  /// Đá/đường không có cột riêng trong CSDL -> ghi vào GhiChu
  String? get ghiChu => product.isDrink ? 'Đá $ice, đường $sugar' : null;

  /// Một dòng của POST /don-hang (DongDatMonRequest) — dùng khi nối API đặt món
  Map<String, dynamic> toOrderJson() => {
    'maChiTietMon': size.maChiTietMon,
    'soLuong': qty,
    if (ghiChu != null) 'ghiChu': ghiChu,
    if (toppings.isNotEmpty)
      'toppings': [for (final t in toppings) t.maTopping],
  };

  CartLine copyWith({int? qty}) => CartLine(
    product: product,
    size: size,
    ice: ice,
    sugar: sugar,
    toppings: toppings,
    qty: qty ?? this.qty,
  );
}

/// Trạng thái giỏ hàng dùng chung cho toàn app (cung cấp qua Provider).
class CartStore extends ChangeNotifier {
  final List<CartLine> _lines = [];

  List<CartLine> get lines => List.unmodifiable(_lines);
  int get count => _lines.fold(0, (s, l) => s + l.qty);
  int get subtotal => _lines.fold(0, (s, l) => s + l.oldPrice * l.qty);
  int get total => _lines.fold(0, (s, l) => s + l.price * l.qty);
  int get saved => subtotal - total;
  int get maxDiscount =>
      _lines.fold(0, (m, l) => l.product.discount > m ? l.product.discount : m);

  /// Mã khuyến mãi đang được áp dụng cho các món trong giỏ
  List<String> get promoCodes => {
    for (final l in _lines)
      if (l.product.discount > 0) l.product.khuyenMai!.code,
  }.toList();

  // ---------------------------------------------------------------- Cách thanh toán
  /// Khách chọn trong giỏ: chuyển khoản QR (mặc định) hoặc tiền mặt tại quầy
  PhuongThuc _phuongThuc = PhuongThuc.qr;
  PhuongThuc get phuongThuc => _phuongThuc;

  set phuongThuc(PhuongThuc pt) {
    if (pt == _phuongThuc) return;
    _phuongThuc = pt;
    notifyListeners();
  }

  // ---------------------------------------------------------------- Đơn hàng trên server
  DonHang? _order;
  String? _orderSig;

  /// Dấu vết nội dung giỏ: đổi món / số lượng -> đơn cũ không còn đúng
  String get _sig => _lines.map((l) => '${l.key}x${l.qty}').join(';');

  /// Đơn đã tạo cho ĐÚNG giỏ hiện tại (để mở lại không sinh thêm đơn trùng)
  DonHang? get pendingOrder => _orderSig == _sig ? _order : null;

  /// Tạo đơn trên server (POST /don-hang) theo cách thanh toán đang chọn.
  /// Giỏ không đổi mà khách đổi QR <-> tiền mặt -> dùng lại đơn cũ, chỉ đổi phương thức.
  Future<DonHang> checkout(OrderApi api) async {
    final pt = _phuongThuc;
    final existing = pendingOrder;
    if (existing != null) {
      if (existing.phuongThuc == pt) return existing;
      final st = await api.doiPhuongThuc(existing.maDonHang, pt);
      return _order = existing.copyWith(
        trangThaiThanhToan: st.trangThai,
        phuongThuc: st.phuongThuc,
      );
    }
    final sig = _sig;
    final order = await api.datMon([
      for (final l in _lines) l.toOrderJson(),
    ], pt);
    _order = order;
    _orderSig = sig;
    return order;
  }

  void _merge(CartLine line) {
    final i = _lines.indexWhere((l) => l.key == line.key);
    if (i >= 0) {
      _lines[i] = _lines[i].copyWith(qty: _lines[i].qty + line.qty);
    } else {
      _lines.add(line);
    }
  }

  void add(CartLine line) {
    _merge(line);
    notifyListeners();
  }

  void replace(String oldKey, CartLine line) {
    _lines.removeWhere((l) => l.key == oldKey);
    _merge(line);
    notifyListeners();
  }

  void setQty(String key, int qty) {
    final i = _lines.indexWhere((l) => l.key == key);
    if (i < 0) return;
    _lines[i] = _lines[i].copyWith(qty: qty);
    notifyListeners();
  }

  void remove(String key) {
    _lines.removeWhere((l) => l.key == key);
    notifyListeners();
  }

  void clear() {
    _lines.clear();
    _order = null;
    _orderSig = null;
    notifyListeners();
  }
}
