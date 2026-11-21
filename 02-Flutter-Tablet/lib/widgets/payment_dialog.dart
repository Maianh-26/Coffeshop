import 'dart:async';
import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../cart.dart';
import '../data.dart';
import '../payment/vietqr.dart';
import '../services/api_client.dart';
import '../services/order_api.dart';
import '../theme.dart';
import 'common.dart';

/// Popup "Thanh toán đơn hàng": tạo đơn trên server theo cách khách chọn trong giỏ
///  - QR: hiện mã VietQR có sẵn số tiền,
///  - Tiền mặt: hiện "Mời thanh toán tại quầy" + mã đơn để nhân viên xác nhận trên web admin,
/// rồi tự hỏi server đến khi đơn PAID.
/// Trả về đơn đã thanh toán (phuongThuc = cách quán thực sự đã thu), hoặc null nếu khách đóng popup / đơn bị hủy.
Future<DonHang?> showPaymentDialog(BuildContext context) =>
    _showPopup<DonHang>(context, const _PaymentDialog());

/// Popup "Thanh toán thành công" — chỉ đóng bằng nút X.
Future<void> showPaymentSuccess(BuildContext context, DonHang order) =>
    _showPopup<void>(context, _SuccessDialog(order), dismissible: false);

Future<T?> _showPopup<T>(
  BuildContext context,
  Widget child, {
  bool dismissible = true,
}) {
  return showGeneralDialog<T>(
    context: context,
    barrierDismissible: dismissible,
    barrierLabel: 'Đóng',
    barrierColor: AppColors.text.withValues(alpha: 0.45),
    transitionDuration: const Duration(milliseconds: 200),
    pageBuilder: (ctx, a1, a2) => child,
    transitionBuilder: (ctx, anim, _, child) {
      final curved = CurvedAnimation(parent: anim, curve: Curves.easeOut);
      return BackdropFilter(
        filter: ImageFilter.blur(
          sigmaX: 3 * anim.value,
          sigmaY: 3 * anim.value,
        ),
        child: FadeTransition(
          opacity: curved,
          child: ScaleTransition(
            scale: Tween(begin: 0.96, end: 1.0).animate(curved),
            child: child,
          ),
        ),
      );
    },
  );
}

class _PaymentDialog extends StatefulWidget {
  const _PaymentDialog();

  @override
  State<_PaymentDialog> createState() => _PaymentDialogState();
}

class _PaymentDialogState extends State<_PaymentDialog> {
  late Future<DonHang> _order;

  /// Tổng tiền app hiển thị trong giỏ (để so với tổng server tính)
  late final int _cartTotal;

  /// Hỏi server định kỳ "đơn đã thanh toán chưa"
  static const pollEvery = Duration(seconds: 3);
  Timer? _poll;
  bool _checking = false;
  bool _cancelled = false;

