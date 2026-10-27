using MixueDatMon.DichVu;
using MixueDatMon.MoHinh;
using MixueDatMon.MoHinh.VM;
using Xunit;

namespace MixueDatMon.Tests;

public class DichVuKhuyenMaiTests
{
    private static async Task<DichVuKhuyenMai> TaoDichVuAsync(MaGiamGia ma, MonAn mon)
    {
        var csdl = HoTroKiemThu.TaoContext();
        csdl.MaGiamGia.Add(ma);
        csdl.MonAn.Add(mon);
        await csdl.SaveChangesAsync();
        return new DichVuKhuyenMai(csdl);
    }

    [Theory]
    [InlineData("0912345678", "0912345678")]
    [InlineData("+84912345678", "0912345678")]
    [InlineData("84912345678", "0912345678")]
    [InlineData("091-234-5678", "0912345678")]
    [InlineData("0912345", null)]          // quá ngắn
    [InlineData("abc", null)]
    [InlineData(null, null)]
    public void ChuanHoaSoDienThoai_TraVeDungSoHoacNull(string? dauVao, string? ketQuaMongDoi)
    {
        Assert.Equal(ketQuaMongDoi, DichVuKhuyenMai.ChuanHoaSoDienThoai(dauVao));
    }

    [Fact]
    public async Task KiemTraMa_GiamPhanTram_CoGioiHanToiDa()
    {
        var mon = new MonAn { MaMonAn = 1, TenMon = "Trà sữa", Gia = 100000 };
        var ma = new MaGiamGia
        {
            MaCode = "GIAM10", LoaiGiam = HangSoLoaiGiam.PhanTram, GiaTriGiam = 10,
            GiamToiDa = 5000, DangHoatDong = true
        };
        var dichVu = await TaoDichVuAsync(ma, mon);
        var danhSachMon = new List<MonDat> { new() { MaMonAn = 1, SoLuong = 1 } };

        // 10% của 100.000 = 10.000, nhưng giới hạn tối đa 5.000 -> phải giảm đúng 5.000
        var ketQua = await dichVu.KiemTraMaAsync("giam10", danhSachMon);

        Assert.True(ketQua.HopLe);
        Assert.Equal(5000m, ketQua.SoTienGiam);
        Assert.Equal(95000m, ketQua.TongTienSauGiam);
    }

    [Fact]
    public async Task KiemTraMa_DonChuaDuToiThieu_BiTuChoi()
    {
        var mon = new MonAn { MaMonAn = 1, TenMon = "Trà sữa", Gia = 20000 };
        var ma = new MaGiamGia
        {
            MaCode = "CAN50K", LoaiGiam = HangSoLoaiGiam.SoTien, GiaTriGiam = 10000,
            DonHangToiThieu = 50000, DangHoatDong = true
        };
        var dichVu = await TaoDichVuAsync(ma, mon);
        var danhSachMon = new List<MonDat> { new() { MaMonAn = 1, SoLuong = 1 } };   // chỉ 20.000, chưa đủ 50.000

        var ketQua = await dichVu.KiemTraMaAsync("CAN50K", danhSachMon);

        Assert.False(ketQua.HopLe);
        Assert.Equal(0m, ketQua.SoTienGiam);
    }

    [Fact]
    public async Task KiemTraMa_MaDaNgungHoatDong_BiTuChoi()
    {
        var mon = new MonAn { MaMonAn = 1, TenMon = "Trà sữa", Gia = 100000 };
        var ma = new MaGiamGia { MaCode = "TAT", LoaiGiam = HangSoLoaiGiam.SoTien, GiaTriGiam = 5000, DangHoatDong = false };
        var dichVu = await TaoDichVuAsync(ma, mon);

        var ketQua = await dichVu.KiemTraMaAsync("TAT", new List<MonDat> { new() { MaMonAn = 1, SoLuong = 1 } });

        Assert.False(ketQua.HopLe);
    }

    [Fact]
    public async Task KiemTraMa_SoTienGiamKhongVuotQuaTongDon()
    {
        // Đơn 10.000đ, mã giảm thẳng 50.000đ -> chỉ được giảm tối đa bằng đúng giá trị đơn (10.000đ), không âm tiền.
        var mon = new MonAn { MaMonAn = 1, TenMon = "Trà chanh", Gia = 10000 };
        var ma = new MaGiamGia { MaCode = "GIAM50K", LoaiGiam = HangSoLoaiGiam.SoTien, GiaTriGiam = 50000, DangHoatDong = true };
        var dichVu = await TaoDichVuAsync(ma, mon);

        var ketQua = await dichVu.KiemTraMaAsync("GIAM50K", new List<MonDat> { new() { MaMonAn = 1, SoLuong = 1 } });

        Assert.True(ketQua.HopLe);
        Assert.Equal(10000m, ketQua.SoTienGiam);
        Assert.Equal(0m, ketQua.TongTienSauGiam);
    }
}
