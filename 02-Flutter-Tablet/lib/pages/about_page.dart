import 'package:flutter/material.dart';

import 'package:provider/provider.dart';

import '../data.dart';
import '../models/menu_models.dart';
import '../providers/menu_provider.dart';
import '../theme.dart';
import '../widgets/common.dart';

class AboutPage extends StatelessWidget {
  final VoidCallback onExplore;

  /// Bấm vào một món -> mở món đó ở Danh mục
  final ValueChanged<int> onOpenProduct;

  const AboutPage({
    super.key,
    required this.onExplore,
    required this.onOpenProduct,
  });

  @override
  Widget build(BuildContext context) {
    final phone = isPhone(context);

    final heroText = Padding(
      padding: EdgeInsets.all(phone ? 20 : 32),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(
            'Đồ uống đặc trưng,\nnguyên liệu chọn lọc,\nhương vị thật.',
            style: serif(
              size: phone ? 26 : 32,
              weight: FontWeight.w500,
              height: 1.25,
            ),
          ),
          const SizedBox(height: 14),
          const Text(
            'Chúng mình chọn lọc hạt cà phê chất lượng, rang theo mẻ và pha chế bằng cả sự tỉ mỉ. '
            'Từ nguyên liệu đến cách pha đều được chăm chút để mỗi tách cà phê mang đến hương vị đặc trưng '
            'và trải nghiệm thật sự thư giãn.',
            style: TextStyle(
              color: AppColors.muted,
              fontSize: 13,
              height: 1.55,
            ),
          ),
          const SizedBox(height: 18),
          DarkButton(
            label: 'Khám phá đồ uống',
            icon: Icons.arrow_forward,
            onPressed: onExplore,
          ),
        ],
      ),
    );

    final heroImage = Stack(
      fit: StackFit.expand,
      children: [
        NetImage(aboutHeroImage, radius: BorderRadius.circular(16)),
        Positioned(
          left: 16,
          bottom: 16,
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(999),
            ),
            child: const Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(Icons.coffee_outlined, size: 14),
                SizedBox(width: 6),
                Text(
                  'Đồ uống đặc trưng · Nguyên liệu chọn lọc',
                  style: TextStyle(fontSize: 12, fontWeight: FontWeight.w500),
                ),
              ],
            ),
          ),
        ),
      ],
    );

    final hero = Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: AppColors.cream200,
        borderRadius: BorderRadius.circular(16),
      ),
      child: phone
          ? Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                heroText,
                SizedBox(height: 220, child: heroImage),
              ],
            )
          : SizedBox(
              height: 340,
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Expanded(flex: 4, child: heroText),
                  Expanded(flex: 5, child: heroImage),
                ],
              ),
            ),
    );

    final cols = phone ? 1 : (isWide(context) ? 3 : 2);
    final menu = context.watch<MenuProvider>();
    // Món đang bán được (lấy từ API) của trang hiện tại
    final cards = [
      for (final f in menu.aboutVisible)
        _FeaturedCard(f, onTap: () => onOpenProduct(f.maTenMon)),
    ];

    return PageBody(
      children: [
        Text(
          'Mỗi tách cà phê đều được chăm chút từ nguyên liệu đến hương vị.',
          style: serif(size: phone ? 20 : 26, weight: FontWeight.w500),
        ),
        const SizedBox(height: 22),
        hero,
        const SizedBox(height: 32),
        Text('Đồ uống của quán', style: serif(size: phone ? 20 : 22)),
        const SizedBox(height: 4),
        const Text(
          'Chạm vào một món để xem và gọi ngay.',
          style: TextStyle(color: AppColors.muted, fontSize: 13),
        ),
        const SizedBox(height: 16),
        if (menu.isFirstLoad)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 40),
            child: Center(
              child: CircularProgressIndicator(color: AppColors.brown700),
            ),
          )
        else if (menu.status == LoadStatus.failure && cards.isEmpty)
          LoadError(message: menu.error ?? '', onRetry: menu.load),
        // Lưới thẻ: mỗi hàng cao bằng thẻ cao nhất; lướt ngang để đổi trang
        if (cards.isNotEmpty)
          SwipePager(
            page: menu.aboutPage,
            pageCount: menu.aboutPageCount,
            onChanged: menu.setAboutPage,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                for (var r = 0; r < cards.length; r += cols) ...[
                  if (r > 0) const SizedBox(height: 20),
                  IntrinsicHeight(
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        for (var c = 0; c < cols; c++) ...[
                          if (c > 0) const SizedBox(width: 20),
                          Expanded(
                            child: r + c < cards.length
                                ? cards[r + c]
                                : const SizedBox(),
                          ),
                        ],
                      ],
                    ),
                  ),
                ],
              ],
            ),
          ),
        if (menu.aboutPageCount > 1) ...[
          const SizedBox(height: 24),
          Pagination(
            page: menu.aboutPage,
            pageCount: menu.aboutPageCount,
            onChanged: menu.setAboutPage,
          ),
        ],
      ],
    );
  }
}

class _FeaturedCard extends StatefulWidget {
  final Mon f;
  final VoidCallback onTap;
  const _FeaturedCard(this.f, {required this.onTap});

  @override
  State<_FeaturedCard> createState() => _FeaturedCardState();
}

class _FeaturedCardState extends State<_FeaturedCard> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final f = widget.f;
    final u = f.displaySize;
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 150),
          transform: Matrix4.translationValues(0, _hover ? -3 : 0, 0),
          clipBehavior: Clip.antiAlias,
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(12),
            border: Border.all(
              color: _hover ? AppColors.brown700 : AppColors.line,
            ),
            boxShadow: _hover
                ? [
                    BoxShadow(
                      color: AppColors.brown900.withValues(alpha: 0.08),
                      blurRadius: 18,
                      offset: const Offset(0, 6),
                    ),
                  ]
                : null,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Stack(
                children: [
                  NetImage(f.image, height: 170, width: double.infinity),
                  if (f.discount > 0)
                    Positioned(
                      top: 10,
                      left: 10,
                      child: DiscountBadge(f.discount),
                    ),
                ],
              ),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        f.tenMon,
                        style: const TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      const SizedBox(height: 8),
                      Text(
                        describe(f.tenMon),
                        style: const TextStyle(
                          fontSize: 12.5,
                          color: AppColors.muted,
                          height: 1.55,
                        ),
                      ),
                      const Spacer(),
                      const SizedBox(height: 12),
                      Row(
                        children: [
                          if (u != null)
                            PriceText(
                              price: u.giaSauGiam,
                              old: u.giaSauGiam < u.giaBan ? u.giaBan : null,
                            ),
                          const Spacer(),
                          const Text(
                            'Xem món',
                            style: TextStyle(
                              fontSize: 12.5,
                              fontWeight: FontWeight.w600,
                              color: AppColors.brown700,
                            ),
                          ),
                          const SizedBox(width: 4),
                          const Icon(
                            Icons.arrow_forward,
                            size: 14,
                            color: AppColors.brown700,
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
