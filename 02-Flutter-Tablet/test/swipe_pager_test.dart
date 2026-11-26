import 'package:coffee_app/widgets/common.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets(
    'SwipePager: vuốt trái sang trang sau, vuốt phải về trang trước',
    (t) async {
      var page = 0;
      await t.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: StatefulBuilder(
              builder: (context, setState) => SwipePager(
                page: page,
                pageCount: 3,
                onChanged: (p) => setState(() => page = p),
                child: SizedBox(
                  height: 300,
                  child: Center(child: Text('Trang ${page + 1}')),
                ),
              ),
            ),
          ),
        ),
      );
      expect(find.text('Trang 1'), findsOneWidget);

      await t.fling(find.text('Trang 1'), const Offset(-300, 0), 1000);
      await t.pumpAndSettle();
      expect(page, 1);
      expect(find.text('Trang 2'), findsOneWidget);

      await t.fling(find.text('Trang 2'), const Offset(300, 0), 1000);
      await t.pumpAndSettle();
      expect(page, 0);

      // Trang đầu: vuốt phải không đổi trang
      await t.fling(find.text('Trang 1'), const Offset(300, 0), 1000);
      await t.pumpAndSettle();
      expect(page, 0);

      // Kéo ngắn, chậm -> bật về, không đổi trang
      await t.drag(find.text('Trang 1'), const Offset(-40, 0));
      await t.pumpAndSettle();
      expect(page, 0);
    },
  );
}
