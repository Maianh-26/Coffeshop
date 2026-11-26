import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'cart.dart';
import 'pages/about_page.dart';
import 'pages/cart_page.dart';
import 'pages/menu_page.dart';
import 'providers/menu_provider.dart';
import 'services/order_api.dart';
import 'theme.dart';
import 'widgets/header.dart';
import 'widgets/payment_dialog.dart';

void main() => runApp(const CoffeeApp());

class CoffeeApp extends StatelessWidget {
  const CoffeeApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        // Tải danh mục + món + topping từ API ngay khi mở app
        ChangeNotifierProvider(create: (_) => MenuProvider()..load()),
        ChangeNotifierProvider(create: (_) => CartStore()),
        // Gọi POST /don-hang khi thanh toán
        Provider(create: (_) => OrderApi()),
      ],
      child: MaterialApp(
        title: 'Coffee Shop',
        debugShowCheckedModeBanner: false,
        theme: buildTheme(),
        home: const HomeShell(),
      ),
    );
  }
}

class HomeShell extends StatefulWidget {
  const HomeShell({super.key});

  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  // Màn hình mở đầu: Danh mục
  AppPage _page = AppPage.menu;
  CartLine? _editing;

  /// Món cần mở ở Danh mục (khi bấm từ trang Giới thiệu)
  int? _focusProductId;

  void _navigate(AppPage p) => setState(() {
    if (p != AppPage.menu) _editing = null;
    _focusProductId = null;
    _page = p;
  });

  @override
  Widget build(BuildContext context) {
    final cartCount = context.select<CartStore, int>((c) => c.count);
    final phone = isPhone(context);
    return Scaffold(
      appBar: AppHeader(
        page: _page,
        onNavigate: _navigate,
        cartCount: cartCount,
        showTabs: !phone,
      ),
      bottomNavigationBar: phone
          ? AppBottomNav(
              page: _page,
              onNavigate: _navigate,
              cartCount: cartCount,
            )
          : null,
      body: AnimatedSwitcher(
        duration: const Duration(milliseconds: 180),
        child: KeyedSubtree(key: ValueKey(_page), child: _body()),
      ),
    );
  }

  /// QR / tiền mặt -> (server báo PAID) -> "Thanh toán thành công" / "Đã thanh toán bằng tiền mặt"
  /// -> bấm X -> về Danh mục
  Future<void> _checkout() async {
    final paid = await showPaymentDialog(context);
    if (paid == null || !mounted) return; // khách đóng popup / đơn bị hủy
    await showPaymentSuccess(context, paid);
    if (mounted) _navigate(AppPage.menu);
  }

  Widget _body() {
    switch (_page) {
      case AppPage.about:
        return AboutPage(
          onExplore: () => _navigate(AppPage.menu),
          onOpenProduct: (id) {
            context.read<MenuProvider>().focus(id);
            setState(() {
              _focusProductId = id;
              _page = AppPage.menu;
            });
          },
        );
      case AppPage.menu:
        return MenuPage(
          editing: _editing,
          onCancelEdit: () => setState(() => _editing = null),
          onUpdated: () => _navigate(AppPage.cart),
          focusProductId: _focusProductId,
        );
      case AppPage.cart:
        return CartPage(
          onEdit: (line) {
            context.read<MenuProvider>().focus(line.product.maTenMon);
            setState(() {
              _editing = line;
              _page = AppPage.menu;
            });
          },
          onContinue: () => _navigate(AppPage.menu),
          onCheckout: _checkout,
        );
    }
  }
}
