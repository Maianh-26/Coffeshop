import 'package:flutter/foundation.dart';

import '../models/menu_models.dart';
import '../services/menu_api.dart';

enum LoadStatus { initial, loading, success, failure }

/// Trạng thái màn hình Home/Menu: dữ liệu tải từ API + bộ lọc (danh mục, tìm kiếm, trang).
class MenuProvider extends ChangeNotifier {
  final MenuApi _api;
  MenuProvider({MenuApi? api}) : _api = api ?? MenuApi();

  /// Số món mỗi trang ở mục "Tất cả"
  static const pageSize = 6;

  LoadStatus _status = LoadStatus.initial;
  String? _error;
  List<DanhMuc> _categories = const [];
  List<Mon> _products = const [];
  List<ToppingItem> _toppings = const [];
  Future<void>? _pending;

  int? _categoryId; // null = Tất cả
  String _query = '';
  int _page = 0;

  // ---------------------------------------------------------------- Dữ liệu
  LoadStatus get status => _status;
  String? get error => _error;
  bool get isLoading => _status == LoadStatus.loading;

  /// Lần tải đầu (chưa có dữ liệu) -> hiện skeleton toàn màn hình
  bool get isFirstLoad =>
      _products.isEmpty &&
      (_status == LoadStatus.initial || _status == LoadStatus.loading);
  List<DanhMuc> get categories => _categories;
  List<Mon> get products => _products;
  List<ToppingItem> get toppings =>
      _toppings.where((t) => t.conHang && !t.ngungBan).toList();

  Mon? findMon(int id) {
    for (final m in _products) {
      if (m.maTenMon == id) return m;
    }
    return null;
  }

  /// Gọi song song GET /danh-muc, /mon, /topping. Gọi lại khi đang tải -> dùng chung request.
  Future<void> load() =>
      _pending ??= _load().whenComplete(() => _pending = null);

  Future<void> _load() async {
    _status = LoadStatus.loading;
    _error = null;
    notifyListeners();
    try {
      final r = await Future.wait([_api.danhMuc(), _api.mon(), _api.topping()]);
      _categories = (r[0] as List<DanhMuc>).where((c) => c.soMon > 0).toList();
      _products = r[1] as List<Mon>;
      _toppings = r[2] as List<ToppingItem>;
      if (_categoryId != null &&
          !_categories.any((c) => c.maDanhMuc == _categoryId)) {
        _categoryId = null;
      }
      _status = LoadStatus.success;
    } catch (e) {
      _error = e.toString();
      _status = LoadStatus.failure;
    }
    notifyListeners();
  }

  // ---------------------------------------------------------------- Bộ lọc
  int? get categoryId => _categoryId;
  String get query => _query;

  void selectCategory(int? id) {
    _categoryId = id;
    _page = 0;
    notifyListeners();
  }

  void setQuery(String q) {
    _query = q;
    _page = 0;
    notifyListeners();
  }

  void setPage(int p) {
    _page = p;
    notifyListeners();
  }

  /// Món sau khi lọc danh mục + từ khóa
  List<Mon> get filtered {
    final q = _query.trim().toLowerCase();
    return _products
        .where(
          (m) =>
              (_categoryId == null || m.maDanhMuc == _categoryId) &&
              (q.isEmpty || m.tenMon.toLowerCase().contains(q)),
        )
        .toList();
  }

  /// Phân trang cho mọi danh mục (đổi danh mục / từ khóa -> về trang 1)
  int get pageCount => (filtered.length / pageSize).ceil();
  int get page => pageCount == 0 ? 0 : _page.clamp(0, pageCount - 1);

  List<Mon> get visible =>
      filtered.skip(page * pageSize).take(pageSize).toList();

  // ---------------------------------------------------------------- Trang Giới thiệu
  /// Số món mỗi trang ở trang Giới thiệu
  static const aboutPageSize = 6;
  int _aboutPage = 0;

  /// Món đang bán được (trang Giới thiệu chỉ giới thiệu món gọi được)
  List<Mon> get featured => _products.where((m) => m.coTheBan).toList();
  int get aboutPageCount => (featured.length / aboutPageSize).ceil();
  int get aboutPage =>
      aboutPageCount == 0 ? 0 : _aboutPage.clamp(0, aboutPageCount - 1);
  List<Mon> get aboutVisible =>
      featured.skip(aboutPage * aboutPageSize).take(aboutPageSize).toList();

  void setAboutPage(int p) {
    _aboutPage = p;
    notifyListeners();
  }

  /// Mở 1 món từ trang khác: về "Tất cả", bỏ tìm kiếm, nhảy tới trang chứa món
  void focus(int maTenMon) {
    _categoryId = null;
    _query = '';
    final i = _products.indexWhere((m) => m.maTenMon == maTenMon);
    _page = i < 0 ? 0 : i ~/ pageSize;
    notifyListeners();
  }
}
