import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../cart.dart';
import '../data.dart';
import '../models/menu_models.dart';
import '../providers/menu_provider.dart';
import '../theme.dart';
import '../widgets/common.dart';

class MenuPage extends StatefulWidget {
  /// Món đang sửa từ giỏ hàng (null = thêm mới)
  final CartLine? editing;
  final VoidCallback onCancelEdit;
  final VoidCallback onUpdated;

  /// Món được chọn sẵn khi mở từ trang Giới thiệu (maTenMon)
  final int? focusProductId;

  const MenuPage({
    super.key,
    required this.editing,
    required this.onCancelEdit,
    required this.onUpdated,
    this.focusProductId,
  });

  @override
  State<MenuPage> createState() => _MenuPageState();
}

class _MenuPageState extends State<MenuPage> {
  late final _search = TextEditingController(
    text: context.read<MenuProvider>().query,
  );

  /// Món đang mở ô tùy chỉnh (null = ẩn ô tùy chỉnh)
  int? _selectedId;

  /// maKichThuoc đang chọn (S / M / L ... theo bảng ChiTietMon)
  String _size = 'M';
  String _ice = '50%';
  String _sugar = '50%';
  int _qty = 1;
  Set<int> _toppings = {};

  @override
  void initState() {
    super.initState();
    _loadEditing();
    _loadFocus();
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  /// Mở từ trang Giới thiệu: chọn sẵn món (MenuProvider.focus đã nhảy tới đúng trang)
  void _loadFocus() {
    final id = widget.focusProductId;
    if (id == null || widget.editing != null) return;
    final m = context.read<MenuProvider>().findMon(id);
    if (m == null || !m.coTheBan) return;
    _selectedId = id;
    _size = m.defaultSize?.maKichThuoc ?? '';
    // Trên điện thoại: mở luôn bảng tùy chỉnh
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && isPhone(context)) _openSheet();
    });
  }

  @override
  void didUpdateWidget(MenuPage old) {
    super.didUpdateWidget(old);
    if (widget.editing != old.editing) _loadEditing();
  }

  void _loadEditing() {
    final e = widget.editing;
    if (e == null) return;
    _selectedId = e.product.maTenMon;
    _size = e.size.maKichThuoc;
    _ice = e.ice;
    _sugar = e.sugar;
    _qty = e.qty;
    _toppings = {for (final t in e.toppings) t.maTopping};
    // Trên điện thoại: vào từ "sửa món" thì tự mở bottom sheet
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && isPhone(context)) _openSheet();
    });
  }

  /// Món đang chọn — lấy bản mới nhất từ API, nếu không còn thì dùng bản trong giỏ khi sửa
  Mon? get _selected {
    final id = _selectedId;
    if (id == null) return null;
    return context.read<MenuProvider>().findMon(id) ??
        (widget.editing?.product.maTenMon == id
            ? widget.editing!.product
            : null);
  }

  /// Chọn món. Bấm lại đúng món đang mở -> ẩn ô tùy chỉnh.
  void _select(Mon m, {bool toggle = true}) {
    if (widget.editing != null) widget.onCancelEdit();
    if (toggle && m.maTenMon == _selectedId) {
      setState(() => _selectedId = null);
      return;
    }
    setState(() {
      _selectedId = m.maTenMon;
      _size = m.defaultSize?.maKichThuoc ?? '';
      _ice = '50%';
      _sugar = '50%';
      _qty = 1;
      _toppings = {};
    });
  }

  /// Topping đã chọn, theo thứ tự danh sách từ API (món bánh không có topping)
  List<ToppingItem> _chosenToppings(Mon product) => product.isDrink
      ? [
          for (final t in context.read<MenuProvider>().toppings)
            if (_toppings.contains(t.maTopping) && t.duNguyenLieu) t,
        ]
      : const [];

  /// Trên điện thoại: chọn món -> mở bottom sheet tùy chỉnh
  void _selectPhone(Mon m) {
    _select(m, toggle: false);
    _openSheet();
  }

  void _openSheet() {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setSheet) => DraggableScrollableSheet(
          expand: false,
          initialChildSize: 0.85,
          maxChildSize: 0.95,
          builder: (ctx, scroll) => SingleChildScrollView(
            controller: scroll,
            padding: const EdgeInsets.fromLTRB(18, 12, 18, 24),
            child: _customizer(
              rebuild: (fn) {
                setState(fn);
                setSheet(() {});
              },
              afterSubmit: () => Navigator.of(ctx).pop(),
              inSheet: true,
            ),
          ),
        ),
      ),
    ).whenComplete(() {
      // Đóng sheet mà chưa cập nhật -> thoát chế độ sửa
      if (!mounted) return;
      if (widget.editing != null) widget.onCancelEdit();
      // Đóng sheet -> bỏ chọn món
      setState(() => _selectedId = null);
    });
  }

  void _submit(Mon product) {
    final size = product.sizeByCode(_size);
    if (size == null) return;
    final line = CartLine(
      product: product,
      size: size,
      ice: _ice,
      sugar: _sugar,
      toppings: _chosenToppings(product),
      qty: _qty,
    );
    final cart = context.read<CartStore>();
    final editing = widget.editing;
    if (editing != null) {
      cart.replace(editing.key, line);
      widget.onUpdated();
    } else {
      cart.add(line);
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text('Đã thêm $_qty × ${product.tenMon} vào giỏ'),
            backgroundColor: AppColors.brown900,
            behavior: SnackBarBehavior.floating,
            width: isPhone(context) ? null : 360,
            duration: const Duration(milliseconds: 1400),
          ),
        );
    }
  }

  @override
  Widget build(BuildContext context) {
    final menu = context.watch<MenuProvider>();
    final phone = isPhone(context);

    // Cột trái tự co theo độ rộng: hẹp (tablet dọc có ô tùy chỉnh) -> 2 cột món
    final main = LayoutBuilder(
      builder: (context, box) {
        final narrow = phone || box.maxWidth < 560;
        final cols = box.maxWidth < 560 ? 2 : 3;
        final gridDelegate = SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: cols,
          crossAxisSpacing: phone ? 12 : 16,
          mainAxisSpacing: phone ? 12 : 16,
          mainAxisExtent: phone ? 186 : 214,
        );
        final imageHeight = phone ? 110.0 : 140.0;

        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Flex(
              direction: narrow ? Axis.vertical : Axis.horizontal,
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              crossAxisAlignment: narrow
                  ? CrossAxisAlignment.stretch
                  : CrossAxisAlignment.center,
              children: [
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text('Khám phá menu', style: serif(size: 24)),
                    const SizedBox(width: 6),
                    IconButton(
                      tooltip: 'Tải lại menu',
                      onPressed: menu.isLoading ? null : menu.load,
                      icon: const Icon(
                        Icons.refresh,
                        size: 20,
                        color: AppColors.muted,
                      ),
                    ),
                  ],
                ),
                if (narrow) const SizedBox(height: 12),
                SizedBox(
                  width: narrow ? null : 280,
                  child: _SearchField(
                    controller: _search,
                    onChanged: menu.setQuery,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            // Đang tải lại khi đã có dữ liệu -> thanh tiến trình mỏng, không che menu
            if (menu.isLoading && !menu.isFirstLoad)
              const Padding(
                padding: EdgeInsets.only(bottom: 12),
                child: LinearProgressIndicator(
                  minHeight: 3,
                  color: AppColors.brown700,
                  backgroundColor: AppColors.cream200,
                ),
              ),
            if (menu.isFirstLoad) ...[
              // ----- Skeleton Loading: lần đầu gọi API
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  for (final w in [64.0, 80.0, 56.0, 92.0])
                    Skeleton(width: w, height: 32, radius: 999),
                ],
              ),
              const SizedBox(height: 18),
              GridView.builder(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: MenuProvider.pageSize,
                gridDelegate: gridDelegate,
                itemBuilder: (_, _) =>
                    _ProductCardSkeleton(imageHeight: imageHeight),
              ),
              const SizedBox(height: 20),
              const Center(
                child: SizedBox(
                  width: 22,
                  height: 22,
                  child: CircularProgressIndicator(
                    strokeWidth: 2.5,
                    color: AppColors.brown700,
                  ),
                ),
              ),
            ] else if (menu.status == LoadStatus.failure &&
                menu.products.isEmpty)
              LoadError(message: menu.error ?? '', onRetry: menu.load)
            else ...[
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  _Chip(
                    label: 'Tất cả',
                    active: menu.categoryId == null,
                    onTap: () => menu.selectCategory(null),
                  ),
                  for (final c in menu.categories)
                    _Chip(
                      label: c.tenDanhMuc,
                      active: menu.categoryId == c.maDanhMuc,
                      onTap: () => menu.selectCategory(c.maDanhMuc),
                    ),
                ],
              ),
              const SizedBox(height: 18),
              if (menu.visible.isEmpty)
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 40),
                  child: Text(
                    'Không tìm thấy món phù hợp.',
                    textAlign: TextAlign.center,
                    style: TextStyle(color: AppColors.muted),
                  ),
                )
              else
                // Lướt ngang để đổi trang
                SwipePager(
                  page: menu.page,
                  pageCount: menu.pageCount,
                  onChanged: menu.setPage,
                  child: GridView.builder(
                    shrinkWrap: true,
                    physics: const NeverScrollableScrollPhysics(),
                    itemCount: menu.visible.length,
                    gridDelegate: gridDelegate,
                    itemBuilder: (_, i) {
                      final p = menu.visible[i];
                      return _ProductCard(
                        product: p,
                        active: !phone && p.maTenMon == _selectedId,
                        imageHeight: imageHeight,
                        onTap: !p.coTheBan
                            ? null
                            : () => phone ? _selectPhone(p) : _select(p),
                      );
                    },
                  ),
                ),
              if (menu.pageCount > 1) ...[
                const SizedBox(height: 20),
                Pagination(
                  page: menu.page,
                  pageCount: menu.pageCount,
                  onChanged: menu.setPage,
                ),
              ],
            ],
          ],
        );
      },
    );

    // Chỉ hiện ô tùy chỉnh khi đã chọn món
    if (_selected == null || phone) return PageBody(children: [main]);

    final side = Panel(
      padding: const EdgeInsets.all(18),
      shadow: true,
      borderColor: AppColors.brown900,
      child: _customizer(rebuild: setState),
    );

    // Tablet / desktop: lưới món bên trái, ô tùy chỉnh bên phải (cuộn riêng)
    return SplitBody(
      left: main,
      right: side,
      rightWidth: sidePaneWidth(context) - 20,
    );
  }

  Widget _customizer({
    required void Function(VoidCallback) rebuild,
    VoidCallback? afterSubmit,
    bool inSheet = false,
  }) {
    final product = _selected;
    if (product == null) return const SizedBox.shrink();
    // read (không watch): hàm này còn được gọi trong bottom sheet
    final toppings = context.read<MenuProvider>().toppings;
    final size = product.sizeByCode(_size) ?? product.defaultSize;
    final discount = product.discount;
    final km = product.khuyenMai;
    final toppingTotal = _chosenToppings(
      product,
    ).fold(0, (s, t) => s + t.giaThem);
    final unitTotal = (size?.giaSauGiam ?? 0) + toppingTotal;
    final editing = widget.editing != null;
    final canSubmit = size != null && size.chonDuoc;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        if (inSheet)
          Center(
            child: Container(
              width: 40,
              height: 4,
              margin: const EdgeInsets.only(bottom: 14),
              decoration: BoxDecoration(
                color: AppColors.cream300,
                borderRadius: BorderRadius.circular(2),
              ),
            ),
          ),
        Text(
          editing ? 'Sửa món' : 'Tùy chỉnh món',
          style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: 12),
        Stack(
          children: [
            NetImage(
              product.image,
              height: 150,
              width: double.infinity,
              radius: BorderRadius.circular(8),
            ),
            if (discount > 0)
              Positioned(top: 8, left: 8, child: DiscountBadge(discount)),
          ],
        ),
        const SizedBox(height: 12),
        Text(product.tenMon, style: serif(size: 18)),
        const SizedBox(height: 2),
        if (size != null)
          PriceText(
            price: size.giaSauGiam,
            old: size.giaSauGiam < size.giaBan ? size.giaBan : null,
            size: 18,
          ),
        const SizedBox(height: 14),
        _OptionGroup(
          label: 'Kích cỡ',
          options: {
            for (final s in product.kichThuoc)
              s.maKichThuoc: '${s.maKichThuoc} · ${formatVnd(s.giaSauGiam)}',
          },
          disabled: {
            for (final s in product.kichThuoc)
              if (!s.chonDuoc) s.maKichThuoc,
          },
          // Món giảm giá: hiện icon (i) kèm giá gốc từng size
          hints: discount > 0
              ? {
                  for (final s in product.kichThuoc)
                    s.maKichThuoc:
                        '${s.tenKichThuoc} – giá đã áp dụng ưu đãi -$discount%\n'
                        'Giá gốc: ${formatVnd(s.giaBan)}',
                }
              : null,
          value: size?.maKichThuoc ?? '',
          onChanged: (v) => rebuild(() => _size = v),
        ),
        if (product.isDrink) ...[
          _OptionGroup(
            label: 'Lượng đá',
            options: {for (final s in iceLevels) s: s},
            value: _ice,
            onChanged: (v) => rebuild(() => _ice = v),
          ),
          _OptionGroup(
            label: 'Lượng đường',
            options: {for (final s in sugarLevels) s: s},
            value: _sugar,
            onChanged: (v) => rebuild(() => _sugar = v),
          ),
          if (toppings.isNotEmpty)
            _ToppingGroup(
              toppings: toppings,
              selected: _toppings,
              onToggle: (id) => rebuild(() {
                _toppings = {..._toppings};
                if (!_toppings.remove(id)) _toppings.add(id);
              }),
            ),
        ],
        if (discount > 0)
          _Note('Giá đã được áp dụng ưu đãi -$discount% (mã ${km!.code})')
        else if (km != null && km.khungGio != null)
          // Khuyến mãi theo khung giờ chưa tới giờ áp dụng
          _Note(
            'Giảm ${km.phanTramGiam}% trong khung giờ ${km.khungGio} (mã ${km.code})',
          ),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            const Text(
              'Số lượng',
              style: TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
            ),
            QtyStepper(value: _qty, onChanged: (v) => rebuild(() => _qty = v)),
          ],
        ),
        const SizedBox(height: 14),
        DarkButton(
          expand: true,
          icon: editing ? Icons.check : Icons.add,
          label: canSubmit
              ? '${editing ? 'Cập nhật món' : 'Thêm vào giỏ'} · ${formatVnd(unitTotal * _qty)}'
              : 'Size này tạm hết',
          onPressed: canSubmit
              ? () {
                  _submit(product);
                  afterSubmit?.call();
                }
              : null,
        ),
        if (editing)
          TextButton(
            onPressed: () {
              widget.onCancelEdit();
              afterSubmit?.call();
            },
            child: const Text(
              'Hủy sửa',
              style: TextStyle(color: AppColors.muted),
            ),
          ),
      ],
    );
  }
}

