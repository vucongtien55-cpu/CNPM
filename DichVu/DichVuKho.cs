using Microsoft.EntityFrameworkCore;
using MixueDatMon.DuLieu;
using MixueDatMon.MoHinh;

namespace MixueDatMon.DichVu;

/// <summary>Trừ kho theo công thức món + topping khi thu ngân bắt đầu làm đơn.</summary>
public class DichVuKho
{
    private readonly DuLieuContext _csdl;
    public DichVuKho(DuLieuContext csdl) => _csdl = csdl;

    /// <summary>
    /// Hoàn lại đúng những nguyên liệu đã trừ cho đơn (dựa trên sổ kho, không tính lại công thức).
    /// Trả về false nếu đơn chưa từng trừ kho hoặc đã hoàn rồi (tránh hoàn hai lần). Người gọi tự SaveChanges.
    /// </summary>
    public async Task<bool> HoanKhoTheoDonAsync(int maDonHang, int? maNguoiThucHien)
    {
        var daXuat = await _csdl.BienDongKho
            .Where(b => b.MaDonHang == maDonHang && b.LoaiBienDong == HangSoBienDong.XuatTheoDon).ToListAsync();
        if (daXuat.Count == 0) return false;
        if (await _csdl.BienDongKho.AnyAsync(b => b.MaDonHang == maDonHang && b.LoaiBienDong == HangSoBienDong.HoanKho)) return false;

        var canHoan = daXuat.GroupBy(b => b.MaNguyenLieu).ToDictionary(g => g.Key, g => -g.Sum(x => x.SoLuongThayDoi));
        var danhSachMa = canHoan.Keys.ToList();
        var nguyenLieu = await _csdl.NguyenLieu.Where(n => danhSachMa.Contains(n.MaNguyenLieu)).ToListAsync();
        foreach (var nl in nguyenLieu)
        {
            var soLuong = canHoan[nl.MaNguyenLieu];
            if (soLuong <= 0) continue;
            nl.SoLuongTon += soLuong;
            _csdl.BienDongKho.Add(new BienDongKho
            {
                MaNguyenLieu = nl.MaNguyenLieu, SoLuongThayDoi = soLuong, LoaiBienDong = HangSoBienDong.HoanKho,
                MaDonHang = maDonHang, MaNguoiThucHien = maNguoiThucHien, GhiChu = "Hoàn kho do hủy đơn", NgayTao = DateTime.Now
            });
        }
        return true;
    }

    /// <summary>
    /// Danh sách món và topping mà kho không còn đủ nguyên liệu để làm 1 phần (tồn kho nhỏ hơn định mức).
    /// Món chưa khai báo công thức thì không bao giờ bị coi là hết.
    /// </summary>
    public async Task<(HashSet<int> Mon, HashSet<int> Topping)> LayHetNguyenLieuAsync()
    {
        // Món có công thức theo size: chỉ coi là "hết" khi TẤT CẢ size của món đó đều thiếu nguyên liệu
        // (môt số công thức khác yêu cầu khác nhau). Món không phân size dùng công thức mặc định (MaKichCo NULL) như cũ.
        var congThuc = await (from r in _csdl.CongThucMon
                              join n in _csdl.NguyenLieu on r.MaNguyenLieu equals n.MaNguyenLieu
                              select new { r.MaMonAn, r.MaKichCo, ThieuNguyenLieu = n.SoLuongTon < r.SoLuongDung })
                             .ToListAsync();
        var mon = congThuc.GroupBy(x => new { x.MaMonAn, x.MaKichCo })
            .Where(g => g.Any(x => x.ThieuNguyenLieu))
            .Select(g => g.Key.MaMonAn)
            .Where(maMon => congThuc.Where(x => x.MaMonAn == maMon)
                .GroupBy(x => x.MaKichCo).All(gSize => gSize.Any(x => x.ThieuNguyenLieu)))
            .Distinct().ToList();
        var topping = await (from r in _csdl.CongThucTopping
                             join n in _csdl.NguyenLieu on r.MaNguyenLieu equals n.MaNguyenLieu
                             where n.SoLuongTon < r.SoLuongDung
                             select r.MaTopping).Distinct().ToListAsync();
        return (mon.ToHashSet(), topping.ToHashSet());
    }

    /// <summary>Chỉ thêm thay đổi vào DbContext - nơi gọi chịu trách nhiệm SaveChanges (để dùng chung transaction).</summary>
    public async Task TruKhoTheoDonAsync(int maDonHang, int? maNguoiThucHien)
    {
        var danhSachDong = await _csdl.ChiTietDonHang.AsNoTracking()
            .Where(c => c.MaDonHang == maDonHang)
            .Select(c => new { c.SoLuong, c.MaMonAn, c.MaKichCo, DanhSachMaTopping = c.DanhSachTopping.Select(t => t.MaTopping).ToList() })
            .ToListAsync();
        if (danhSachDong.Count == 0) return;

        var danhSachMaMon = danhSachDong.Select(d => d.MaMonAn).Distinct().ToList();
        var danhSachMaTopping = danhSachDong.SelectMany(d => d.DanhSachMaTopping).Distinct().ToList();
        var congThucMon = await _csdl.CongThucMon.AsNoTracking().Where(r => danhSachMaMon.Contains(r.MaMonAn)).ToListAsync();
        var congThucTopping = await _csdl.CongThucTopping.AsNoTracking().Where(r => danhSachMaTopping.Contains(r.MaTopping)).ToListAsync();

        var canDung = new Dictionary<int, decimal>();   // MaNguyenLieu -> số lượng cần trừ
        void Cong(int maNguyenLieu, decimal soLuong) => canDung[maNguyenLieu] = canDung.GetValueOrDefault(maNguyenLieu) + soLuong;

        foreach (var dong in danhSachDong)
        {
            // Ưu tiên công thức đúng size đã chọn; nếu món không có công thức riêng cho size đó thì dùng công thức mặc định (MaKichCo NULL).
            var congThucDungSize = congThucMon.Where(r => r.MaMonAn == dong.MaMonAn && r.MaKichCo == dong.MaKichCo).ToList();
            var congThucApDung = congThucDungSize.Count > 0 ? congThucDungSize
                : congThucMon.Where(r => r.MaMonAn == dong.MaMonAn && r.MaKichCo == null).ToList();
            foreach (var r in congThucApDung) Cong(r.MaNguyenLieu, r.SoLuongDung * dong.SoLuong);
            foreach (var maTopping in dong.DanhSachMaTopping)
                foreach (var r in congThucTopping.Where(r => r.MaTopping == maTopping)) Cong(r.MaNguyenLieu, r.SoLuongDung * dong.SoLuong);
        }
        if (canDung.Count == 0) return;

        var danhSachMaNguyenLieu = canDung.Keys.ToList();
        var nguyenLieu = await _csdl.NguyenLieu.Where(n => danhSachMaNguyenLieu.Contains(n.MaNguyenLieu)).ToListAsync();
        foreach (var nl in nguyenLieu)
        {
            var soLuong = canDung[nl.MaNguyenLieu];
            nl.SoLuongTon -= soLuong;    // có thể âm -> quản lý biết cần nhập / kiểm kê
            _csdl.BienDongKho.Add(new BienDongKho
            {
                MaNguyenLieu = nl.MaNguyenLieu, SoLuongThayDoi = -soLuong, LoaiBienDong = HangSoBienDong.XuatTheoDon,
                MaDonHang = maDonHang, MaNguoiThucHien = maNguoiThucHien, NgayTao = DateTime.Now
            });
        }
    }
}
