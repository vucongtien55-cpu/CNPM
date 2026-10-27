# Hướng dẫn chạy dự án bằng Visual Studio 2022

## Chuẩn bị
1. Visual Studio 2022 bản 17.8 trở lên, có workload **ASP.NET and web development** (đi kèm .NET 8 SDK).
2. SQL Server: dùng **LocalDB** (đi kèm Visual Studio) hoặc SQL Server Express/Developer. SSMS hoặc Azure Data Studio để chạy script.

## Bước 1: Tạo cơ sở dữ liệu
1. Mở `Database/MixueDatMonDb.sql` trong SSMS, kết nối vào server rồi bấm **Execute**. Script tự tạo database `MixueDatMonDb`, các bảng, view và dữ liệu mẫu (menu MIXUE, 6 bàn, kho, 2 tài khoản).
2. Với LocalDB, tên server là `(localdb)\MSSQLLocalDB`.

## Bước 2: Cấu hình kết nối
Mở `appsettings.json`, sửa `ConnectionStrings:MacDinh` cho đúng máy của bạn. Ví dụ với SQL Express:

```
Server=.\SQLEXPRESS;Database=MixueDatMonDb;Trusted_Connection=True;TrustServerCertificate=True
```

Sửa thêm mục `CuaHang` (tên quán, địa chỉ, số điện thoại, giờ mở cửa).

## Bước 3: Mở và chạy
1. Bấm đúp `MixueDatMon.sln` để mở bằng Visual Studio. Lần đầu VS sẽ tự khôi phục gói NuGet (`Microsoft.EntityFrameworkCore.SqlServer`, `QRCoder`).
2. Chọn profile **https**, bấm **F5** (hoặc Ctrl+F5). Trình duyệt mở `https://localhost:5001`.
3. Nếu trình duyệt cảnh báo chứng chỉ, chọn tin cậy chứng chỉ phát triển của .NET khi VS hỏi.

## Cấu hình tài khoản ngân hàng nhận tiền (VietQR)
Trong `appsettings.json`, thêm (hoặc sửa) mục `NganHang`, đặt cùng cấp với mục `CuaHang`:

```json
"NganHang": {
  "MaBin": "970436",
  "TenNganHang": "Vietcombank",
  "SoTaiKhoan": "1048748339",
  "TenChuTaiKhoan": "Từ Xuân Đạt"
},
```

`MaBin` là mã ngân hàng 6 số theo NAPAS (Vietcombank là 970436). Nếu để trống, hệ thống không hiện mã QR và thu ngân vẫn xác nhận thủ công được. Sau khi sửa cần chạy lại ứng dụng.

## Tài khoản mẫu
| Vai trò | Tên đăng nhập | Mật khẩu |
|---|---|---|
| Quản lý | `quanly` | `Admin@123` |
| Thu ngân | `thungan1` | `Thungan@123` |

Đăng nhập tại `/TaiKhoan/DangNhap` (hoặc liên kết "Dành cho nhân viên" ở chân trang). **Hãy đổi mật khẩu ngay** trong mục "Đổi mật khẩu".

## Thử quét QR bằng điện thoại
1. Máy tính và điện thoại dùng chung Wi-Fi. Xem địa chỉ IP máy tính (ví dụ `192.168.1.20`).
2. Trong Visual Studio chọn profile **MangLAN-DienThoaiQuetQR** để ứng dụng lắng nghe ở `0.0.0.0:5000`. Cho phép cổng 5000 qua Windows Firewall khi được hỏi.
3. Trong `appsettings.json` đặt `CuaHang:DiaChiCongKhai` thành `http://192.168.1.20:5000`.
4. Đăng nhập quản lý, vào **Bàn và mã QR**, in hoặc mở mã QR rồi quét bằng điện thoại.

## Thử luồng thời gian thực
Mở hai cửa sổ: cửa sổ 1 đăng nhập thu ngân ở trang **Bảng đơn hàng**, cửa sổ 2 (ẩn danh hoặc điện thoại) quét QR và đặt món. Thẻ đơn xuất hiện ở cửa sổ 1 ngay lập tức; khi thu ngân đổi trạng thái, trang theo dõi của khách đổi theo.

## Lỗi thường gặp
| Hiện tượng | Cách xử lý |
|---|---|
| `Cannot open database "MixueDatMonDb"` | Chưa chạy script SQL hoặc chuỗi kết nối sai tên server |
| Đăng nhập không được | Kiểm tra bảng `NguoiDung` có 2 dòng mẫu; script phải chạy trọn vẹn, không bỏ đoạn INSERT |
| Ảnh QR không quét được từ điện thoại | `DiaChiCongKhai` đang là `localhost`; đổi sang IP máy tính hoặc tên miền |
| Thu ngân không thấy đơn mới ngay | Kiểm tra dòng trạng thái "Đang nhận đơn trực tiếp" phía trên bảng đơn; nếu đỏ là mất kết nối SignalR, trang vẫn tự làm mới sau 30 giây |
| Lỗi khi tải ảnh món lên | Thư mục `wwwroot/uploads` cần quyền ghi |

## Triển khai lên máy chủ thật
- Đặt `ASPNETCORE_ENVIRONMENT=Production`, dùng HTTPS, và đổi `Cookie.SecurePolicy` thành `Always` trong `Program.cs`.
- Không để mật khẩu mẫu; đổi hết trước khi mở cho khách.
- Nếu chạy sau IIS/nginx, bật WebSocket để SignalR hoạt động (nếu không, SignalR tự chuyển sang long-polling).
