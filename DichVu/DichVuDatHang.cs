using Microsoft.EntityFrameworkCore;
using MixueDatMon.DuLieu;
using MixueDatMon.MoHinh;
using MixueDatMon.MoHinh.VM;

namespace MixueDatMon.DichVu;

/// <summary>
/// Dựng đơn hàng từ giỏ hàng, dùng chung cho đơn khách tự đặt (điện thoại) và đơn thu ngân tạo tại quầy.
/// Giá, tùy chọn, topping đều được tra lại từ CSDL - không tin dữ liệu gửi lên.
/// </summary>
public class DichVuDatHang
{
    private readonly DuLieuContext _csdl;
    private readonly DichVuCaiDat _caiDat;
    private readonly DichVuKho _kho;
    private readonly DichVuKhuyenMai _khuyenMai;
    public DichVuDatHang(DuLieuContext csdl, DichVuCaiDat caiDat, DichVuKho kho, DichVuKhuyenMai khuyenMai)
    { _csdl = csdl; _caiDat = caiDat; _kho = kho; _khuyenMai = khuyenMai; }

    /// <summary>Trả về đơn chưa lưu (trạng thái Chờ xác nhận) hoặc thông báo lỗi. Người gọi tự Add + SaveChanges.</summary>
    /// <param name="kiemTraKho">true với đơn khách tự đặt: từ chối món/topping đang hết nguyên liệu (nếu quản lý bật tự ẩn món hết).</param>
    public async Task<(DonHang? DonMoi, string? Loi)> DungDonAsync(YeuCauDatHang yeuCau, int? maBan, bool kiemTraKho = false)
    {
        if (yeuCau?.DanhSachMon == null || yeuCau.DanhSachMon.Count == 0 || yeuCau.DanhSachMon.Count > 40)
            return (null, "Giỏ hàng đang trống.");

        var danhSachMaMon = yeuCau.DanhSachMon.Select(m => m.MaMonAn).Distinct().ToList();
        var monHopLe = await _csdl.MonAn.Include(m => m.NhomMon)
            .Where(m => danhSachMaMon.Contains(m.MaMonAn) && m.DangBan && m.NhomMon.DangHoatDong)
            .ToDictionaryAsync(m => m.MaMonAn);

        var danhSachMaTopping = yeuCau.DanhSachMon.SelectMany(m => m.DanhSachMaTopping ?? new List<int>()).Distinct().ToList();
        var toppingHopLe = await _csdl.Topping.Where(t => danhSachMaTopping.Contains(t.MaTopping) && t.DangBan)
            .ToDictionaryAsync(t => t.MaTopping);
        var maMucDaHopLe = await _csdl.MucDa.OrderBy(x => x.MaMucDa).Select(x => x.MaMucDa).ToListAsync();
        var maMucDuongHopLe = await _csdl.MucDuong.OrderBy(x => x.MaMucDuong).Select(x => x.MaMucDuong).ToListAsync();

        // Giá theo size (nếu món có khai báo size) - khóa theo (MaMonAn, MaKichCo)
        var giaTheoSize = await _csdl.MonAnKichCo.AsNoTracking()
            .Where(k => danhSachMaMon.Contains(k.MaMonAn))
            .ToDictionaryAsync(k => (k.MaMonAn, k.MaKichCo), k => k.Gia);
        var sizeTheoMon = giaTheoSize.Keys.GroupBy(k => k.MaMonAn).ToDictionary(g => g.Key, g => g.Select(k => k.MaKichCo).ToHashSet());

        HashSet<int> monHet = new(), toppingHet = new();
        if (kiemTraKho && (await _caiDat.LayAsync()).TuAnMonHetNguyenLieu)
            (monHet, toppingHet) = await _kho.LayHetNguyenLieuAsync();

        var donHang = new DonHang
        {
            MaTheoDoi = await TaoMaTheoDoiAsync(),
            MaBan = maBan,
            MaTrangThai = HangSoTrangThai.ChoXacNhan,
            TenKhach = LamSach(yeuCau.TenKhach, 100),
            GhiChuKhach = LamSach(yeuCau.GhiChu, 300),
            NgayTao = DateTime.Now
        };

        foreach (var mon in yeuCau.DanhSachMon)
        {
            if (!monHopLe.TryGetValue(mon.MaMonAn, out var monAn))
                return (null, "Có món vừa hết hoặc ngừng phục vụ. Vui lòng tải lại thực đơn.");
            if (mon.SoLuong < 1 || mon.SoLuong > 20)
                return (null, "Số lượng mỗi món từ 1 đến 20.");
            if (monHet.Contains(monAn.MaMonAn))
                return (null, $"Món \"{monAn.TenMon}\" vừa tạm hết nguyên liệu. Bạn vui lòng chọn món khác.");

            // Món có khai báo size thì bắt buộc chọn 1 size hợp lệ của chính món đó (giá lấy từ bảng size, không tin giá gửi lên).
            var cacSizeCuaMon = sizeTheoMon.GetValueOrDefault(monAn.MaMonAn);
            int? maKichCoDaChon = null;
            decimal donGia = monAn.Gia;
            if (cacSizeCuaMon is { Count: > 0 })
            {
                if (mon.MaKichCo is not int maKichCo || !cacSizeCuaMon.Contains(maKichCo))
                    return (null, $"Vui lòng chọn size cho món \"{monAn.TenMon}\".");
                maKichCoDaChon = maKichCo;
                donGia = giaTheoSize[(monAn.MaMonAn, maKichCo)];
            }

            var chiTiet = new ChiTietDonHang { MaMonAn = monAn.MaMonAn, SoLuong = mon.SoLuong, DonGia = donGia, MaKichCo = maKichCoDaChon, GhiChu = LamSach(mon.GhiChu, 200) };
            if (monAn.NhomMon.CoTuyChonDoUong)
            {
                chiTiet.MaMucDa = mon.MaMucDa is int mucDa && maMucDaHopLe.Contains(mucDa) ? mucDa : maMucDaHopLe.FirstOrDefault();
                chiTiet.MaMucDuong = mon.MaMucDuong is int mucDuong && maMucDuongHopLe.Contains(mucDuong) ? mucDuong : maMucDuongHopLe.FirstOrDefault();
                foreach (var maTopping in (mon.DanhSachMaTopping ?? new List<int>()).Distinct().Take(5))
                {
                    if (toppingHet.Contains(maTopping) && toppingHopLe.TryGetValue(maTopping, out var toppingHetHang))
                        return (null, $"Topping \"{toppingHetHang.TenTopping}\" vừa tạm hết. Bạn vui lòng bỏ topping này.");
                    if (toppingHopLe.TryGetValue(maTopping, out var topping))
                        chiTiet.DanhSachTopping.Add(new ChiTietTopping { MaTopping = topping.MaTopping, DonGia = topping.Gia });
                }
            }
            donHang.DanhSachChiTiet.Add(chiTiet);
        }

        var loiKhuyenMai = await _khuyenMai.ApDungVaoDonAsync(donHang, yeuCau.SoDienThoai, yeuCau.MaGiamGia);
        if (loiKhuyenMai != null) return (null, loiKhuyenMai);

        return (donHang, null);
    }

    public async Task<string> TaoMaTheoDoiAsync()
    {
        const string kyTu = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        for (var lanThu = 0; lanThu < 8; lanThu++)
        {
            var duoi = new string(Enumerable.Range(0, 4).Select(_ => kyTu[Random.Shared.Next(kyTu.Length)]).ToArray());
            var ma = $"MX{DateTime.Now:yyMMdd}-{duoi}";
            if (!await _csdl.DonHang.AnyAsync(d => d.MaTheoDoi == ma)) return ma;
        }
        return $"MX{DateTime.Now:yyMMddHHmmss}";
    }

    public static string? LamSach(string? chuoi, int doDaiToiDa)
    {
        if (string.IsNullOrWhiteSpace(chuoi)) return null;
        chuoi = chuoi.Trim();
        return chuoi.Length > doDaiToiDa ? chuoi[..doDaiToiDa] : chuoi;
    }
}
