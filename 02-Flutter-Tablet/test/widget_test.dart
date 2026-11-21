import 'dart:async';

import 'package:coffee_app/cart.dart';
import 'package:coffee_app/data.dart';
import 'package:coffee_app/models/menu_models.dart';
import 'package:coffee_app/pages/menu_page.dart';
import 'package:coffee_app/providers/menu_provider.dart';
import 'package:coffee_app/services/api_client.dart';
import 'package:coffee_app/services/menu_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

/// JSON đúng như README / openapi.yaml của CoffeeShop.Api
final bacXiuJson = <String, dynamic>{
  'maTenMon': 3,
  'maDanhMuc': 1,
  'tenDanhMuc': 'Cà phê',
  'tenMon': 'Bạc xỉu',
  'hinhAnh': null,
  'conHang': true,
  'ngungBan': false,
  'coTheBan': true,
  'khuyenMai': {
    'maKhuyenMai': 2,
    'code': 'KHAITRUONG10',
    'phanTramGiam': 10,
    'dangApDung': true,
    'gioBatDau': null,
    'gioKetThuc': null,
  },
  'kichThuoc': [
    {
      'maChiTietMon': 6,
      'maKichThuoc': 'M',
      'tenKichThuoc': 'Vừa',
      'giaBan': 32000,
      'giaSauGiam': 28800,
      'dangApDung': true,
      'duNguyenLieu': true,
    },
    {
      'maChiTietMon': 7,
      'maKichThuoc': 'L',
      'tenKichThuoc': 'Lớn',
      'giaBan': 38000,
      'giaSauGiam': 34200,
      'dangApDung': true,
      'duNguyenLieu': true,
    },
  ],
};

final bacXiu = Mon.fromJson(bacXiuJson);
const espresso = ToppingItem(
  maTopping: 3,
  tenTopping: 'Thêm shot espresso',
  giaThem: 10000,
);

class FakeMenuApi extends MenuApi {
  final Completer<void> gate = Completer();
  bool fail = false;
  List<Mon> mons = [bacXiu];

  Future<void> _wait() async {
    await gate.future;
    if (fail) throw const ApiException('NETWORK', 'Mất kết nối');
  }

  @override
  Future<List<DanhMuc>> danhMuc() async {
    await _wait();
    return const [DanhMuc(maDanhMuc: 1, tenDanhMuc: 'Cà phê', soMon: 1)];
  }

  @override
  Future<List<Mon>> mon() async {
    await _wait();
    return mons;
  }

  @override
  Future<List<ToppingItem>> topping() async {
    await _wait();
    return const [espresso];
  }
}

void main() {
  test('Định dạng giá VND', () {
    expect(formatVnd(149000), '149.000đ');
    expect(formatVnd(28800), '28.800đ');
  });

  test('Đọc JSON món từ API', () {
    expect(bacXiu.tenMon, 'Bạc xỉu');
    expect(bacXiu.discount, 10);
    expect(bacXiu.defaultSize!.maChiTietMon, 6);
    expect(bacXiu.sizeByCode('L')!.giaSauGiam, 34200);
    expect(bacXiu.image, ''); // hinhAnh null -> ảnh mặc định
  });

  test('Giỏ hàng tính giá theo giaSauGiam của API (khớp ví dụ đặt món)', () {
    final cart = CartStore();
    cart.add(
      CartLine(
        product: bacXiu,
        size: bacXiu.sizeByCode('L')!,
        ice: '50%',
        sugar: '50%',
        toppings: const [espresso],
        qty: 2,
      ),
    );
    // README: (34200 + 10000) × 2 = 88400
    expect(cart.total, 88400);
    expect(cart.subtotal, (38000 + 10000) * 2);
    expect(cart.promoCodes, ['KHAITRUONG10']);
    expect(cart.lines.first.toOrderJson(), {
      'maChiTietMon': 7,
      'soLuong': 2,
      'ghiChu': 'Đá 50%, đường 50%',
      'toppings': [3],
    });
  });

  test('MenuProvider: loading -> success', () async {
    final api = FakeMenuApi();
    final menu = MenuProvider(api: api);
    final f = menu.load();
    expect(menu.status, LoadStatus.loading);
    expect(menu.isFirstLoad, true);
    api.gate.complete();
    await f;
    expect(menu.status, LoadStatus.success);
    expect(menu.products.single.tenMon, 'Bạc xỉu');
    expect(menu.categories.single.tenDanhMuc, 'Cà phê');
  });

  test('MenuProvider: lỗi mạng -> failure kèm thông báo', () async {
    final api = FakeMenuApi()..fail = true;
    final menu = MenuProvider(api: api);
    api.gate.complete();
    await menu.load();
    expect(menu.status, LoadStatus.failure);
    expect(menu.error, 'Mất kết nối');
  });

  testWidgets('Menu hiện skeleton khi chờ API, sau đó hiện món', (t) async {
    final api = FakeMenuApi();
    final menu = MenuProvider(api: api);
    unawaited(menu.load());
    await t.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider.value(value: menu),
          ChangeNotifierProvider(create: (_) => CartStore()),
        ],
        child: MaterialApp(
          home: Scaffold(
            body: MenuPage(
              editing: null,
              onCancelEdit: () {},
              onUpdated: () {},
            ),
          ),
        ),
      ),
    );
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    expect(find.text('Bạc xỉu'), findsNothing);

    api.gate.complete();
    await t.pump();
    await t.pump();
    expect(find.byType(CircularProgressIndicator), findsNothing);
    expect(find.text('Bạc xỉu'), findsOneWidget);
    expect(find.text('Cà phê'), findsOneWidget);
  });
  test('Phân trang theo danh mục và trang Giới thiệu', () async {
    // Danh mục 1: 8 món, danh mục 2: 3 món (1 món hết hàng)
    final api = FakeMenuApi()
      ..mons = [
        for (var i = 1; i <= 8; i++) fakeMon(i, 1),
        fakeMon(9, 2),
        fakeMon(10, 2),
        fakeMon(11, 2, coTheBan: false),
      ];
    final menu = MenuProvider(api: api);
    api.gate.complete();
    await menu.load();

    // Tất cả: 11 món -> 2 trang (6 + 5)
    expect(menu.pageCount, 2);
    menu.setPage(1);
    expect(menu.visible.length, 5);

    // Danh mục 1: 8 món -> 2 trang, đổi danh mục thì về trang 1
    menu.selectCategory(1);
    expect(menu.pageCount, 2);
    expect(menu.page, 0);
    expect(menu.visible.length, 6);
    menu.setPage(1);
    expect(menu.visible.map((m) => m.maTenMon), [7, 8]);

    // Danh mục 2: 3 món -> 1 trang
    menu.selectCategory(2);
    expect(menu.pageCount, 1);

    // Giới thiệu: 10 món bán được -> 2 trang (6 + 4)
    expect(menu.aboutPageCount, 2);
    menu.setAboutPage(1);
    expect(menu.aboutVisible.length, 4);
  });
}

Mon fakeMon(int id, int danhMuc, {bool coTheBan = true}) => Mon.fromJson({
  ...bacXiuJson,
  'maTenMon': id,
  'maDanhMuc': danhMuc,
  'tenMon': 'Món $id',
  'coTheBan': coTheBan,
});
