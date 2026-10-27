using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MixueDatMon.DuLieu;
using MixueDatMon.MoHinh;
using MixueDatMon.MoHinh.VM;

namespace MixueDatMon.DichVu;

/// <summary>
/// Xử lý mã giảm giá và tích điểm khách hàng thân thiết theo số điện thoại.
/// Nguyên tắc: số tiền giảm luôn được tính lại từ CSDL (server-side), không tin số do client gửi.
/// </summary>
public class DichVuKhuyenMai
{
    private readonly DuLieuContext _csdl;
    public DichVuKhuyenMai(DuLieuContext csdl) { _csdl = csdl; }

    private static readonly Regex MauSoDienThoai = new(@"^0\d{9}$", RegexOptions.Compiled);

    /// <summary>Chuẩn hóa số điện thoại VN (bỏ khoảng trắng/dấu gạch, quy +84 về 0). Trả về null nếu không hợp lệ.</summary>
    public static string? ChuanHoaSoDienThoai(string? soDienThoai)
    {
        if (string.IsNullOrWhiteSpace(soDienThoai)) return null;
        var so = Regex.Replace(soDienThoai.Trim(), @"[\s\-\.]", "");
        if (so.StartsWith("+84")) so = "0" + so[3..];
        else if (so.StartsWith("84") && so.Length == 11) so = "0" + so[2..];
        return MauSoDienThoai.IsMatch(so) ? so : null;
    }

    /// <summary>Tính tổng tiền gốc (chưa giảm) từ danh sách món trong giỏ, tra giá hiện hành trong CSDL.</summary>
    public async Task<decimal> TinhTongGocAsync(List<MonDat> danhSachMon)
    {
        if (danhSachMon == null || danhSachMon.Count == 0) return 0;
        var maMon = danhSachMon.Select(m => m.MaMonAn).Distinct().ToList();
        var giaMon = await _csdl.MonAn.Where(m => maMon.Contains(m.MaMonAn)).ToDictionaryAsync(m => m.MaMonAn, m => m.Gia);
        var giaTheoSize = await _csdl.MonAnKichCo.Where(k => maMon.Contains(k.MaMonAn))
            .ToDictionaryAsync(k => (k.MaMonAn, k.MaKichCo), k => k.Gia);
        var maTopping = danhSachMon.SelectMany(m => m.DanhSachMaTopping ?? new List<int>()).Distinct().ToList();
        var giaTopping = await _csdl.Topping.Where(t => maTopping.Contains(t.MaTopping)).ToDictionaryAsync(t => t.MaTopping, t => t.Gia);

        decimal tong = 0;
        foreach (var mon in danhSachMon)
        {
            if (!giaMon.TryGetValue(mon.MaMonAn, out var gia)) continue;
            if (mon.MaKichCo is int maKichCo && giaTheoSize.TryGetValue((mon.MaMonAn, maKichCo), out var giaSize)) gia = giaSize;
            var tienMon = gia * mon.SoLuong;
            var tienTopping = (mon.DanhSachMaTopping ?? new List<int>())
                .Sum(mt => giaTopping.TryGetValue(mt, out var g) ? g : 0) * mon.SoLuong;
            tong += tienMon + tienTopping;
        }
        return tong;
    }

