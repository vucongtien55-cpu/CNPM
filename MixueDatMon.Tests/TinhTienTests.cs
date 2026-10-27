using MixueDatMon.MoHinh;
using Xunit;

namespace MixueDatMon.Tests;

/// <summary>Các thuộc tính tính tiền ([NotMapped]) trên thực thể - logic lõi, sai 1 dòng là sai tiền thật của khách.</summary>
public class TinhTienTests
{
    [Fact]
    public void ThanhTien_CongCaTienTopping()
    {
        var chiTiet = new ChiTietDonHang
        {
            DonGia = 25000,
            SoLuong = 2,
            DanhSachTopping = new List<ChiTietTopping>
            {
                new() { MaTopping = 1, DonGia = 3000 },
                new() { MaTopping = 2, DonGia = 3000 }
            }
        };
        // (25.000 + 3.000 + 3.000) x 2 = 62.000
        Assert.Equal(62000m, chiTiet.ThanhTien);
    }

    [Fact]
    public void ThanhTien_KhongTopping_ChiNhanDonGiaNhanSoLuong()
    {
        var chiTiet = new ChiTietDonHang { DonGia = 15000, SoLuong = 3 };
        Assert.Equal(45000m, chiTiet.ThanhTien);
    }

    [Fact]
    public void TongTienGoc_LaTongThanhTienCacDong()
    {
        var don = new DonHang
        {
            DanhSachChiTiet = new List<ChiTietDonHang>
            {
                new() { DonGia = 25000, SoLuong = 1 },
                new() { DonGia = 22000, SoLuong = 2 }
            }
        };
        // 25.000 + 22.000x2 = 69.000
        Assert.Equal(69000m, don.TongTienGoc);
    }

    [Fact]
    public void TongTien_TruDungSoTienGiam()
    {
        var don = new DonHang
        {
            SoTienGiam = 10000,
            DanhSachChiTiet = new List<ChiTietDonHang> { new() { DonGia = 50000, SoLuong = 1 } }
        };
        Assert.Equal(40000m, don.TongTien);
    }

    [Fact]
    public void TongTien_KhongDuocAm_DuSoTienGiamLonHonTongGoc()
    {
        // Phòng trường hợp dữ liệu lỗi (mã giảm giá tính sai): khách không bao giờ bị trả âm tiền.
        var don = new DonHang
        {
            SoTienGiam = 999999,
            DanhSachChiTiet = new List<ChiTietDonHang> { new() { DonGia = 20000, SoLuong = 1 } }
        };
        Assert.Equal(0m, don.TongTien);
    }
}