class _Note extends StatelessWidget {
  final String text;
  const _Note(this.text);

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Row(
        children: [
          const Icon(Icons.info_outline, size: 13, color: AppColors.muted),
          const SizedBox(width: 6),
          Expanded(
            child: Text(
              text,
              style: const TextStyle(fontSize: 11.5, color: AppColors.muted),
            ),
          ),
        ],
      ),
    );
  }
}

class _SearchField extends StatelessWidget {
  final TextEditingController controller;
  final ValueChanged<String> onChanged;
  const _SearchField({required this.controller, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    OutlineInputBorder border(Color c) => OutlineInputBorder(
      borderRadius: BorderRadius.circular(8),
      borderSide: BorderSide(color: c),
    );
    return TextField(
      controller: controller,
      onChanged: onChanged,
      style: const TextStyle(fontSize: 14),
      decoration: InputDecoration(
        isDense: true,
        filled: true,
        fillColor: Colors.white,
        hintText: 'Tìm món bạn yêu thích...',
        hintStyle: const TextStyle(color: AppColors.muted, fontSize: 13),
        prefixIcon: const Icon(Icons.search, size: 18, color: AppColors.muted),
        contentPadding: const EdgeInsets.symmetric(vertical: 12),
        enabledBorder: border(AppColors.line),
        focusedBorder: border(AppColors.brown700),
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  final String label;
  final bool active;
  final VoidCallback onTap;
  const _Chip({required this.label, required this.active, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return Material(
      color: active ? AppColors.brown900 : Colors.white,
      shape: StadiumBorder(
        side: BorderSide(color: active ? AppColors.brown900 : AppColors.line),
      ),
      child: InkWell(
        customBorder: const StadiumBorder(),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 7),
          child: Text(
            label,
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w500,
              color: active ? Colors.white : AppColors.muted,
            ),
          ),
        ),
      ),
    );
  }
}

/// Thẻ món dạng khung xám trong lúc chờ API
class _ProductCardSkeleton extends StatelessWidget {
  final double imageHeight;
  const _ProductCardSkeleton({required this.imageHeight});

  @override
  Widget build(BuildContext context) {
    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.line, width: 1.5),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Skeleton(height: imageHeight, width: double.infinity, radius: 0),
          const Padding(
            padding: EdgeInsets.fromLTRB(12, 12, 12, 0),
            child: Skeleton(height: 14, width: 120),
          ),
          const Padding(
            padding: EdgeInsets.fromLTRB(12, 12, 12, 0),
            child: Skeleton(height: 14, width: 70),
          ),
        ],
      ),
    );
  }
}

