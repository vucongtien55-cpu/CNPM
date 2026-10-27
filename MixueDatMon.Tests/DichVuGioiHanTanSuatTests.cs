using MixueDatMon.DichVu;
using Xunit;

namespace MixueDatMon.Tests;

public class DichVuGioiHanTanSuatTests
{
    [Fact]
    public void ChoPhep_DuoiNguongThiLuonCho()
    {
        var gioiHan = new DichVuGioiHanTanSuat();
        for (var i = 0; i < 5; i++)
            Assert.True(gioiHan.ChoPhep("khach-A", soLanToiDa: 5, TimeSpan.FromMinutes(10), out _));
    }

    [Fact]
    public void ChoPhep_QuaNguongThiTuChoi()
    {
        var gioiHan = new DichVuGioiHanTanSuat();
        for (var i = 0; i < 5; i++)
            gioiHan.ChoPhep("khach-B", soLanToiDa: 5, TimeSpan.FromMinutes(10), out _);

        var choPhep = gioiHan.ChoPhep("khach-B", soLanToiDa: 5, TimeSpan.FromMinutes(10), out var thoiGianCho);
        Assert.False(choPhep);
        Assert.True(thoiGianCho > TimeSpan.Zero);
    }

    [Fact]
    public void ChoPhep_CacKhoaKhacNhauKhongAnhHuongNhau()
    {
        var gioiHan = new DichVuGioiHanTanSuat();
        for (var i = 0; i < 5; i++)
            gioiHan.ChoPhep("ban-1", soLanToiDa: 5, TimeSpan.FromMinutes(10), out _);

        // "ban-1" đã hết lượt, nhưng "ban-2" là khóa riêng nên vẫn phải được phép
        Assert.True(gioiHan.ChoPhep("ban-2", soLanToiDa: 5, TimeSpan.FromMinutes(10), out _));
    }

    [Fact]
    public void DinhDangThoiGianCho_Duoi1PhutHienThiGiay()
    {
        Assert.Equal("30 giây", DichVuGioiHanTanSuat.DinhDangThoiGianCho(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void DinhDangThoiGianCho_TuMotPhutTroLenHienThiPhut()
    {
        Assert.Equal("2 phút", DichVuGioiHanTanSuat.DinhDangThoiGianCho(TimeSpan.FromSeconds(90)));
    }
}