  @override
  void initState() {
    super.initState();
    _cartTotal = context.read<CartStore>().total;
    _order = _create();
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  Future<DonHang> _create() {
    final f = context.read<CartStore>().checkout(context.read<OrderApi>());
    // Có đơn rồi -> bắt đầu hỏi trạng thái
    f.then(_startPolling, onError: (_) {});
    return f;
  }

  void _retry() {
    final next = _create();
    setState(() {
      _order = next;
    });
  }

  void _startPolling(DonHang order) {
    _poll?.cancel();
    _poll = Timer.periodic(pollEvery, (_) => _check(order));
  }

  Future<void> _check(DonHang order) async {
    if (_checking || !mounted) return;
    _checking = true;
    try {
      final status = await context.read<OrderApi>().trangThai(order.maDonHang);
      if (!mounted) return;
      if (status.trangThai == 'PAID') {
        _poll?.cancel();
        context.read<CartStore>().clear();
        // Báo cho màn hình gọi: đã thanh toán — kèm cách quán đã thu
        // (web admin bấm "xác nhận tiền mặt" -> CASH, kể cả khi khách đang mở QR)
        Navigator.of(context).pop(
          order.copyWith(
            trangThaiThanhToan: 'PAID',
            phuongThuc: status.phuongThuc,
          ),
        );
      } else if (status.trangThai == 'CANCELLED') {
        _poll?.cancel();
        setState(() => _cancelled = true);
      }
    } catch (_) {
      // Mạng chập chờn: bỏ qua, lần hỏi sau thử lại
    } finally {
      _checking = false;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Material(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          elevation: 20,
          shadowColor: Colors.black38,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 360),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(20, 24, 20, 20),
              child: FutureBuilder<DonHang>(
                future: _order,
                builder: (context, snap) {
                  if (snap.hasError) return _error(snap.error!);
                  if (!snap.hasData) return _loading();
                  if (_cancelled) {
                    return _error(
                      'Đơn ${snap.data!.maHienThi} đã bị hủy. '
                      'Vui lòng liên hệ nhân viên.',
                      canRetry: false,
                    );
                  }
                  final order = snap.data!;
                  return order.phuongThuc == PhuongThuc.cash
                      ? _cash(order)
                      : _qr(order);
                },
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _header() => Column(
    children: [
      Container(
        padding: const EdgeInsets.all(2),
        decoration: const BoxDecoration(
          shape: BoxShape.circle,
          color: AppColors.brown700,
        ),
        child: const BrandLogo(size: 56),
      ),
      const SizedBox(height: 12),
      Text(
        'Thanh toán đơn hàng',
        style: serif(size: 20, weight: FontWeight.w500),
      ),
      const SizedBox(height: 14),
    ],
  );

  Widget _back() => LightButton(
    expand: true,
    label: 'Quay lại giỏ hàng',
    onPressed: () => Navigator.of(context).pop(),
  );

  Widget _loading() => Column(
    mainAxisSize: MainAxisSize.min,
    children: [
      _header(),
      const SizedBox(height: 20),
      const CircularProgressIndicator(color: AppColors.brown700),
      const SizedBox(height: 14),
      const Text(
        'Đang tạo đơn hàng...',
        style: TextStyle(color: AppColors.muted, fontSize: 13),
      ),
      const SizedBox(height: 24),
    ],
  );

  Widget _error(Object e, {bool canRetry = true}) {
    var msg = e.toString();
    // 422 HET_NGUYEN_LIEU: liệt kê nguyên liệu thiếu
    if (e is ApiException && e.details.isNotEmpty) {
      msg +=
          '\n${e.details.whereType<Map>().map((d) => '• ${d['tenNguyenLieu']}: cần ${d['canDung']}, còn ${d['conTon']} ${d['donViTinh']}').join('\n')}';
    }
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        _header(),
        const Icon(Icons.error_outline, size: 34, color: AppColors.red),
        const SizedBox(height: 10),
        Text(
          msg,
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 13, color: AppColors.text),
        ),
        const SizedBox(height: 16),
        if (canRetry) ...[
          DarkButton(
            expand: true,
            icon: Icons.refresh,
            label: 'Thử lại',
            onPressed: _retry,
          ),
          const SizedBox(height: 8),
        ],
        _back(),
      ],
    );
  }

  Widget _qr(DonHang order) {
    final payload = buildVietQr(
      amount: order.tongTien,
      addInfo: order.maHienThi,
    );
    final priceChanged = order.tongTien != _cartTotal;

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        _header(),
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: AppColors.cream100,
            borderRadius: BorderRadius.circular(12),
          ),
          child: Column(
            children: [
              const Text(
                'Quét mã bằng app ngân hàng',
                style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
              ),
              const SizedBox(height: 8),
              _chip('Mã đơn', order.maHienThi),
              const SizedBox(height: 10),
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(10),
                ),
                // Đen trên nền trắng (style mặc định) — app ngân hàng quét ổn định nhất
                child: QrImageView(
                  data: payload,
                  size: 220,
                  padding: EdgeInsets.zero,
                  backgroundColor: Colors.white,
                ),
              ),
              const SizedBox(height: 10),
              const Text(
                'Số tiền cần chuyển',
                style: TextStyle(fontSize: 11.5, color: AppColors.muted),
              ),
              Text(
                formatVnd(order.tongTien),
                style: const TextStyle(
                  fontSize: 24,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 4),
              const Text(
                'Số tiền và nội dung đã được điền sẵn trong mã QR',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 11.5, color: AppColors.muted),
              ),
            ],
          ),
        ),
        if (priceChanged) ...[
          const SizedBox(height: 10),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            decoration: BoxDecoration(
              color: AppColors.redSoft,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Text(
              'Tổng tiền đã được cập nhật theo giá tại thời điểm tạo đơn '
              '(giỏ hàng: ${formatVnd(_cartTotal)}).',
              style: const TextStyle(fontSize: 12, color: AppColors.redText),
            ),
          ),
        ],
        const SizedBox(height: 14),
        // Không còn nút "Tôi đã chuyển khoản": hệ thống tự phát hiện khi đơn PAID
        _waiting('Đang chờ xác nhận thanh toán...'),
        const SizedBox(height: 12),
        _back(),
      ],
    );
  }

  /// Tiền mặt: khách ra quầy trả tiền, nhân viên bấm "Xác nhận đã thanh toán tiền mặt" trên web admin
  Widget _cash(DonHang order) {
    final priceChanged = order.tongTien != _cartTotal;
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        _header(),
        Container(
          width: double.infinity,
          padding: const EdgeInsets.fromLTRB(14, 18, 14, 18),
          decoration: BoxDecoration(
            color: AppColors.cream100,
            borderRadius: BorderRadius.circular(12),
          ),
          child: Column(
            children: [
              Container(
                width: 60,
                height: 60,
                decoration: const BoxDecoration(
                  shape: BoxShape.circle,
                  color: Colors.white,
                ),
                child: const Icon(
                  Icons.payments_outlined,
                  size: 32,
                  color: AppColors.brown700,
                ),
              ),
              const SizedBox(height: 12),
              const Text(
                'Mời bạn thanh toán tiền mặt tại quầy',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
              ),
              const SizedBox(height: 6),
              const Text(
                'Vui lòng đọc mã đơn cho nhân viên thu ngân',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 12, color: AppColors.muted),
              ),
              const SizedBox(height: 14),
              Text(
                order.maHienThi,
                style: serif(
                  size: 34,
                  weight: FontWeight.w700,
                ).copyWith(letterSpacing: 2),
              ),
              const SizedBox(height: 12),
              const Text(
                'Số tiền cần trả',
                style: TextStyle(fontSize: 11.5, color: AppColors.muted),
              ),
              Text(
                formatVnd(order.tongTien),
                style: const TextStyle(
                  fontSize: 24,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),
        ),
        if (priceChanged) ...[
          const SizedBox(height: 10),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            decoration: BoxDecoration(
              color: AppColors.redSoft,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Text(
              'Tổng tiền đã được cập nhật theo giá tại thời điểm tạo đơn '
              '(giỏ hàng: ${formatVnd(_cartTotal)}).',
              style: const TextStyle(fontSize: 12, color: AppColors.redText),
            ),
          ),
        ],
        const SizedBox(height: 14),
        _waiting('Đang chờ nhân viên xác nhận đã thu tiền...'),
        const SizedBox(height: 12),
        _back(),
      ],
    );
  }

  Widget _waiting(String text) => Row(
    mainAxisAlignment: MainAxisAlignment.center,
    children: [
      const SizedBox(
        width: 14,
        height: 14,
        child: CircularProgressIndicator(
          strokeWidth: 2,
          color: AppColors.brown700,
        ),
      ),
      const SizedBox(width: 8),
      Flexible(
        child: Text(
          text,
          style: const TextStyle(fontSize: 12.5, color: AppColors.muted),
        ),
      ),
    ],
  );

  Widget _chip(String label, String value) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
    decoration: BoxDecoration(
      color: Colors.white,
      borderRadius: BorderRadius.circular(999),
      border: Border.all(color: AppColors.cream300),
    ),
    child: Text.rich(
      TextSpan(
        text: '$label: ',
        style: const TextStyle(fontSize: 12.5, color: AppColors.muted),
        children: [
          TextSpan(
            text: value,
            style: const TextStyle(
              fontWeight: FontWeight.w700,
              color: AppColors.text,
              letterSpacing: 0.5,
            ),
          ),
        ],
      ),
    ),
  );
}

