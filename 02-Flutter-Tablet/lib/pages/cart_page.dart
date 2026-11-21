import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../cart.dart';
import '../data.dart';
import '../services/order_api.dart';
import '../theme.dart';
import '../widgets/common.dart';

class CartPage extends StatefulWidget {
  final ValueChanged<CartLine> onEdit;
  final VoidCallback onContinue;
  final VoidCallback onCheckout;

  const CartPage({
    super.key,
    required this.onEdit,
    required this.onContinue,
    required this.onCheckout,
  });

  @override
  State<CartPage> createState() => _CartPageState();
}

class _CartPageState extends State<CartPage> {
  @override
  Widget build(BuildContext context) {
    final cart = context.watch<CartStore>();
    final phone = isPhone(context);

    // Cột trái hẹp (tablet dọc) -> tiêu đề & nút xếp dọc, thẻ món gọn hơn
    Widget head(bool narrow) => Flex(
      direction: narrow ? Axis.vertical : Axis.horizontal,
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      crossAxisAlignment: narrow
          ? CrossAxisAlignment.stretch
          : CrossAxisAlignment.center,
      children: [
        Text('Giỏ hàng của bạn', style: serif(size: 24)),
        if (narrow) const SizedBox(height: 12),
        LightButton(
          label: 'Tiếp tục chọn món',
          icon: Icons.arrow_back,
          onPressed: widget.onContinue,
        ),
      ],
    );

    Widget items(bool narrow) => Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(
              'Món đã chọn (${cart.count})',
              style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
            ),
            if (cart.lines.isNotEmpty)
              TextButton(
                onPressed: cart.clear,
                style: TextButton.styleFrom(
                  foregroundColor: AppColors.muted,
                  padding: EdgeInsets.zero,
                ),
                child: const Text(
                  'Xóa tất cả',
                  style: TextStyle(
                    fontSize: 12,
                    decoration: TextDecoration.underline,
                  ),
                ),
              ),
          ],
        ),
        const SizedBox(height: 8),
        if (cart.lines.isEmpty) _empty(),
        for (final l in cart.lines) ...[
          _LineTile(
            line: l,
            compact: narrow,
            onQty: (q) => cart.setQty(l.key, q),
            onRemove: () => cart.remove(l.key),
            onEdit: () => widget.onEdit(l),
          ),
          const SizedBox(height: 12),
        ],
      ],
    );

    final summary = _Summary(cart: cart, onCheckout: widget.onCheckout);

    if (phone) {
      return PageBody(
        children: [
          head(true),
          const SizedBox(height: 20),
          items(true),
          const SizedBox(height: 24),
          summary,
        ],
      );
    }

    // Tablet / desktop: danh sách món bên trái, thanh toán bên phải,
    // mỗi cột cuộn riêng nên phần thanh toán luôn vừa màn hình.
    return SplitBody(
      rightWidth: sidePaneWidth(context),
      left: LayoutBuilder(
        builder: (context, box) {
          final narrow = box.maxWidth < 520;
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [head(narrow), const SizedBox(height: 20), items(narrow)],
          );
        },
      ),
      right: summary,
    );
  }

  Widget _empty() => Container(
    padding: const EdgeInsets.all(40),
    margin: const EdgeInsets.only(bottom: 12),
    decoration: BoxDecoration(
      color: Colors.white,
      borderRadius: BorderRadius.circular(12),
      border: Border.all(color: AppColors.cream300),
    ),
    child: Column(
      children: [
        const Icon(
          Icons.shopping_bag_outlined,
          size: 32,
          color: AppColors.muted,
        ),
        const SizedBox(height: 10),
        const Text(
          'Giỏ hàng đang trống.',
          style: TextStyle(color: AppColors.muted),
        ),
        const SizedBox(height: 12),
        DarkButton(label: 'Chọn món ngay', onPressed: widget.onContinue),
      ],
    ),
  );
}

class _LineTile extends StatelessWidget {
  final CartLine line;
  final bool compact;
  final ValueChanged<int> onQty;
  final VoidCallback onRemove;
  final VoidCallback onEdit;

