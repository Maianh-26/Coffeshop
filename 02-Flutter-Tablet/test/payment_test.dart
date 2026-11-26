import 'package:coffee_app/cart.dart';
import 'package:coffee_app/payment/vietqr.dart';
import 'package:coffee_app/services/order_api.dart';
import 'package:flutter_test/flutter_test.dart';

import 'widget_test.dart' show bacXiu;

class FakeOrderApi extends OrderApi {
  int calls = 0;
  int switches = 0;
  List<Map<String, dynamic>>? lastBody;
  PhuongThuc? lastMethod;

  @override
  Future<DonHang> datMon(
    List<Map<String, dynamic>> chiTiet,
    PhuongThuc phuongThuc,
  ) async {
    calls++;
    lastBody = chiTiet;
    lastMethod = phuongThuc;
    return DonHang(
      maDonHang: 100 + calls,
      tongTien: 57600,
      trangThaiThanhToan: 'PENDING',
      phuongThuc: phuongThuc,
    );
  }

  @override
  Future<TrangThaiDon> doiPhuongThuc(int maDonHang, PhuongThuc pt) async {
    switches++;
    lastMethod = pt;
    return TrangThaiDon('PENDING', pt);
  }
}

CartStore _cart() => CartStore()
  ..add(
    CartLine(
      product: bacXiu,
      size: bacXiu.defaultSize!,
      ice: '50%',
      sugar: '50%',
      qty: 2,
    ),
  );

void main() {
  test('CRC16 khớp mã VietQR tĩnh thật của quán (assets/images/vietqr.jpg)', () {
    const real =
        '00020101021138570010A000000727012700069704220113VQRQAFCOL70240208QRIBFTTA53037045802VN62150107NPS68690800630425E6';
    final body = real.substring(0, real.length - 4);
    expect(crc16(body), '25E6');
  });

  test('QR động có số tiền + mã đơn, đúng tài khoản quán', () {
    final p = buildVietQr(amount: 117400, addInfo: 'DH12');
    expect(p.startsWith('000201010212'), true); // 12 = QR động
    expect(p, contains('0006970422')); // MB Bank
    expect(p, contains('0113VQRQAFCOL7024'));
    expect(p, contains('5406117400')); // trường 54: số tiền
    expect(p, contains('62080804DH12')); // trường 62.08: nội dung CK
    expect(crc16(p.substring(0, p.length - 4)), p.substring(p.length - 4));
  });

  test('Mở lại QR không tạo đơn trùng; đổi giỏ thì tạo đơn mới', () async {
    final api = FakeOrderApi();
    final cart = _cart();
    final a = await cart.checkout(api);
    final b = await cart.checkout(api);
    expect(api.calls, 1);
    expect(b.maDonHang, a.maDonHang);
    expect(api.lastBody, [
      {'maChiTietMon': 6, 'soLuong': 2, 'ghiChu': 'Đá 50%, đường 50%'},
    ]);

    cart.setQty(cart.lines.first.key, 3);
    final c = await cart.checkout(api);
    expect(api.calls, 2);
    expect(c.maDonHang, isNot(a.maDonHang));
  });

  test(
    'Đặt bằng tiền mặt gửi CASH; đổi QR <-> tiền mặt dùng lại đơn cũ',
    () async {
      final api = FakeOrderApi();
      final cart = _cart()..phuongThuc = PhuongThuc.cash;
      final a = await cart.checkout(api);
      expect(api.lastMethod, PhuongThuc.cash);
      expect(a.phuongThuc, PhuongThuc.cash);

      cart.phuongThuc = PhuongThuc.qr;
      final b = await cart.checkout(api);
      expect(api.calls, 1); // không tạo đơn mới
      expect(api.switches, 1);
      expect(b.maDonHang, a.maDonHang);
      expect(b.phuongThuc, PhuongThuc.qr);

      // Mở lại cùng cách -> không gọi API nữa
      await cart.checkout(api);
      expect(api.switches, 1);
    },
  );
}
