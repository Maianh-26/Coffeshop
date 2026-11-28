import 'package:flutter/material.dart';

import '../data.dart';
import '../theme.dart';

/// Ảnh mạng có placeholder + fallback nâu nếu lỗi.
class NetImage extends StatelessWidget {
  final String url;
  final double? width;
  final double? height;
  final BorderRadius radius;

  const NetImage(
    this.url, {
    super.key,
    this.width,
    this.height,
    this.radius = BorderRadius.zero,
  });

  @override
  Widget build(BuildContext context) {
    Widget fallback([bool loading = false]) => Container(
      width: width,
      height: height,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: loading
              ? const [AppColors.cream200, AppColors.cream300]
              : const [Color(0xFFC9A98A), Color(0xFF6B4637)],
        ),
      ),
      alignment: Alignment.center,
      child: loading ? null : const Text('☕', style: TextStyle(fontSize: 26)),
    );

    // Món chưa có HinhAnh trong CSDL
    if (url.isEmpty) {
      return ClipRRect(borderRadius: radius, child: fallback());
    }

    return ClipRRect(
      borderRadius: radius,
      child: Image.network(
        url,
        width: width,
        height: height,
        fit: BoxFit.cover,
        loadingBuilder: (c, child, p) => p == null ? child : fallback(true),
        errorBuilder: (c, e, s) => fallback(),
      ),
    );
  }
}

/// Khung trắng bo góc có viền.
class Panel extends StatelessWidget {
  final Widget child;
  final EdgeInsets padding;
  final bool shadow;
  final Color? borderColor;

  const Panel({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(16),
    this.shadow = false,
    this.borderColor,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: padding,
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: borderColor ?? AppColors.line,
          width: borderColor != null ? 1.5 : 1,
        ),
        boxShadow: shadow
            ? [
                BoxShadow(
                  color: AppColors.brown900.withValues(alpha: 0.06),
                  blurRadius: 16,
                  offset: const Offset(0, 4),
                ),
              ]
            : null,
      ),
      child: child,
    );
  }
}

class DarkButton extends StatelessWidget {
  final String label;
  final IconData? icon;
  final VoidCallback? onPressed;
  final bool expand;

  const DarkButton({
    super.key,
    required this.label,
    this.icon,
    this.onPressed,
    this.expand = false,
  });

  @override
  Widget build(BuildContext context) {
    final btn = FilledButton.icon(
      onPressed: onPressed,
      icon: icon == null ? const SizedBox.shrink() : Icon(icon, size: 16),
      label: Text(label, style: const TextStyle(fontWeight: FontWeight.w600)),
      style: FilledButton.styleFrom(
        backgroundColor: AppColors.brown900,
        foregroundColor: Colors.white,
        disabledBackgroundColor: AppColors.brown900.withValues(alpha: 0.4),
        disabledForegroundColor: Colors.white70,
        minimumSize: const Size(0, 44),
        padding: const EdgeInsets.symmetric(horizontal: 18),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
      ),
    );
    return expand ? SizedBox(width: double.infinity, child: btn) : btn;
  }
}

class LightButton extends StatelessWidget {
  final String label;
  final IconData? icon;
  final VoidCallback? onPressed;
  final bool expand;

  const LightButton({
    super.key,
    required this.label,
    this.icon,
    this.onPressed,
    this.expand = false,
  });

  @override
  Widget build(BuildContext context) {
    final btn = OutlinedButton.icon(
      onPressed: onPressed,
      icon: icon == null ? const SizedBox.shrink() : Icon(icon, size: 16),
      label: Text(label, style: const TextStyle(fontWeight: FontWeight.w600)),
      style: OutlinedButton.styleFrom(
        foregroundColor: AppColors.text,
        backgroundColor: Colors.white,
        side: const BorderSide(color: AppColors.line),
        minimumSize: const Size(0, 42),
        padding: const EdgeInsets.symmetric(horizontal: 16),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
      ),
    );
    return expand ? SizedBox(width: double.infinity, child: btn) : btn;
  }
}

class PriceText extends StatelessWidget {
  final int price;
  final int? old;
  final double size;

  const PriceText({super.key, required this.price, this.old, this.size = 14});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.baseline,
      textBaseline: TextBaseline.alphabetic,
      children: [
        Text(
          formatVnd(price),
          style: TextStyle(fontSize: size, fontWeight: FontWeight.w700),
        ),
        if (old != null && old != price) ...[
          const SizedBox(width: 6),
          Text(
            formatVnd(old!),
            style: const TextStyle(
              fontSize: 11.5,
              color: AppColors.muted,
              decoration: TextDecoration.lineThrough,
            ),
          ),
        ],
      ],
    );
  }
}

