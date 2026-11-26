import 'package:flutter/material.dart';

import '../theme.dart';
import 'common.dart';

enum AppPage { menu, cart, about }

class NavTab {
  final AppPage page;
  final String label;
  final IconData icon;
  const NavTab(this.page, this.label, this.icon);
}

const navTabs = [
  NavTab(AppPage.menu, 'Danh mục', Icons.local_cafe_outlined),
  NavTab(AppPage.cart, 'Giỏ hàng', Icons.shopping_bag_outlined),
  NavTab(AppPage.about, 'Giới thiệu', Icons.coffee_outlined),
];

/// Thanh header nâu. Trên màn rộng hiện tab ở giữa; trên điện thoại tab chuyển xuống bottom bar.
class AppHeader extends StatelessWidget implements PreferredSizeWidget {
  final AppPage page;
  final ValueChanged<AppPage> onNavigate;
  final int cartCount;
  final bool showTabs;

  const AppHeader({
    super.key,
    required this.page,
    required this.onNavigate,
    required this.cartCount,
    required this.showTabs,
  });

  @override
  Size get preferredSize => const Size.fromHeight(60);

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.brown900,
      child: SafeArea(
        bottom: false,
        child: SizedBox(
          height: 60,
          child: Padding(
            padding: EdgeInsets.symmetric(
              horizontal: isPhone(context) ? 16 : 24,
            ),
            child: Row(
              children: [
                InkWell(
                  onTap: () => onNavigate(AppPage.about),
                  child: Row(
                    children: [
                      const BrandLogo(size: 40),
                      const SizedBox(width: 10),
                      Text(
                        'Coffee Shop',
                        style: serif(size: 19, color: Colors.white),
                      ),
                    ],
                  ),
                ),
                Expanded(
                  child: showTabs
                      ? Row(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            for (final t in navTabs)
                              Padding(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 3,
                                ),
                                child: _HeaderTab(
                                  tab: t,
                                  active: page == t.page,
                                  badge: t.page == AppPage.cart ? cartCount : 0,
                                  onTap: () => onNavigate(t.page),
                                ),
                              ),
                          ],
                        )
                      : const SizedBox(),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _HeaderTab extends StatelessWidget {
  final NavTab tab;
  final bool active;
  final int badge;
  final VoidCallback onTap;

  const _HeaderTab({
    required this.tab,
    required this.active,
    required this.badge,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final fg = active ? AppColors.brown900 : AppColors.cream200;
    return Material(
      color: active ? AppColors.cream100 : Colors.transparent,
      borderRadius: BorderRadius.circular(8),
      child: InkWell(
        borderRadius: BorderRadius.circular(8),
        hoverColor: Colors.white.withValues(alpha: 0.08),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
          child: Row(
            children: [
              Icon(tab.icon, size: 16, color: fg),
              const SizedBox(width: 7),
              Text(
                tab.label,
                style: TextStyle(color: fg, fontWeight: FontWeight.w500),
              ),
              if (badge > 0) ...[
                const SizedBox(width: 6),
                Text(
                  '$badge',
                  style: TextStyle(color: fg, fontWeight: FontWeight.w700),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// Bottom navigation cho điện thoại.
class AppBottomNav extends StatelessWidget {
  final AppPage page;
  final ValueChanged<AppPage> onNavigate;
  final int cartCount;

  const AppBottomNav({
    super.key,
    required this.page,
    required this.onNavigate,
    required this.cartCount,
  });

  @override
  Widget build(BuildContext context) {
    return NavigationBar(
      backgroundColor: Colors.white,
      indicatorColor: AppColors.cream200,
      height: 64,
      selectedIndex: navTabs.indexWhere((t) => t.page == page),
      onDestinationSelected: (i) => onNavigate(navTabs[i].page),
      destinations: [
        for (final t in navTabs)
          NavigationDestination(
            label: t.label,
            icon: t.page == AppPage.cart && cartCount > 0
                ? Badge(
                    label: Text('$cartCount'),
                    backgroundColor: AppColors.red,
                    child: Icon(t.icon),
                  )
                : Icon(t.icon),
          ),
      ],
    );
  }
}