  const _LineTile({
    required this.line,
    required this.compact,
    required this.onQty,
    required this.onRemove,
    required this.onEdit,
  });

  @override
  Widget build(BuildContext context) {
    final p = line.product;
    final imgSize = compact ? 60.0 : 76.0;
    final toppingNames = line.toppings.map((t) => t.tenTopping).join(', ');

    return Panel(
      padding: const EdgeInsets.all(14),
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Align(
              alignment: Alignment.topCenter,
              child: NetImage(
                p.image,
                width: imgSize,
                height: imgSize,
                radius: BorderRadius.circular(8),
              ),
            ),
            SizedBox(width: compact ? 10 : 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Flexible(
                        child: Text(
                          p.tenMon,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                      if (p.discount > 0) ...[
                        const SizedBox(width: 8),
                        Text(
                          '-${p.discount}%',
                          style: const TextStyle(
                            color: AppColors.red,
                            fontSize: 11,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ],
                    ],
                  ),
                  const SizedBox(height: 2),
                  InkWell(
                    onTap: onEdit,
                    borderRadius: BorderRadius.circular(4),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Flexible(
                          child: Text(
                            p.isDrink
                                ? 'Cỡ ${line.size.maKichThuoc} · ${line.ice} đá · ${line.sugar} đường'
                                : 'Cỡ ${line.size.maKichThuoc}',
                            style: const TextStyle(
                              fontSize: 12,
                              color: AppColors.muted,
                            ),
                          ),
                        ),
                        const SizedBox(width: 6),
                        const Icon(
                          Icons.edit_outlined,
                          size: 13,
                          color: AppColors.muted,
                        ),
                      ],
                    ),
                  ),
                  if (toppingNames.isNotEmpty)
                    Padding(
                      padding: const EdgeInsets.only(top: 2),
                      child: Text(
                        'Topping: $toppingNames (+${formatVnd(line.toppingTotal)})',
                        style: const TextStyle(
                          fontSize: 12,
                          color: AppColors.muted,
                        ),
                      ),
                    ),
                  const SizedBox(height: 4),
                  PriceText(
                    price: line.price,
                    old: p.discount > 0 ? line.oldPrice : null,
                  ),
                  const SizedBox(height: 8),
                  QtyStepper(value: line.qty, onChanged: onQty),
                ],
              ),
            ),
            SizedBox(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  IconButton(
                    onPressed: onRemove,
                    tooltip: 'Xóa món',
                    visualDensity: VisualDensity.compact,
                    icon: const Icon(
                      Icons.delete_outline,
                      size: 19,
                      color: AppColors.muted,
                    ),
                    hoverColor: AppColors.redSoft,
                  ),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      const Text(
                        'Thành tiền',
                        style: TextStyle(fontSize: 11, color: AppColors.muted),
                      ),
                      Text(
                        formatVnd(line.price * line.qty),
                        style: const TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Summary extends StatelessWidget {
  final CartStore cart;
  final VoidCallback onCheckout;
  const _Summary({required this.cart, required this.onCheckout});

  @override
  Widget build(BuildContext context) {
    Widget row(String label, String value, {Color? color}) => Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Expanded(
            child: Text(
              label,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontSize: 13, color: AppColors.muted),
            ),
          ),
          const SizedBox(width: 8),
          Text(
            value,
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: color,
            ),
          ),
        ],
      ),
    );

    return Panel(
      padding: const EdgeInsets.all(20),
      shadow: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'Thông tin thanh toán',
            style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 14),
          // Mã khuyến mãi thật của các món trong giỏ (bảng KhuyenMai), không còn mã cố định
          for (final code in cart.promoCodes)
            Container(
              margin: const EdgeInsets.only(bottom: 8),
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
              decoration: BoxDecoration(
                color: AppColors.cream100,
                borderRadius: BorderRadius.circular(8),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Expanded(
                    child: Text(
                      code,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontSize: 12.5,
                        color: AppColors.muted,
                      ),
                    ),
                  ),
                  const Row(
                    children: [
                      Icon(Icons.check, size: 14, color: AppColors.green),
                      SizedBox(width: 4),
                      Text(
                        'Đã áp dụng',
                        style: TextStyle(
                          fontSize: 12.5,
                          color: AppColors.green,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          const SizedBox(height: 6),
          row('Tạm tính (giá gốc)', formatVnd(cart.subtotal)),
          if (cart.saved > 0)
            row(
              'Khuyến mãi (tối đa -${cart.maxDiscount}%)',
              '-${formatVnd(cart.saved)}',
              color: AppColors.red,
            ),
          const Divider(color: AppColors.line, height: 20),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text(
                'Tổng cộng',
                style: TextStyle(fontWeight: FontWeight.w700),
              ),
              Text(
                formatVnd(cart.total),
                style: const TextStyle(
                  fontSize: 26,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          if (cart.saved > 0)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
              decoration: BoxDecoration(
                color: AppColors.redSoft,
                borderRadius: BorderRadius.circular(8),
              ),
              child: Row(
                children: [
                  const Icon(
                    Icons.info_outline,
                    size: 15,
                    color: AppColors.redText,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'Bạn đã tiết kiệm ${formatVnd(cart.saved)} cho đơn này.',
                      style: const TextStyle(
                        fontSize: 12.5,
                        color: AppColors.redText,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          const SizedBox(height: 12),
          const Text(
            'Giá đã bao gồm thuế. Nhân viên sẽ mang món ra tận bàn.',
            style: TextStyle(fontSize: 12, color: AppColors.muted),
          ),
          const SizedBox(height: 16),
          const Text(
            'Phương thức thanh toán',
            style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: _MethodOption(
                  icon: Icons.qr_code_2,
                  label: 'Chuyển khoản QR',
                  selected: cart.phuongThuc == PhuongThuc.qr,
                  onTap: () => cart.phuongThuc = PhuongThuc.qr,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: _MethodOption(
                  icon: Icons.payments_outlined,
                  label: 'Tiền mặt',
                  selected: cart.phuongThuc == PhuongThuc.cash,
                  onTap: () => cart.phuongThuc = PhuongThuc.cash,
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),
          DarkButton(
            expand: true,
            icon: cart.phuongThuc == PhuongThuc.cash
                ? Icons.storefront_outlined
                : Icons.arrow_forward,
            label: cart.phuongThuc == PhuongThuc.cash
                ? 'Đặt món · ${formatVnd(cart.total)}'
                : 'Thanh toán · ${formatVnd(cart.total)}',
            onPressed: cart.lines.isEmpty ? null : onCheckout,
          ),
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(
                Icons.verified_user_outlined,
                size: 13,
                color: AppColors.muted,
              ),
              const SizedBox(width: 6),
              Flexible(
                child: Text(
                  cart.phuongThuc == PhuongThuc.cash
                      ? 'Đặt món xong, vui lòng trả tiền mặt tại quầy'
                      : 'Quét mã QR bằng app ngân hàng để thanh toán',
                  textAlign: TextAlign.center,
                  style: const TextStyle(fontSize: 12, color: AppColors.muted),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// Ô chọn "Chuyển khoản QR" / "Tiền mặt"
class _MethodOption extends StatelessWidget {
  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _MethodOption({
    required this.icon,
    required this.label,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: selected ? AppColors.cream100 : Colors.white,
      borderRadius: BorderRadius.circular(10),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(10),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 150),
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 12),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(10),
            border: Border.all(
              color: selected ? AppColors.brown700 : AppColors.line,
              width: selected ? 1.6 : 1,
            ),
          ),
          child: Row(
            children: [
              Icon(
                icon,
                size: 20,
                color: selected ? AppColors.brown700 : AppColors.muted,
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                    color: AppColors.text,
                  ),
                ),
              ),
              Icon(
                selected ? Icons.radio_button_checked : Icons.radio_button_off,
                size: 18,
                color: selected ? AppColors.brown700 : AppColors.line,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
