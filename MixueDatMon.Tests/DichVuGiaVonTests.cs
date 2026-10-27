using MixueDatMon.DichVu;
using MixueDatMon.MoHinh;
using Xunit;

namespace MixueDatMon.Tests;

public class DichVuGiaVonTests
{
    [Fact]
    public async Task TinhAsync_GiaVonBinhQuanGiaQuyen_TinhDungTheoNhieuLanNhap()
    {
        var csdl = HoTroKiemThu.TaoContext();
        var nl = new NguyenLieu { MaNguyenLieu = 1, TenNguyenLieu = "Trân châu", DonViTinh = "g" };
        var mon = new MonAn { MaMonAn = 1, TenMon = "Trà sữa trân châu", Gia = 25000 };
        csdl.NguyenLieu.Add(nl);
        csdl.MonAn.Add(mon);
        // 2 lần nhập với giá khác nhau -> giá vốn phải là bình quân GIA QUYỀN (theo số lượng), không phải trung bình cộng 2 giá.
        csdl.ChiTietPhieuNhap.Add(new ChiTietPhieuNhap { MaNguyenLieu = 1, SoLuong = 1000, DonGiaNhap = 50 });   // 50.000đ
        csdl.ChiTietPhieuNhap.Add(new ChiTietPhieuNhap { MaNguyenLieu = 1, SoLuong = 500, DonGiaNhap = 56 });    // 28.000đ
        // Bình quân gia quyền = (50.000 + 28.000) / (1000 + 500) = 52đ/g
        csdl.CongThucMon.Add(new CongThucMon { MaMonAn = 1, MaKichCo = null, MaNguyenLieu = 1, SoLuongDung = 50 });
        await csdl.SaveChangesAsync();

        var ketQua = await new DichVuGiaVon(csdl).TinhAsync();

        var giaVon = ketQua.GiaVonCuaDong(maMonAn: 1, maKichCo: null);
        Assert.NotNull(giaVon);
        Assert.Equal(52m * 50, giaVon!.Value);   // 52đ/g x 50g = 2.600đ
        Assert.False(ketQua.ThieuGiaVonDong(1, null));
    }

    [Fact]
    public async Task TinhAsync_MonChuaKhaiBaoCongThuc_BiDanhDauThieuGiaVon()
    {
        var csdl = HoTroKiemThu.TaoContext();
        csdl.MonAn.Add(new MonAn { MaMonAn = 2, TenMon = "Kem ốc quế", Gia = 10000 });
        await csdl.SaveChangesAsync();

        var ketQua = await new DichVuGiaVon(csdl).TinhAsync();

        Assert.True(ketQua.ThieuGiaVonDong(2, null));
        Assert.Null(ketQua.GiaVonCuaDong(2, null));
    }

    [Fact]
    public async Task TinhAsync_MonCoSizeRieng_SizeChuaNhapKhoThiDungCongThucMacDinhNeuCo()
    {
        var csdl = HoTroKiemThu.TaoContext();
        var nl = new NguyenLieu { MaNguyenLieu = 1, TenNguyenLieu = "Trà", DonViTinh = "g" };
        var size = new KichCo { MaKichCo = 1, TenKichCo = "L", ThuTu = 2 };
        csdl.NguyenLieu.Add(nl);
        csdl.KichCo.Add(size);
        csdl.MonAn.Add(new MonAn { MaMonAn = 3, TenMon = "Trà đào", Gia = 22000 });
        csdl.ChiTietPhieuNhap.Add(new ChiTietPhieuNhap { MaNguyenLieu = 1, SoLuong = 100, DonGiaNhap = 10 });
        // Chỉ khai báo công thức riêng cho size L, KHÔNG có công thức mặc định (MaKichCo = null)
        csdl.CongThucMon.Add(new CongThucMon { MaMonAn = 3, MaKichCo = 1, MaNguyenLieu = 1, SoLuongDung = 20 });
        await csdl.SaveChangesAsync();

        var ketQua = await new DichVuGiaVon(csdl).TinhAsync();

        // Giá vốn size L phải tính được (20g x 10đ = 200đ)
        Assert.Equal(200m, ketQua.GiaVonCuaDong(3, 1));
        // Size M (chưa khai báo công thức) không có công thức mặc định để fallback -> không tính được
        Assert.Null(ketQua.GiaVonCuaDong(3, 2));
    }
}
