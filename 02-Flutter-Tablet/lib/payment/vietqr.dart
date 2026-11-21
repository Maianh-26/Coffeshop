// Sinh nội dung mã VietQR động (chuẩn EMVCo / NAPAS 247) — có sẵn số tiền + nội dung chuyển khoản.
// Khách quét bằng app ngân hàng bất kỳ: số tiền và nội dung được điền tự động, không sửa được số tiền.

/// Tài khoản nhận tiền của quán (giải mã từ assets/images/vietqr.jpg).
/// Đổi tài khoản khi build:
///   --dart-define=BANK_BIN=970422 --dart-define=BANK_ACCOUNT=VQRQAFCOL7024
class BankConfig {
  /// Mã BIN ngân hàng (970422 = MB Bank)
  static const bin = String.fromEnvironment('BANK_BIN', defaultValue: '970422');
  static const account = String.fromEnvironment(
    'BANK_ACCOUNT',
    defaultValue: 'VQRQAFCOL7024',
  );
}

/// Một trường EMVCo: ID (2 số) + độ dài (2 số) + giá trị
String _tlv(String id, String value) =>
    '$id${value.length.toString().padLeft(2, '0')}$value';

/// CRC16-CCITT-FALSE (poly 0x1021, init 0xFFFF) — trường 63 bắt buộc của EMVCo
String crc16(String data) {
  var crc = 0xFFFF;
  for (final c in data.codeUnits) {
    crc ^= c << 8;
    for (var i = 0; i < 8; i++) {
      crc = (crc & 0x8000) != 0 ? ((crc << 1) ^ 0x1021) : (crc << 1);
      crc &= 0xFFFF;
    }
  }
  return crc.toRadixString(16).toUpperCase().padLeft(4, '0');
}

/// Nội dung chuyển khoản chỉ nên dùng chữ không dấu, số, khoảng trắng (tối đa 25 ký tự)
String _sanitize(String s) {
  final clean = s.replaceAll(RegExp(r'[^A-Za-z0-9 ]'), '');
  return clean.length > 25 ? clean.substring(0, 25) : clean;
}

/// Payload VietQR động. [amount] tính bằng đồng (VND).
String buildVietQr({
  required int amount,
  required String addInfo,
  String bin = BankConfig.bin,
  String account = BankConfig.account,
}) {
  final merchant = _tlv(
    '38',
    _tlv('00', 'A000000727') + // AID của NAPAS
        _tlv('01', _tlv('00', bin) + _tlv('01', account)) +
        _tlv('02', 'QRIBFTTA'), // chuyển nhanh đến tài khoản
  );
  final payload = [
    _tlv('00', '01'),
    _tlv('01', '12'), // 12 = QR động (dùng 1 lần, có số tiền)
    merchant,
    _tlv('53', '704'), // VND
    _tlv('54', '$amount'),
    _tlv('58', 'VN'),
    _tlv('62', _tlv('08', _sanitize(addInfo))),
    '6304', // CRC tính trên toàn bộ chuỗi, kể cả "6304"
  ].join();
  return payload + crc16(payload);
}
