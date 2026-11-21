import 'package:flutter/foundation.dart';

/// Địa chỉ Backend (CoffeeShop.Api — cổng 3000, prefix /api/v1).
///
/// Ghi đè khi chạy trên tablet thật trong cùng mạng LAN:
///   flutter run --dart-define=API_BASE_URL=http://192.168.1.10:3000/api/v1
class ApiConfig {
  static const _override = String.fromEnvironment('API_BASE_URL');

  static String get baseUrl {
    if (_override.isNotEmpty) return _override;
    // Android emulator: 10.0.2.2 chính là "localhost" của máy chạy API
    if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) {
      return 'http://10.0.2.2:3000/api/v1';
    }
    // 127.0.0.1 thay vì localhost: tránh phân giải IPv6 (::1) chậm/treo trên Windows
    return 'http://127.0.0.1:3000/api/v1';
  }

  /// Gốc server (không có /api/v1) — dùng ghép ảnh nếu HinhAnh là đường dẫn tương đối
  static String get origin => Uri.parse(baseUrl).replace(path: '').toString();

  static const timeout = Duration(seconds: 15);
}

/// HinhAnh trong DB có thể là null, URL đầy đủ hoặc đường dẫn tương đối.
String resolveImageUrl(String? path) {
  if (path == null || path.trim().isEmpty) return '';
  if (path.startsWith('http://') || path.startsWith('https://')) return path;
  return '${ApiConfig.origin}${path.startsWith('/') ? '' : '/'}$path';
}
