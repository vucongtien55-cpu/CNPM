using Microsoft.EntityFrameworkCore;
using MixueDatMon.DuLieu;
using MixueDatMon.MoHinh;

namespace MixueDatMon.DichVu;

public class KetQuaGiaVon
{
    /// <summary>Giá vốn ước tính theo (MaMonAn, MaKichCo) - MaKichCo = null là công thức mặc định (món không phân size).</summary>
    public Dictionary<(int MaMonAn, int? MaKichCo), decimal> GiaVonMon { get; set; } = new();
    /// <summary>Những cặp (món, size) không tính được giá vốn (chưa có công thức hoặc nguyên liệu chưa từng nhập kho).</summary>
    public HashSet<(int MaMonAn, int? MaKichCo)> MonThieuGiaVon { get; set; } = new();
    public Dictionary<int, decimal> GiaVonTopping { get; set; } = new();
    public HashSet<int> ToppingThieuGiaVon { get; set; } = new();

    /// <summary>Lấy giá vốn cho 1 dòng đơn: ưu tiên công thức đúng size, nếu món không phân size riêng thì dùng công thức mặc định.</summary>
    public decimal? GiaVonCuaDong(int maMonAn, int? maKichCo) =>
        GiaVonMon.TryGetValue((maMonAn, maKichCo), out var gv) ? gv
        : GiaVonMon.TryGetValue((maMonAn, null), out var gvMacDinh) ? gvMacDinh : null;

    public bool ThieuGiaVonDong(int maMonAn, int? maKichCo) =>
        MonThieuGiaVon.Contains((maMonAn, maKichCo)) ||
        (!GiaVonMon.ContainsKey((maMonAn, maKichCo)) && MonThieuGiaVon.Contains((maMonAn, null)));
}

/// <summary>
/// Tính giá vốn món/topping theo phương pháp bình quân gia quyền (weighted average cost):
/// giá vốn 1 đơn vị nguyên liệu = tổng tiền đã nhập / tổng số lượng đã nhập (từ trước tới nay).
/// Dùng cho báo cáo lãi gộp - không ảnh hưởng tới giá bán hiển thị cho khách.
/// </summary>
public class DichVuGiaVon
{
    private readonly DuLieuContext _csdl;
    public DichVuGiaVon(DuLieuContext csdl) { _csdl = csdl; }

    public async Task<KetQuaGiaVon> TinhAsync()
    {
        // Giá nhập bình quân gia quyền theo từng nguyên liệu
        var donGiaNguyenLieu = await _csdl.ChiTietPhieuNhap
            .GroupBy(c => c.MaNguyenLieu)
            .Select(g => new
            {
                MaNguyenLieu = g.Key,
                TongTien = g.Sum(x => x.SoLuong * x.DonGiaNhap),
                TongSoLuong = g.Sum(x => x.SoLuong)
            })
            .ToDictionaryAsync(x => x.MaNguyenLieu, x => x.TongSoLuong > 0 ? x.TongTien / x.TongSoLuong : 0m);

        var ketQua = new KetQuaGiaVon();

        var congThucMon = await _csdl.CongThucMon.ToListAsync();
        var tatCaMon = await _csdl.MonAn.Select(m => m.MaMonAn).ToListAsync();

        // Mỗi món có thể có nhiều "bộ công thức": 1 bộ mặc định (MaKichCo = null) và/hoặc 1 bộ riêng cho từng size.
        foreach (var maMon in tatCaMon)
        {
            var cacBoCongThuc = congThucMon.Where(c => c.MaMonAn == maMon).Select(c => c.MaKichCo).Distinct().ToList();
            if (cacBoCongThuc.Count == 0) { ketQua.MonThieuGiaVon.Add((maMon, null)); continue; }

            foreach (var maKichCo in cacBoCongThuc)
            {
                var thanhPhan = congThucMon.Where(c => c.MaMonAn == maMon && c.MaKichCo == maKichCo).ToList();
                if (thanhPhan.Any(tp => !donGiaNguyenLieu.ContainsKey(tp.MaNguyenLieu) || donGiaNguyenLieu[tp.MaNguyenLieu] == 0))
                { ketQua.MonThieuGiaVon.Add((maMon, maKichCo)); continue; }

                ketQua.GiaVonMon[(maMon, maKichCo)] = thanhPhan.Sum(tp => tp.SoLuongDung * donGiaNguyenLieu[tp.MaNguyenLieu]);
            }
        }

        var congThucTopping = await _csdl.CongThucTopping.ToListAsync();
        var tatCaTopping = await _csdl.Topping.Select(t => t.MaTopping).ToListAsync();
        foreach (var maTopping in tatCaTopping)
        {
            var thanhPhan = congThucTopping.Where(c => c.MaTopping == maTopping).ToList();
            if (thanhPhan.Count == 0) { ketQua.ToppingThieuGiaVon.Add(maTopping); continue; }
            if (thanhPhan.Any(tp => !donGiaNguyenLieu.ContainsKey(tp.MaNguyenLieu) || donGiaNguyenLieu[tp.MaNguyenLieu] == 0))
            { ketQua.ToppingThieuGiaVon.Add(maTopping); continue; }

            ketQua.GiaVonTopping[maTopping] = thanhPhan.Sum(tp => tp.SoLuongDung * donGiaNguyenLieu[tp.MaNguyenLieu]);
        }

        return ketQua;
    }
}
