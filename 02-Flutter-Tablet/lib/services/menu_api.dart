import '../models/menu_models.dart';
import 'api_client.dart';

/// Các endpoint công khai cho tablet của khách (không cần token).
class MenuApi {
  final ApiClient _client;
  MenuApi({ApiClient? client}) : _client = client ?? ApiClient();

  /// GET /danh-muc
  Future<List<DanhMuc>> danhMuc() async {
    final list = await _client.get('/danh-muc') as List;
    return [for (final j in list) DanhMuc.fromJson(j as Map<String, dynamic>)];
  }

  /// GET /mon — Backend phân trang (limit tối đa 100) nên lấy lần lượt đến trang cuối.
  Future<List<Mon>> mon() async {
    final result = <Mon>[];
    var page = 1;
    while (true) {
      final res =
          await _client.get('/mon', {'page': page, 'limit': 100})
              as Map<String, dynamic>;
      result.addAll([
        for (final j in res['data'] as List)
          Mon.fromJson(j as Map<String, dynamic>),
      ]);
      final totalPages = (res['meta']?['totalPages'] as int?) ?? 1;
      if (page >= totalPages) break;
      page++;
    }
    return result;
  }

  /// GET /topping
  Future<List<ToppingItem>> topping() async {
    final list = await _client.get('/topping') as List;
    return [
      for (final j in list) ToppingItem.fromJson(j as Map<String, dynamic>),
    ];
  }
}
