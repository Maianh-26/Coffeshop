import 'package:coffee_app/cart.dart';
import 'package:coffee_app/main.dart';
import 'package:coffee_app/pages/cart_page.dart';
import 'package:coffee_app/pages/menu_page.dart';
import 'package:coffee_app/providers/menu_provider.dart';
import 'package:coffee_app/services/order_api.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'widget_test.dart' show FakeMenuApi, bacXiu;

/// Server giả: đơn PENDING, đến khi [paidBy] != null thì trả PAID bằng cách đó
class FakeOrderApi extends OrderApi {
  PhuongThuc? paidBy;
  PhuongThuc? createdWith;
  int polls = 0;

  @override
  Future<DonHang> datMon(
    List<Map<String, dynamic>> chiTiet,
    PhuongThuc phuongThuc,
  ) async {
    createdWith = phuongThuc;
    return DonHang(
      maDonHang: 7,
      tongTien: 57600,
      trangThaiThanhToan: 'PENDING',
      phuongThuc: phuongThuc,
    );
  }

  @override
  Future<TrangThaiDon> trangThai(int maDonHang) async {
    polls++;
    final by = paidBy;
    return by == null
        ? TrangThaiDon('PENDING', createdWith ?? PhuongThuc.qr)
        : TrangThaiDon('PAID', by);
  }
}

class _Setup {
  final FakeOrderApi orderApi;
  final CartStore cart;
  _Setup(this.orderApi, this.cart);
}

Future<_Setup> _openCart(WidgetTester t) async {
  t.view.physicalSize = const Size(1400, 1000);
  t.view.devicePixelRatio = 1;
  addTearDown(t.view.reset);
  // Font giả của flutter_test (mỗi ký tự là 1 ô vuông) rộng hơn font thật -> bỏ qua cảnh báo tràn layout
  final onError = FlutterError.onError;
  FlutterError.onError = (d) {
    if (d.exceptionAsString().contains('overflowed')) return;
    onError?.call(d);
  };
  addTearDown(() => FlutterError.onError = onError);

  final menuApi = FakeMenuApi()..gate.complete();
  final orderApi = FakeOrderApi();
  final cart = CartStore()
    ..add(
      CartLine(
        product: bacXiu,
        size: bacXiu.defaultSize!,
        ice: '50%',
        sugar: '50%',
        qty: 2,
      ),
    );

  await t.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider(
          create: (_) => MenuProvider(api: menuApi)..load(),
        ),
        ChangeNotifierProvider.value(value: cart),
        Provider<OrderApi>.value(value: orderApi),
      ],
      child: const MaterialApp(home: HomeShell()),
    ),
  );
  await t.pumpAndSettle();

  await t.tap(find.text('Giỏ hàng').first);
  await t.pumpAndSettle();
  expect(find.byType(CartPage), findsOneWidget);
  return _Setup(orderApi, cart);
}

void main() {
  testWidgets(
    'QR -> server báo PAID -> Thanh toán thành công -> X -> Danh mục',
    (t) async {
      final s = await _openCart(t);

      // Mặc định chọn QR -> nút "Thanh toán"
      await t.tap(find.textContaining('Thanh toán ·'));
      await t.pump();
      await t.pump(const Duration(milliseconds: 300));
      expect(s.orderApi.createdWith, PhuongThuc.qr);

      // Đang hiện QR + chờ xác nhận, không còn nút "Tôi đã chuyển khoản"
      expect(find.textContaining('DH7', findRichText: true), findsOneWidget);
      expect(find.text('Đang chờ xác nhận thanh toán...'), findsOneWidget);
      expect(find.text('Tôi đã chuyển khoản'), findsNothing);

      // Chưa trả tiền: hỏi server vẫn PENDING -> QR vẫn hiện
      await t.pump(const Duration(seconds: 3));
      expect(s.orderApi.polls, 1);
      expect(find.text('Thanh toán thành công'), findsNothing);

      // Webhook ngân hàng xác nhận -> lần hỏi kế tiếp thấy PAID
      s.orderApi.paidBy = PhuongThuc.qr;
      await t.pump(const Duration(seconds: 3));
      await t.pumpAndSettle();
      expect(find.text('Thanh toán thành công'), findsOneWidget);
      expect(find.text('Đơn DH7 · 57.600đ'), findsOneWidget);
      expect(s.cart.lines, isEmpty);

      // Bấm X -> về trang Danh mục
      await t.tap(find.byTooltip('Đóng'));
      await t.pumpAndSettle();
      expect(find.text('Thanh toán thành công'), findsNothing);
      expect(find.byType(MenuPage), findsOneWidget);
    },
  );

  testWidgets(
    'Tiền mặt -> chờ tại quầy -> admin xác nhận -> Đã thanh toán bằng tiền mặt',
    (t) async {
      final s = await _openCart(t);

      // Chọn Tiền mặt -> nút đổi thành "Đặt món"
      await t.tap(find.text('Tiền mặt'));
      await t.pump();
      expect(find.textContaining('Thanh toán ·'), findsNothing);
      await t.tap(find.textContaining('Đặt món ·'));
      await t.pump();
      await t.pump(const Duration(milliseconds: 300));
      expect(s.orderApi.createdWith, PhuongThuc.cash);

      // Không có QR, hiện mã đơn để đọc cho thu ngân
      expect(find.text('Mời bạn thanh toán tiền mặt tại quầy'), findsOneWidget);
      expect(find.text('DH7'), findsOneWidget);
      expect(
        find.text('Đang chờ nhân viên xác nhận đã thu tiền...'),
        findsOneWidget,
      );

      await t.pump(const Duration(seconds: 3));
      expect(find.text('Đã thanh toán bằng tiền mặt'), findsNothing);

      // Web admin bấm "Xác nhận đã thanh toán tiền mặt"
      s.orderApi.paidBy = PhuongThuc.cash;
      await t.pump(const Duration(seconds: 3));
      await t.pumpAndSettle();
      expect(find.text('Đã thanh toán bằng tiền mặt'), findsOneWidget);
      expect(find.text('Tiền mặt tại quầy'), findsOneWidget);
      expect(s.cart.lines, isEmpty);

      await t.tap(find.byTooltip('Đóng'));
      await t.pumpAndSettle();
      expect(find.byType(MenuPage), findsOneWidget);
    },
  );
}