    /// <summary>Kiểm tra mã giảm giá có áp dụng được cho giỏ hàng hiện tại không, trả về số tiền giảm thực tế.</summary>
    public async Task<KetQuaKiemTraMa> KiemTraMaAsync(string? maCode, List<MonDat> danhSachMon)
    {
        var tongGoc = await TinhTongGocAsync(danhSachMon);
        if (string.IsNullOrWhiteSpace(maCode))
            return new KetQuaKiemTraMa { HopLe = false, ThongBao = "Vui lòng nhập mã giảm giá.", TongTienGoc = tongGoc, TongTienSauGiam = tongGoc };

        var ma = await _csdl.MaGiamGia.FirstOrDefaultAsync(m => m.MaCode == maCode.Trim().ToUpper());
        if (ma == null)
            return new KetQuaKiemTraMa { HopLe = false, ThongBao = "Mã giảm giá không tồn tại.", TongTienGoc = tongGoc, TongTienSauGiam = tongGoc };

        var loi = KiemTraDieuKienMa(ma, tongGoc);
        if (loi != null)
            return new KetQuaKiemTraMa { HopLe = false, ThongBao = loi, TongTienGoc = tongGoc, TongTienSauGiam = tongGoc };

        var soTienGiam = TinhSoTienGiam(ma, tongGoc);
        return new KetQuaKiemTraMa
        {
            HopLe = true,
            ThongBao = $"Áp dụng mã \"{ma.MaCode}\" thành công, giảm {soTienGiam:N0}đ.",
            SoTienGiam = soTienGiam,
            TongTienGoc = tongGoc,
            TongTienSauGiam = Math.Max(0, tongGoc - soTienGiam)
        };
    }

    private static string? KiemTraDieuKienMa(MaGiamGia ma, decimal tongGoc)
    {
        if (!ma.DangHoatDong) return "Mã giảm giá đã ngừng áp dụng.";
        var bayGio = DateTime.Now;
        if (ma.NgayBatDau is DateTime batDau && bayGio < batDau) return "Mã giảm giá chưa tới ngày áp dụng.";
        if (ma.NgayKetThuc is DateTime ketThuc && bayGio > ketThuc) return "Mã giảm giá đã hết hạn.";
        if (ma.SoLuotToiDa is int soLuot && ma.SoLuotDaDung >= soLuot) return "Mã giảm giá đã hết lượt sử dụng.";
        if (tongGoc < ma.DonHangToiThieu) return $"Đơn hàng cần tối thiểu {ma.DonHangToiThieu:N0}đ để dùng mã này.";
        return null;
    }

    private static decimal TinhSoTienGiam(MaGiamGia ma, decimal tongGoc)
    {
        var soTienGiam = ma.LoaiGiam == HangSoLoaiGiam.PhanTram ? Math.Round(tongGoc * ma.GiaTriGiam / 100) : ma.GiaTriGiam;
        if (ma.GiamToiDa is decimal toiDa && soTienGiam > toiDa) soTienGiam = toiDa;
        if (soTienGiam > tongGoc) soTienGiam = tongGoc;
        return soTienGiam;
    }

    /// <summary>Gán mã giảm giá + số điện thoại vào đơn đang dựng (chưa lưu), tính lại số tiền giảm từ CSDL.
    /// Không tăng SoLuotDaDung ở đây - việc đó chỉ xảy ra khi đơn thực sự được lưu (<see cref="GiuLuotAsync"/>).</summary>
    public async Task<string?> ApDungVaoDonAsync(DonHang donHang, string? soDienThoai, string? maCode)
    {
        var soChuan = ChuanHoaSoDienThoai(soDienThoai);
        if (!string.IsNullOrWhiteSpace(soDienThoai) && soChuan == null)
            return "Số điện thoại không hợp lệ.";
        donHang.SoDienThoaiKhach = soChuan;

        if (string.IsNullOrWhiteSpace(maCode))
        {
            donHang.SoTienGiam = 0;
            return null;
        }

        var tongGoc = donHang.DanhSachChiTiet.Sum(c =>
            c.DonGia * c.SoLuong + c.DanhSachTopping.Sum(t => t.DonGia) * c.SoLuong);

        var ma = await _csdl.MaGiamGia.FirstOrDefaultAsync(m => m.MaCode == maCode.Trim().ToUpper());
        if (ma == null) return "Mã giảm giá không tồn tại.";
        var loi = KiemTraDieuKienMa(ma, tongGoc);
        if (loi != null) return loi;

        donHang.MaGiamGiaApDung = ma.MaMaGiamGia;
        donHang.SoTienGiam = TinhSoTienGiam(ma, tongGoc);
        return null;
    }

