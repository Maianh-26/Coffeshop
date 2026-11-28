import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

class AppColors {
  static const brown900 = Color(0xFF3A2620);
  static const brown700 = Color(0xFF5C3D33);
  static const cream50 = Color(0xFFFBF9F5);
  static const cream100 = Color(0xFFF5F0E8);
  static const cream200 = Color(0xFFECE4D8);
  static const cream300 = Color(0xFFE2D8C9);
  static const text = Color(0xFF2B1D18);
  static const muted = Color(0xFF8A7D74);
  static const line = Color(0xFFEBE5DC);
  static const red = Color(0xFFD9482B);
  static const redSoft = Color(0xFFFBE9E3);
  static const redText = Color(0xFFA8371F);
  static const green = Color(0xFF3B7A4B);
}

TextStyle serif({
  double size = 16,
  FontWeight weight = FontWeight.w600,
  Color? color,
  double? height,
}) => GoogleFonts.lora(
  fontSize: size,
  fontWeight: weight,
  color: color ?? AppColors.text,
  height: height,
);

ThemeData buildTheme() {
  final base = ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(
      seedColor: AppColors.brown900,
      primary: AppColors.brown900,
      surface: AppColors.cream50,
    ),
    scaffoldBackgroundColor: AppColors.cream50,
  );
  return base.copyWith(
    textTheme: GoogleFonts.beVietnamProTextTheme(
      base.textTheme,
    ).apply(bodyColor: AppColors.text, displayColor: AppColors.text),
  );
}

/// Breakpoints giống bản web
bool isWide(BuildContext c) => MediaQuery.sizeOf(c).width >= 1024;
bool isPhone(BuildContext c) => MediaQuery.sizeOf(c).width < 760;