class DiscountBadge extends StatelessWidget {
  final int discount;
  const DiscountBadge(this.discount, {super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
      decoration: BoxDecoration(
        color: AppColors.red,
        borderRadius: BorderRadius.circular(5),
      ),
      child: Text(
        '-$discount%',
        style: const TextStyle(
          color: Colors.white,
          fontSize: 11,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

class QtyStepper extends StatelessWidget {
  final int value;
  final ValueChanged<int> onChanged;
  final int min;

  const QtyStepper({
    super.key,
    required this.value,
    required this.onChanged,
    this.min = 1,
  });

  @override
  Widget build(BuildContext context) {
    Widget btn(IconData icon, VoidCallback? onTap) => InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(8),
      child: SizedBox(
        width: 32,
        height: 32,
        child: Icon(
          icon,
          size: 15,
          color: onTap == null ? AppColors.cream300 : AppColors.text,
        ),
      ),
    );

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(color: AppColors.line),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          btn(Icons.remove, value > min ? () => onChanged(value - 1) : null),
          SizedBox(
            width: 28,
            child: Text(
              '$value',
              textAlign: TextAlign.center,
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
          ),
          btn(Icons.add, () => onChanged(value + 1)),
        ],
      ),
    );
  }
}

/// Khung nội dung trang: giới hạn chiều rộng + padding.
class PageBody extends StatelessWidget {
  final List<Widget> children;
  const PageBody({super.key, required this.children});

  @override
  Widget build(BuildContext context) {
    final h = isPhone(context) ? 16.0 : 24.0;
    return SingleChildScrollView(
      padding: EdgeInsets.fromLTRB(h, 24, h, 48),
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 1132),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: children,
          ),
        ),
      ),
    );
  }
}

/// Logo thương hiệu dạng tròn (cắt phần vòng tròn trắng của ảnh logo).
class BrandLogo extends StatelessWidget {
  final double size;
  const BrandLogo({super.key, this.size = 40});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: const BoxDecoration(
        shape: BoxShape.circle,
        color: Colors.white,
      ),
      clipBehavior: Clip.antiAlias,
      // Ảnh gốc có viền nâu quanh vòng tròn trắng -> phóng to để vòng tròn lấp đầy
      child: Transform.scale(
        scale: 1.37,
        child: Image.asset(
          'assets/images/logo.png',
          fit: BoxFit.cover,
          filterQuality: FilterQuality.medium,
        ),
      ),
    );
  }
}

/// Độ rộng cột phải (ô tùy chỉnh / thanh toán) theo kích thước màn hình.
double sidePaneWidth(BuildContext c) => isWide(c) ? 340 : 300;

/// Bố cục 2 cột cho tablet/desktop: mỗi cột tự cuộn riêng và cao vừa màn hình,
/// nên cột phải (thanh toán, tùy chỉnh món) luôn nằm gọn trong màn hình.
class SplitBody extends StatelessWidget {
  final Widget left;
  final Widget right;
  final double rightWidth;

  const SplitBody({
    super.key,
    required this.left,
    required this.right,
    required this.rightWidth,
  });

  @override
  Widget build(BuildContext context) {
    final h = isWide(context) ? 24.0 : 20.0;
    return Center(
      child: ConstrainedBox(
        constraints: BoxConstraints(maxWidth: 1132 + h * 2),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Expanded(
              child: SingleChildScrollView(
                padding: EdgeInsets.fromLTRB(h, 24, h / 2, 32),
                child: left,
              ),
            ),
            SizedBox(
              width: rightWidth + h * 1.5,
              child: SingleChildScrollView(
                padding: EdgeInsets.fromLTRB(h / 2, 24, h, 24),
                child: right,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Khối xám nhấp nháy dùng làm Skeleton Loading khi đang gọi API.
class Skeleton extends StatefulWidget {
  final double? width;
  final double? height;
  final double radius;
  const Skeleton({super.key, this.width, this.height, this.radius = 8});

  @override
  State<Skeleton> createState() => _SkeletonState();
}

class _SkeletonState extends State<Skeleton>
    with SingleTickerProviderStateMixin {
  late final _c = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 900),
  )..repeat(reverse: true);

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return FadeTransition(
      opacity: Tween(begin: 0.45, end: 1.0).animate(_c),
      child: Container(
        width: widget.width,
        height: widget.height,
        decoration: BoxDecoration(
          color: AppColors.cream200,
          borderRadius: BorderRadius.circular(widget.radius),
        ),
      ),
    );
  }
}

/// Khung báo lỗi tải dữ liệu + nút thử lại.
class LoadError extends StatelessWidget {
  final String message;
  final VoidCallback onRetry;
  const LoadError({super.key, required this.message, required this.onRetry});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(32),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.cream300),
      ),
      child: Column(
        children: [
          const Icon(Icons.wifi_off_rounded, size: 34, color: AppColors.muted),
          const SizedBox(height: 10),
          const Text(
            'Không tải được menu',
            style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
          ),
          const SizedBox(height: 6),
          Text(
            message,
            textAlign: TextAlign.center,
            style: const TextStyle(color: AppColors.muted, fontSize: 12.5),
          ),
          const SizedBox(height: 14),
          DarkButton(label: 'Thử lại', icon: Icons.refresh, onPressed: onRetry),
        ],
      ),
    );
  }
}

/// Thanh phân trang: ‹ 1 2 3 ›
class Pagination extends StatelessWidget {
  final int page;
  final int pageCount;
  final ValueChanged<int> onChanged;

