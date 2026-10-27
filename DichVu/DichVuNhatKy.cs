using MixueDatMon.DuLieu;
using MixueDatMon.MoHinh;

namespace MixueDatMon.DichVu;

/// <summary>Ghi nhật ký thao tác quan trọng (ai làm gì, lúc nào). Chỉ thêm vào DbContext - nơi gọi tự SaveChanges.</summary>
public class DichVuNhatKy
{
    private readonly DuLieuContext _csdl;
    public DichVuNhatKy(DuLieuContext csdl) => _csdl = csdl;

    public void Ghi(string hanhDong, string moTa, int? maNguoiThucHien, int? maDonHang = null)
    {
        if (moTa.Length > 500) moTa = moTa[..500];
        _csdl.NhatKyThaoTac.Add(new NhatKyThaoTac
        {
            HanhDong = hanhDong, MoTa = moTa, MaNguoiThucHien = maNguoiThucHien, MaDonHang = maDonHang, NgayTao = DateTime.Now
        });
    }
}
