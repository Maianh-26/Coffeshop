// Model khớp JSON của CoffeeShop.Api (openapi.yaml). Tên trường = cột DB dạng camelCase.

import '../config/api_config.dart';

int _int(Object? v) => (v as num?)?.round() ?? 0;

/// GET /danh-muc
class DanhMuc {
  final int maDanhMuc;
  final String tenDanhMuc;
  final int soMon;

  const DanhMuc({
    required this.maDanhMuc,
    required this.tenDanhMuc,
    this.soMon = 0,
  });

  factory DanhMuc.fromJson(Map<String, dynamic> j) => DanhMuc(
    maDanhMuc: j['maDanhMuc'] as int,
    tenDanhMuc: j['tenDanhMuc'] as String,
    soMon: (j['soMon'] as int?) ?? 0,
  );
}

/// khuyenMai trong MonDto
class KhuyenMaiNgan {
  final int maKhuyenMai;
  final String code;
  final num phanTramGiam;

  /// Đang trong ngày + khung giờ áp dụng (Backend tự tính)
  final bool dangApDung;
  final String? gioBatDau;
  final String? gioKetThuc;

  const KhuyenMaiNgan({
    required this.maKhuyenMai,
    required this.code,
    required this.phanTramGiam,
    required this.dangApDung,
    this.gioBatDau,
    this.gioKetThuc,
  });

  factory KhuyenMaiNgan.fromJson(Map<String, dynamic> j) => KhuyenMaiNgan(
    maKhuyenMai: j['maKhuyenMai'] as int,
    code: j['code'] as String,
    phanTramGiam: j['phanTramGiam'] as num,
    dangApDung: j['dangApDung'] as bool,
    gioBatDau: j['gioBatDau'] as String?,
    gioKetThuc: j['gioKetThuc'] as String?,
  );

  /// "14:00–16:00" hoặc null nếu áp dụng cả ngày
  String? get khungGio =>
      gioBatDau != null && gioKetThuc != null ? '$gioBatDau–$gioKetThuc' : null;
}

/// Một dòng ChiTietMon (giá theo size) — maChiTietMon là mã gửi lên khi đặt món.
class GiaSize {
  final int maChiTietMon;
  final String maKichThuoc;
  final String tenKichThuoc;
  final int giaBan;
  final int giaSauGiam;
  final bool dangApDung;
  final bool duNguyenLieu;

  const GiaSize({
    required this.maChiTietMon,
    required this.maKichThuoc,
    required this.tenKichThuoc,
    required this.giaBan,
    required this.giaSauGiam,
    this.dangApDung = true,
    this.duNguyenLieu = true,
  });

  factory GiaSize.fromJson(Map<String, dynamic> j) => GiaSize(
    maChiTietMon: j['maChiTietMon'] as int,
    maKichThuoc: j['maKichThuoc'] as String,
    tenKichThuoc: j['tenKichThuoc'] as String,
    giaBan: _int(j['giaBan']),
    giaSauGiam: _int(j['giaSauGiam']),
    dangApDung: j['dangApDung'] as bool,
    duNguyenLieu: j['duNguyenLieu'] as bool,
  );

  bool get chonDuoc => dangApDung && duNguyenLieu;
}

/// GET /mon — TenMon + ChiTietMon
class Mon {
  final int maTenMon;
  final int maDanhMuc;
  final String tenDanhMuc;
  final String tenMon;
  final String? hinhAnh;
  final bool conHang;
  final bool ngungBan;

  /// Đang bán + còn hàng + có ít nhất 1 size đủ nguyên liệu (Backend tự tính)
  final bool coTheBan;
  final KhuyenMaiNgan? khuyenMai;

  /// Chỉ các size DangApDung, sắp theo giá tăng dần (S → M → L)
  final List<GiaSize> kichThuoc;

  const Mon({
    required this.maTenMon,
    required this.maDanhMuc,
    required this.tenDanhMuc,
    required this.tenMon,
    this.hinhAnh,
    this.conHang = true,
    this.ngungBan = false,
    this.coTheBan = true,
    this.khuyenMai,
    required this.kichThuoc,
  });

  factory Mon.fromJson(Map<String, dynamic> j) => Mon(
    maTenMon: j['maTenMon'] as int,
    maDanhMuc: j['maDanhMuc'] as int,
    tenDanhMuc: (j['tenDanhMuc'] as String?) ?? '',
    tenMon: j['tenMon'] as String,
    hinhAnh: j['hinhAnh'] as String?,
    conHang: j['conHang'] as bool,
    ngungBan: j['ngungBan'] as bool,
    coTheBan: j['coTheBan'] as bool,
    khuyenMai: j['khuyenMai'] == null
        ? null
        : KhuyenMaiNgan.fromJson(j['khuyenMai'] as Map<String, dynamic>),
    kichThuoc: [
      for (final s in (j['kichThuoc'] as List? ?? const []))
        GiaSize.fromJson(s as Map<String, dynamic>),
    ].where((s) => s.dangApDung).toList(),
  );

  String get image => resolveImageUrl(hinhAnh);

  /// % giảm đang áp dụng ngay lúc này (0 = không giảm)
  int get discount =>
      khuyenMai?.dangApDung == true ? khuyenMai!.phanTramGiam.round() : 0;

  /// Món bánh không có tùy chọn đá / đường
  bool get isDrink => !tenDanhMuc.toLowerCase().contains('bánh');

  /// Size mặc định: M nếu có, không thì size đầu tiên chọn được
  GiaSize? get defaultSize {
    final ok = kichThuoc.where((s) => s.chonDuoc).toList();
    if (ok.isEmpty) return kichThuoc.isEmpty ? null : kichThuoc.first;
    return ok.firstWhere((s) => s.maKichThuoc == 'M', orElse: () => ok.first);
  }

  /// Giá hiển thị ở thẻ món = size mặc định
  GiaSize? get displaySize => defaultSize;

  GiaSize? sizeByCode(String ma) {
    for (final s in kichThuoc) {
      if (s.maKichThuoc == ma) return s;
    }
    return null;
  }
}

/// GET /topping
class ToppingItem {
  final int maTopping;
  final String tenTopping;
  final int giaThem;
  final bool conHang;
  final bool ngungBan;
  final bool duNguyenLieu;

  const ToppingItem({
    required this.maTopping,
    required this.tenTopping,
    required this.giaThem,
    this.conHang = true,
    this.ngungBan = false,
    this.duNguyenLieu = true,
  });

  factory ToppingItem.fromJson(Map<String, dynamic> j) => ToppingItem(
    maTopping: j['maTopping'] as int,
    tenTopping: j['tenTopping'] as String,
    giaThem: _int(j['giaThem']),
    conHang: j['conHang'] as bool,
    ngungBan: j['ngungBan'] as bool,
    duNguyenLieu: j['duNguyenLieu'] as bool,
  );

  bool get chonDuoc => conHang && !ngungBan && duNguyenLieu;
}
