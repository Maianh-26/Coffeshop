import 'api_client.dart';

/// Cách khách trả tiền — khớp cột DonHang.PhuongThucThanhToan
enum PhuongThuc {
  qr('QR_TRANSFER'),
  cash('CASH');

  final String code;
  const PhuongThuc(this.code);

  static PhuongThuc parse(String? code) =>
      code == cash.code ? PhuongThuc.cash : PhuongThuc.qr;
}

/// Phần cần dùng của DonHangDto (POST /don-hang trả về)
class DonHang {
  final int maDonHang;

  /// Tổng tiền do SERVER tính tại lúc tạo đơn (giá KM theo giờ server) — số tiền khách phải trả
  final int tongTien;
  final String trangThaiThanhToan;
  final PhuongThuc phuongThuc;
  final DateTime? thoiGianTao;

  const DonHang({
    required this.maDonHang,
    required this.tongTien,
    required this.trangThaiThanhToan,
    required this.phuongThuc,
    this.thoiGianTao,
  });

  factory DonHang.fromJson(Map<String, dynamic> j) => DonHang(
    maDonHang: j['maDonHang'] as int,
    tongTien: (j['tongTien'] as num).round(),
    trangThaiThanhToan: j['trangThaiThanhToan'] as String,
    phuongThuc: PhuongThuc.parse(j['phuongThucThanhToan'] as String?),
    thoiGianTao: DateTime.tryParse('${j['thoiGianTao']}'),
  );

  DonHang copyWith({String? trangThaiThanhToan, PhuongThuc? phuongThuc}) =>
      DonHang(
        maDonHang: maDonHang,
        tongTien: tongTien,
        trangThaiThanhToan: trangThaiThanhToan ?? this.trangThaiThanhToan,
        phuongThuc: phuongThuc ?? this.phuongThuc,
        thoiGianTao: thoiGianTao,
      );

  /// Mã đơn hiển thị + nội dung chuyển khoản (đối soát với DonHang.MaDonHang)
  String get maHienThi => 'DH$maDonHang';
}

/// GET /don-hang/{id}/trang-thai
class TrangThaiDon {
  /// PENDING | PAID | CANCELLED
  final String trangThai;

  /// Cách quán thực sự đã thu (admin xác nhận tiền mặt -> CASH, webhook ngân hàng -> QR_TRANSFER)
  final PhuongThuc phuongThuc;

  const TrangThaiDon(this.trangThai, this.phuongThuc);

  factory TrangThaiDon.fromJson(Map<String, dynamic> j) => TrangThaiDon(
    j['trangThaiThanhToan'] as String,
    PhuongThuc.parse(j['phuongThucThanhToan'] as String?),
  );
}

class OrderApi {
  final ApiClient _client;
  OrderApi({ApiClient? client}) : _client = client ?? ApiClient();

  /// POST /don-hang — khách tự đặt trên tablet (không cần token)
  Future<DonHang> datMon(
    List<Map<String, dynamic>> chiTiet,
    PhuongThuc phuongThuc,
  ) async {
    final res = await _client.post('/don-hang', {
      'phuongThucThanhToan': phuongThuc.code,
      'chiTiet': chiTiet,
    });
    return DonHang.fromJson(res as Map<String, dynamic>);
  }

  /// PUT /don-hang/{id}/phuong-thuc-thanh-toan — đổi QR <-> tiền mặt cho đơn còn PENDING
  Future<TrangThaiDon> doiPhuongThuc(int maDonHang, PhuongThuc pt) async {
    final res = await _client.put(
      '/don-hang/$maDonHang/phuong-thuc-thanh-toan',
      {'phuongThucThanhToan': pt.code},
    );
    return TrangThaiDon.fromJson(res as Map<String, dynamic>);
  }

  /// GET /don-hang/{id}/trang-thai
  Future<TrangThaiDon> trangThai(int maDonHang) async {
    final res = await _client.get('/don-hang/$maDonHang/trang-thai');
    return TrangThaiDon.fromJson(res as Map<String, dynamic>);
  }
}
