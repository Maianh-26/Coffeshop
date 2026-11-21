import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import '../config/api_config.dart';

/// Lỗi từ API — theo định dạng chung { "error": { "code", "message" } } của Backend.
class ApiException implements Exception {
  final String code;
  final String message;
  final int? status;

  /// error.details (vd. HET_NGUYEN_LIEU: tenNguyenLieu, canDung, conTon)
  final List details;
  const ApiException(
    this.code,
    this.message, {
    this.status,
    this.details = const [],
  });

  @override
  String toString() => message;
}

class ApiClient {
  final http.Client _http;
  final String baseUrl;

  ApiClient({http.Client? client, String? baseUrl})
    : _http = client ?? http.Client(),
      baseUrl = baseUrl ?? ApiConfig.baseUrl;

  Future<dynamic> get(String path, [Map<String, Object?>? query]) {
    final uri = Uri.parse('$baseUrl$path').replace(
      queryParameters: query == null
          ? null
          : {
              for (final e in query.entries)
                if (e.value != null) e.key: '${e.value}',
            },
    );
    return _send(() => _http.get(uri, headers: {'Accept': 'application/json'}));
  }

  static const _jsonHeaders = {
    'Accept': 'application/json',
    'Content-Type': 'application/json; charset=utf-8',
  };

  Future<dynamic> post(String path, Object body) => _send(
    () => _http.post(
      Uri.parse('$baseUrl$path'),
      headers: _jsonHeaders,
      body: jsonEncode(body),
    ),
  );

  Future<dynamic> put(String path, Object body) => _send(
    () => _http.put(
      Uri.parse('$baseUrl$path'),
      headers: _jsonHeaders,
      body: jsonEncode(body),
    ),
  );

  Future<dynamic> _send(Future<http.Response> Function() request) async {
    final http.Response res;
    try {
      res = await request().timeout(ApiConfig.timeout);
    } on TimeoutException {
      throw const ApiException(
        'TIMEOUT',
        'Máy chủ phản hồi quá lâu. Vui lòng thử lại.',
      );
    } catch (_) {
      throw ApiException(
        'NETWORK',
        'Không kết nối được máy chủ ($baseUrl). Kiểm tra API đã chạy chưa.',
      );
    }

    final body = res.body.isEmpty
        ? null
        : jsonDecode(utf8.decode(res.bodyBytes));
    if (res.statusCode >= 200 && res.statusCode < 300) return body;

    final err = body is Map ? body['error'] : null;
    throw ApiException(
      err is Map ? '${err['code']}' : 'HTTP_${res.statusCode}',
      err is Map ? '${err['message']}' : 'Lỗi máy chủ (${res.statusCode})',
      status: res.statusCode,
      details: err is Map && err['details'] is List
          ? err['details'] as List
          : const [],
    );
  }

  void close() => _http.close();
}
