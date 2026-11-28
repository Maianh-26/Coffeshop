// Danh mục, món, giá, khuyến mãi, topping đều lấy từ API (xem providers/menu_provider.dart).
// File này chỉ còn các hằng số giao diện không có trong CSDL.

/// Đá / đường không có cột trong CSDL -> gửi kèm vào ChiTietDonHang.GhiChu khi đặt món.
const iceLevels = ['50%', '100%'];
const sugarLevels = ['30%', '50%', '100%'];

const aboutHeroImage =
    'https://images.unsplash.com/photo-1447933601403-0c6688de566e?auto=format&fit=crop&w=1200&q=70';

/// Mô tả món ở trang Giới thiệu (bảng TenMon chưa có cột MoTa) — tra theo tên món.
const productDescriptions = {
  'cà phê sữa đá':
      'Được pha chế từ cà phê rang xay đậm đặc kết hợp sữa đặc và đá viên. Mang lại hương vị đậm đà, đắng nhẹ đặc trưng xen lẫn vị ngọt béo ngậy, giúp sảng khoái tức thì.',
  'cà phê đen':
      'Cà phê phin nguyên chất pha đậm, rót qua đá lạnh. Vị đắng mạnh mẽ, hậu ngọt nhẹ và thơm mùi rang – dành cho người thích cà phê "thật".',
  'bạc xỉu':
      'Nhiều sữa, ít cà phê: sữa tươi và sữa đặc hòa cùng một chút cà phê phin. Ngọt dịu, béo thơm, dễ uống cho cả người mới làm quen với cà phê.',
  'trà đào cam sả':
      'Trà đen ủ thơm kết hợp đào ngâm giòn ngọt, cam tươi và sả. Thanh mát, chua ngọt hài hòa, thơm nhẹ mùi sả – giải nhiệt tức thì.',
  'hồng trà sữa':
      'Hồng trà đậm vị pha cùng sữa theo công thức truyền thống. Thơm béo, ngọt vừa, hợp nhất khi đi kèm trân châu đen dai mềm.',
  'tiramisu':
      'Bánh tiramisu mềm mịn với lớp kem mascarpone béo nhẹ, cốt bánh thấm cà phê espresso và phủ bột cacao – món ngọt hoàn hảo đi kèm cà phê.',
  'cà phê latte':
      'Được tạo nên từ espresso đậm đặc kết hợp với sữa tươi đánh nóng và lớp bọt sữa mỏng mịn. Hương vị cân bằng, béo nhẹ, êm dịu.',
  'cà phê ủ lạnh':
      'Những hạt cà phê xay thô được ngâm trong nước lạnh từ 12 đến 24 giờ. Quá trình chiết xuất chậm giúp giữ lại hương thơm tự nhiên, vị chua thanh nhẹ và hậu ngọt sâu.',
  'cà phê muối':
      'Cà phê đậm phủ lớp kem muối mặn nhẹ, béo mịn. Vị mặn khéo léo làm nổi bật vị ngọt và giảm đắng, tạo hậu vị cân bằng khó quên.',
  'espresso':
      'Một shot cà phê chiết xuất dưới áp suất cao trong khoảng 25 giây. Đậm đặc, lớp crema vàng óng, hậu vị đắng thơm kéo dài – tinh chất của mọi ly cà phê Ý.',
  'cappuccino':
      'Espresso kết hợp sữa nóng và lớp bọt sữa dày mịn theo tỉ lệ cân bằng. Thơm béo, vị cà phê rõ nét, điểm xuyết latte art trên mặt ly.',
  'americano':
      'Espresso pha loãng với nước nóng, giữ trọn hương thơm nhưng nhẹ nhàng hơn. Vị đắng thanh, ít béo – phù hợp cho những ai thích cà phê đen dịu.',
  'trà bưởi':
      'Trà xanh thanh nhẹ hòa cùng tép bưởi hồng mọng nước. Vị chua dịu, đắng nhẹ đặc trưng của bưởi, thơm mát và ít ngọt.',
  'trà sữa trân châu đường đen':
      'Sữa tươi thanh trùng béo mịn hòa quyện cùng trân châu dẻo dai nấu đường đen thơm lừng. Vị ngọt đậm đà, hấp dẫn khó cưỡng.',
  'red velvet':
      'Sự hòa quyện tinh tế giữa cốt bánh bông lan nhung đỏ mềm ẩm và lớp kem phô mai béo ngậy, chua nhẹ. Vị ngọt thanh tao, tan ngay đầu lưỡi cho buổi chiều thư giãn.',
  'bánh mousse chocolate bạc hà':
      'Lớp mousse sô-cô-la đen đậm đà hòa quyện cùng kem bạc hà thanh mát, tạo cảm giác bùng nổ đầy sảng khoái. Vị đắng nhẹ xen lẫn hương thơm dịu mát, tan ngay đầu lưỡi.',
  'bông lan kem tươi':
      'Cốt bánh bông lan mềm mịn, xốp nhẹ phủ lớp kem tươi béo ngậy nhưng không hề gây ngán. Vị ngọt thanh tao hòa quyện cùng hương sữa thơm dịu cho phút giây thư giãn.',
  'cà phê cốt dừa':
      'Sự kết hợp độc đáo giữa cà phê phin đậm đà và cốt dừa xay béo ngậy, mát lạnh. Mang lại hương vị thơm bùi đặc trưng, ngọt dịu hài hòa, đánh thức mọi giác quan.',
  'bánh cookie':
      'Bánh quy nướng vàng giòn rụm bên ngoài, mềm ẩm bên trong với những hạt sô-cô-la chip tan chảy đậm đà. Hương bơ thơm lừng hòa quyện vị ngọt dịu, cực kỳ bắt vị khi dùng kèm cà phê.',
};



String describe(String tenMon) =>
    productDescriptions[tenMon.trim().toLowerCase()] ?? '';

String formatVnd(int n) {
  final s = n.abs().toString();
  final buf = StringBuffer();
  for (var i = 0; i < s.length; i++) {
    if (i > 0 && (s.length - i) % 3 == 0) buf.write('.');
    buf.write(s[i]);
  }
  return '${n < 0 ? '-' : ''}${buf.toString()}đ';
}