class _ProductCard extends StatefulWidget {
  final Mon product;
  final bool active;
  final double imageHeight;

  /// null = món không bán được (hết hàng / hết nguyên liệu)
  final VoidCallback? onTap;

  const _ProductCard({
    required this.product,
    required this.active,
    required this.imageHeight,
    required this.onTap,
  });

  @override
  State<_ProductCard> createState() => _ProductCardState();
}

class _ProductCardState extends State<_ProductCard> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final p = widget.product;
    final u = p.displaySize;
    final soldOut = widget.onTap == null;
    final hover = _hover && !soldOut;
    return MouseRegion(
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      cursor: soldOut ? SystemMouseCursors.basic : SystemMouseCursors.click,
      child: GestureDetector(
        onTap: widget.onTap,
        child: Opacity(
          opacity: soldOut ? 0.55 : 1,
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 150),
            transform: Matrix4.translationValues(0, hover ? -2 : 0, 0),
            clipBehavior: Clip.antiAlias,
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(12),
              border: Border.all(
                color: widget.active ? AppColors.brown900 : AppColors.line,
                width: 1.5,
              ),
              boxShadow: hover
                  ? [
                      BoxShadow(
                        color: AppColors.brown900.withValues(alpha: 0.08),
                        blurRadius: 16,
                        offset: const Offset(0, 4),
                      ),
                    ]
                  : null,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Stack(
                  children: [
                    NetImage(
                      p.image,
                      height: widget.imageHeight,
                      width: double.infinity,
                    ),
                    if (p.discount > 0 && !soldOut)
                      Positioned(
                        top: 8,
                        left: 8,
                        child: DiscountBadge(p.discount),
                      ),
                    if (soldOut)
                      Positioned(
                        top: 8,
                        left: 8,
                        child: Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 7,
                            vertical: 2,
                          ),
                          decoration: BoxDecoration(
                            color: AppColors.text,
                            borderRadius: BorderRadius.circular(5),
                          ),
                          child: const Text(
                            'Tạm hết',
                            style: TextStyle(
                              color: Colors.white,
                              fontSize: 11,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ),
                  ],
                ),
                Expanded(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          p.tenMon,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        Row(
                          children: [
                            Expanded(
                              child: FittedBox(
                                fit: BoxFit.scaleDown,
                                alignment: Alignment.centerLeft,
                                child: u == null
                                    ? const SizedBox()
                                    : PriceText(
                                        price: u.giaSauGiam,
                                        old: u.giaSauGiam < u.giaBan
                                            ? u.giaBan
                                            : null,
                                      ),
                              ),
                            ),
                            if (!soldOut)
                              Container(
                                width: 24,
                                height: 24,
                                decoration: BoxDecoration(
                                  shape: BoxShape.circle,
                                  color: widget.active
                                      ? AppColors.brown900
                                      : AppColors.cream100,
                                ),
                                child: Icon(
                                  widget.active ? Icons.check : Icons.add,
                                  size: 14,
                                  color: widget.active
                                      ? Colors.white
                                      : AppColors.brown900,
                                ),
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
      ),
    );
  }
}

class _OptionGroup extends StatelessWidget {
  final String label;
  final Map<String, String> options;

  /// Lựa chọn không bấm được (vd. size hết nguyên liệu)
  final Set<String> disabled;

  /// Lưu ý cho từng lựa chọn -> hiện icon (i) có tooltip
  final Map<String, String>? hints;
  final String value;
  final ValueChanged<String> onChanged;

  const _OptionGroup({
    required this.label,
    required this.options,
    this.disabled = const {},
    this.hints,
    required this.value,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 6),
          Row(
            children: [
              for (final e in options.entries.toList().asMap().entries) ...[
                if (e.key > 0) const SizedBox(width: 8),
                Expanded(
                  child: Opacity(
                    opacity: disabled.contains(e.value.key) ? 0.4 : 1,
                    child: Material(
                      color: value == e.value.key
                          ? AppColors.brown900
                          : AppColors.cream100,
                      borderRadius: BorderRadius.circular(8),
                      child: InkWell(
                        borderRadius: BorderRadius.circular(8),
                        onTap: disabled.contains(e.value.key)
                            ? null
                            : () => onChanged(e.value.key),
                        child: SizedBox(
                          height: 38,
                          child: Row(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              Flexible(
                                child: Text(
                                  e.value.value,
                                  overflow: TextOverflow.ellipsis,
                                  style: TextStyle(
                                    fontSize: 12.5,
                                    fontWeight: FontWeight.w500,
                                    color: value == e.value.key
                                        ? Colors.white
                                        : AppColors.text,
                                  ),
                                ),
                              ),
                              if (hints?[e.value.key] case final hint?) ...[
                                const SizedBox(width: 5),
                                Tooltip(
                                  message: hint,
                                  triggerMode: TooltipTriggerMode.tap,
                                  preferBelow: false,
                                  child: Icon(
                                    Icons.info_outline,
                                    size: 14,
                                    color: value == e.value.key
                                        ? Colors.white70
                                        : AppColors.muted,
                                  ),
                                ),
                              ],
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}

/// Chọn nhiều topping, có hiển thị giá từng loại.
class _ToppingGroup extends StatelessWidget {
  final List<ToppingItem> toppings;
  final Set<int> selected;
  final ValueChanged<int> onToggle;

  const _ToppingGroup({
    required this.toppings,
    required this.selected,
    required this.onToggle,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'Topping',
            style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 6),
          for (final t in toppings) ...[
            _ToppingTile(
              topping: t,
              active: selected.contains(t.maTopping) && t.duNguyenLieu,
              onTap: t.duNguyenLieu ? () => onToggle(t.maTopping) : null,
            ),
            if (t != toppings.last) const SizedBox(height: 6),
          ],
        ],
      ),
    );
  }
}

class _ToppingTile extends StatelessWidget {
  final ToppingItem topping;
  final bool active;

  /// null = topping hết nguyên liệu
  final VoidCallback? onTap;

  const _ToppingTile({
    required this.topping,
    required this.active,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final fg = active ? Colors.white : AppColors.text;
    return Opacity(
      opacity: onTap == null ? 0.45 : 1,
      child: Material(
        color: active ? AppColors.brown900 : AppColors.cream100,
        borderRadius: BorderRadius.circular(8),
        child: InkWell(
          borderRadius: BorderRadius.circular(8),
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
            child: Row(
              children: [
                Icon(
                  active ? Icons.check_box : Icons.check_box_outline_blank,
                  size: 17,
                  color: fg,
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    onTap == null
                        ? '${topping.tenTopping} (tạm hết)'
                        : topping.tenTopping,
                    style: TextStyle(
                      fontSize: 12.5,
                      fontWeight: FontWeight.w500,
                      color: fg,
                    ),
                  ),
                ),
                Text(
                  '+${formatVnd(topping.giaThem)}',
                  style: TextStyle(
                    fontSize: 12.5,
                    fontWeight: FontWeight.w600,
                    color: fg,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