  const Pagination({
    super.key,
    required this.page,
    required this.pageCount,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    Widget box({
      required Widget child,
      VoidCallback? onTap,
      bool active = false,
    }) => Padding(
      padding: const EdgeInsets.symmetric(horizontal: 3),
      child: Material(
        color: active ? AppColors.brown900 : Colors.white,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(8),
          side: BorderSide(color: active ? AppColors.brown900 : AppColors.line),
        ),
        child: InkWell(
          borderRadius: BorderRadius.circular(8),
          onTap: onTap,
          child: SizedBox(width: 34, height: 34, child: Center(child: child)),
        ),
      ),
    );

    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        box(
          onTap: page > 0 ? () => onChanged(page - 1) : null,
          child: Icon(
            Icons.chevron_left,
            size: 18,
            color: page > 0 ? AppColors.text : AppColors.cream300,
          ),
        ),
        for (var i = 0; i < pageCount; i++)
          box(
            active: i == page,
            onTap: () => onChanged(i),
            child: Text(
              '${i + 1}',
              style: TextStyle(
                fontWeight: FontWeight.w600,
                color: i == page ? Colors.white : AppColors.text,
              ),
            ),
          ),
        box(
          onTap: page < pageCount - 1 ? () => onChanged(page + 1) : null,
          child: Icon(
            Icons.chevron_right,
            size: 18,
            color: page < pageCount - 1 ? AppColors.text : AppColors.cream300,
          ),
        ),
      ],
    );
  }
}

/// Vùng nội dung phân trang lướt ngang được (vuốt trái = trang sau, vuốt phải = trang trước).
/// Nội dung đi theo ngón tay khi kéo, thả ra thì chuyển trang (kéo > 20% chiều rộng hoặc vuốt nhanh)
/// hoặc bật về chỗ cũ. Đồng bộ với [Pagination] vì cùng dùng [page] / [onChanged].
/// Không dùng PageView vì danh sách nằm trong trang cuộn dọc, cao thấp khác nhau theo số món.
class SwipePager extends StatefulWidget {
  final int page;
  final int pageCount;
  final ValueChanged<int> onChanged;
  final Widget child;

  const SwipePager({
    super.key,
    required this.page,
    required this.pageCount,
    required this.onChanged,
    required this.child,
  });

  @override
  State<SwipePager> createState() => _SwipePagerState();
}

class _SwipePagerState extends State<SwipePager> {
  double _dx = 0; // độ lệch khi đang kéo
  bool _dragging = false;
  int _dir = 1; // 1: sang trang sau (trượt từ phải vào), -1: về trang trước

  @override
  void didUpdateWidget(SwipePager old) {
    super.didUpdateWidget(old);
    if (old.page != widget.page) {
      _dir = widget.page > old.page ? 1 : -1;
      _dx = 0;
    }
  }

  bool get _canPrev => widget.page > 0;
  bool get _canNext => widget.page < widget.pageCount - 1;

  void _update(DragUpdateDetails d) {
    var delta = d.delta.dx;
    // Kéo quá trang đầu / trang cuối -> có lực cản
    if ((_dx + delta > 0 && !_canPrev) || (_dx + delta < 0 && !_canNext)) {
      delta *= 0.3;
    }
    setState(() {
      _dragging = true;
      _dx += delta;
    });
  }

  void _end(DragEndDetails d) {
    final width = context.size?.width ?? 1;
    final v = d.primaryVelocity ?? 0;
    final next = (_dx < -width * 0.2 || v < -400) && _canNext;
    final prev = (_dx > width * 0.2 || v > 400) && _canPrev;
    setState(() {
      _dragging = false;
      _dx = 0;
    });
    if (next) widget.onChanged(widget.page + 1);
    if (prev) widget.onChanged(widget.page - 1);
  }

  @override
  Widget build(BuildContext context) {
    if (widget.pageCount <= 1) return widget.child;
    return GestureDetector(
      behavior: HitTestBehavior.translucent,
      onHorizontalDragUpdate: _update,
      onHorizontalDragEnd: _end,
      onHorizontalDragCancel: () => setState(() {
        _dragging = false;
        _dx = 0;
      }),
      child: ClipRect(
        child: AnimatedSwitcher(
          duration: const Duration(milliseconds: 280),
          switchInCurve: Curves.easeOutCubic,
          switchOutCurve: Curves.easeInCubic,
          layoutBuilder: (current, previous) => Stack(
            alignment: Alignment.topCenter,
            children: [...previous, ?current],
          ),
          transitionBuilder: (child, animation) {
            final incoming = child.key == ValueKey(widget.page);
            final from = Offset(
              incoming ? _dir.toDouble() : -_dir.toDouble(),
              0,
            );
            return SlideTransition(
              position: Tween(begin: from, end: Offset.zero).animate(animation),
              child: child,
            );
          },
          child: AnimatedContainer(
            key: ValueKey(widget.page),
            duration: _dragging
                ? Duration.zero
                : const Duration(milliseconds: 200),
            curve: Curves.easeOut,
            transform: Matrix4.translationValues(_dx, 0, 0),
            child: widget.child,
          ),
        ),
      ),
    );
  }
}