    /// <summary>Ghi nhận 1 lượt dùng mã giảm giá (gọi ngay sau khi đơn được lưu thành công).</summary>
    public async Task GiuLuotAsync(DonHang donHang)
    {
        if (donHang.MaGiamGiaApDung is not int maMa) return;
        var ma = await _csdl.MaGiamGia.FindAsync(maMa);
        if (ma != null)
        {
            ma.SoLuotDaDung++;
            await _csdl.SaveChangesAsync();
        }
    }

    /// <summary>Trả lại 1 lượt dùng mã giảm giá (gọi khi hủy đơn có hoàn tiền).</summary>
    public async Task TraLuotAsync(DonHang donHang)
    {
        if (donHang.MaGiamGiaApDung is not int maMa) return;
        var ma = await _csdl.MaGiamGia.FindAsync(maMa);
        if (ma != null && ma.SoLuotDaDung > 0)
        {
            ma.SoLuotDaDung--;
            await _csdl.SaveChangesAsync();
        }
    }

    /// <summary>Ghi nhận khách hàng + tích điểm khi đơn được xác nhận đã thu tiền. Gọi 1 lần duy nhất mỗi đơn.</summary>
    public async Task GhiNhanThanhToanAsync(DonHang donHang)
    {
        if (string.IsNullOrWhiteSpace(donHang.SoDienThoaiKhach) || donHang.DiemDaTich > 0) return;

        var caiDat = await _csdl.CaiDatCuaHang.FirstOrDefaultAsync();
        var soDong = caiDat?.SoDongTichMotDiem ?? 10000;
        var diemMoi = soDong > 0 ? (int)(donHang.TongTien / soDong) : 0;

        var khach = await _csdl.KhachHang.FindAsync(donHang.SoDienThoaiKhach);
        if (khach == null)
        {
            khach = new KhachHang { SoDienThoai = donHang.SoDienThoaiKhach, HoTen = donHang.TenKhach };
            _csdl.KhachHang.Add(khach);
        }
        khach.DiemTichLuy += diemMoi;
        khach.TongChiTieu += donHang.TongTien;
        khach.LanCuoiMua = DateTime.Now;
        if (string.IsNullOrWhiteSpace(khach.HoTen) && !string.IsNullOrWhiteSpace(donHang.TenKhach))
            khach.HoTen = donHang.TenKhach;

        donHang.DiemDaTich = diemMoi;
        await _csdl.SaveChangesAsync();
    }

    /// <summary>Hoàn lại điểm + doanh thu đã tích khi hủy đơn có hoàn tiền (gọi sau khi đơn đã từng thanh toán).</summary>
    public async Task HoanDiemAsync(DonHang donHang)
    {
        if (string.IsNullOrWhiteSpace(donHang.SoDienThoaiKhach) || donHang.DiemDaTich <= 0) return;
        var khach = await _csdl.KhachHang.FindAsync(donHang.SoDienThoaiKhach);
        if (khach == null) return;
        khach.DiemTichLuy = Math.Max(0, khach.DiemTichLuy - donHang.DiemDaTich);
        khach.TongChiTieu = Math.Max(0, khach.TongChiTieu - donHang.TongTien);
        donHang.DiemDaTich = 0;
        await _csdl.SaveChangesAsync();
    }

    /// <summary>Lấy thông tin điểm hiện có của 1 số điện thoại (dùng cho trang "Điểm của tôi").</summary>
    public async Task<DiemCuaToiVM> LayDiemAsync(string soDienThoai)
    {
        var caiDat = await _csdl.CaiDatCuaHang.FirstOrDefaultAsync();
        var khach = await _csdl.KhachHang.FindAsync(soDienThoai);
        return new DiemCuaToiVM
        {
            SoDienThoai = soDienThoai,
            DiemTichLuy = khach?.DiemTichLuy ?? 0,
            TongChiTieu = khach?.TongChiTieu ?? 0,
            SoDongTichMotDiem = caiDat?.SoDongTichMotDiem ?? 10000
        };
    }
}