/// Popup "Thanh toán thành công" / "Đã thanh toán bằng tiền mặt" — đóng bằng nút X góc trên.
class _SuccessDialog extends StatelessWidget {
  final DonHang order;
  const _SuccessDialog(this.order);

  @override
  Widget build(BuildContext context) {
    final cash = order.phuongThuc == PhuongThuc.cash;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Material(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          elevation: 20,
          shadowColor: Colors.black38,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 340),
            child: Stack(
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(24, 36, 24, 28),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Container(
                        width: 72,
                        height: 72,
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          color: AppColors.green.withValues(alpha: 0.12),
                        ),
                        child: const Icon(
                          Icons.check_circle,
                          size: 48,
                          color: AppColors.green,
                        ),
                      ),
                      const SizedBox(height: 16),
                      Text(
                        cash
                            ? 'Đã thanh toán bằng tiền mặt'
                            : 'Thanh toán thành công',
                        textAlign: TextAlign.center,
                        style: serif(size: 21, weight: FontWeight.w600),
                      ),
                      const SizedBox(height: 6),
                      Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(
                            cash ? Icons.payments_outlined : Icons.qr_code_2,
                            size: 15,
                            color: AppColors.muted,
                          ),
                          const SizedBox(width: 5),
                          Text(
                            cash ? 'Tiền mặt tại quầy' : 'Chuyển khoản QR',
                            style: const TextStyle(
                              fontSize: 12.5,
                              color: AppColors.muted,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 8),
                      Text(
                        'Đơn ${order.maHienThi} · ${formatVnd(order.tongTien)}',
                        style: const TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      const SizedBox(height: 10),
                      const Text(
                        'Cảm ơn bạn! Quán đang pha chế, nhân viên sẽ mang món ra tận bàn.',
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          fontSize: 12.5,
                          color: AppColors.muted,
                          height: 1.5,
                        ),
                      ),
                    ],
                  ),
                ),
                Positioned(
                  top: 6,
                  right: 6,
                  child: IconButton(
                    tooltip: 'Đóng',
                    icon: const Icon(Icons.close, color: AppColors.muted),
                    onPressed: () => Navigator.of(context).pop(),
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
