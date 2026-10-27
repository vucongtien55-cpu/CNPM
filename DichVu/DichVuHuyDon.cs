using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MixueDatMon.DuLieu;
using MixueDatMon.HubThoiGianThuc;
using MixueDatMon.MoHinh;
using MixueDatMon.TroGiup;

namespace MixueDatMon.DichVu;

/// <summary>
/// Hủy đơn có lý do; nếu đơn đã thu tiền thì ghi nhận hoàn tiền; tùy chọn hoàn nguyên liệu vào kho; ghi nhật ký thao tác.
/// Dùng chung cho thu ngân hủy tay và cho tác vụ tự hủy đơn mang đi quá hạn thanh toán.
/// </summary>
public class DichVuHuyDon
{
    public static readonly string[] DanhSachLyDo =
    {
        "Khách đổi ý", "Khách không đến nhận / bỏ về", "Nhầm đơn / nhập sai", "Hết nguyên liệu", "Món pha hỏng",
        "Khách không hài lòng", "Quá hạn thanh toán (tự động)", "Khác"
    };

    private readonly DuLieuContext _csdl;
    private readonly DichVuKho _kho;
    private readonly DichVuNhatKy _nhatKy;
    private readonly DichVuKhuyenMai _khuyenMai;
    private readonly IHubContext<DonHangHub> _hub;
    public DichVuHuyDon(DuLieuContext csdl, DichVuKho kho, DichVuNhatKy nhatKy, DichVuKhuyenMai khuyenMai, IHubContext<DonHangHub> hub)
    { _csdl = csdl; _kho = kho; _nhatKy = nhatKy; _khuyenMai = khuyenMai; _hub = hub; }

    public async Task<(bool ThanhCong, string ThongBao)> HuyAsync(
        int maDonHang, string? lyDo, string? ghiChu, bool hoanKho, bool daHoanTien, int? maNguoiThucHien, bool laQuanLy, bool tuDong = false)
    {
        var don = await _csdl.DonHang.Include(d => d.BanAn)
            .Include(d => d.DanhSachChiTiet).ThenInclude(c => c.DanhSachTopping)
            .FirstOrDefaultAsync(d => d.MaDonHang == maDonHang);
        if (don == null) return (false, "Không tìm thấy đơn.");
        if (don.MaTrangThai == HangSoTrangThai.DaHuy) return (false, "Đơn này đã được hủy trước đó.");
        if (don.MaTrangThai == HangSoTrangThai.HoanThanh && !laQuanLy)
            return (false, "Đơn đã hoàn thành. Chỉ quản lý mới được hủy và hoàn tiền đơn này.");

        lyDo = lyDo?.Trim();
        ghiChu = ghiChu?.Trim();
        if (string.IsNullOrEmpty(lyDo)) return (false, "Vui lòng chọn lý do hủy đơn.");
        if (!DanhSachLyDo.Contains(lyDo)) return (false, "Lý do hủy không hợp lệ.");
        if (lyDo == "Khác" && string.IsNullOrEmpty(ghiChu)) return (false, "Lý do \"Khác\" cần ghi rõ nội dung.");

        var daThu = don.NgayThanhToan != null;
        if (daThu && !daHoanTien)
            return (false, "Đơn đã thu tiền. Hãy hoàn tiền cho khách rồi tích xác nhận đã hoàn tiền.");

        var tongTien = await _csdl.TongTienDon.Where(t => t.MaDonHang == maDonHang).Select(t => t.TongTien).FirstOrDefaultAsync();
        var lyDoLuu = lyDo == "Khác" ? "Khác: " + ghiChu : lyDo;
        if (lyDoLuu.Length > 300) lyDoLuu = lyDoLuu[..300];
        var bayGio = DateTime.Now;

        await using var giaoDich = await _csdl.Database.BeginTransactionAsync();
        don.MaTrangThai = HangSoTrangThai.DaHuy;
        don.LyDoHuy = lyDoLuu;
        don.NgayHuy = bayGio;
        don.MaNguoiHuy = maNguoiThucHien;
        don.NgayCapNhat = bayGio;
        if (daThu) don.NgayHoanTien = bayGio;

        var daHoanKho = hoanKho && await _kho.HoanKhoTheoDonAsync(maDonHang, maNguoiThucHien);

        var moTa = $"Hủy đơn {don.MaTheoDoi} ({don.NoiPhucVu}, {tongTien.Tien()}). Lý do: {lyDoLuu}."
                 + (daThu ? $" Đã hoàn {tongTien.Tien()} cho khách bằng {HangSoPhuongThuc.Ten(don.MaPhuongThuc ?? 0).ToLower()}." : "")
                 + (daHoanKho ? " Đã hoàn nguyên liệu vào kho." : "")
                 + (!string.IsNullOrEmpty(ghiChu) && lyDo != "Khác" ? $" Ghi chú: {ghiChu}." : "");
        _nhatKy.Ghi(tuDong ? HangSoNhatKy.TuHuyDon : daThu ? HangSoNhatKy.HoanTien : HangSoNhatKy.HuyDon, moTa, maNguoiThucHien, maDonHang);

        await _csdl.SaveChangesAsync();
        await _khuyenMai.TraLuotAsync(don);
        if (daThu) await _khuyenMai.HoanDiemAsync(don);
        await giaoDich.CommitAsync();

        await _hub.Clients.Group("nhanvien").SendAsync("DonThayDoi");
        await _hub.Clients.Group("don-" + don.MaTheoDoi).SendAsync("TrangThaiThayDoi", new
        {
            maTrangThai = HangSoTrangThai.DaHuy, tenTrangThai = HangSoTrangThai.Ten(HangSoTrangThai.DaHuy),
            daThanhToan = daThu, lyDoHuy = lyDoLuu
        });
        return (true, daThu ? "Đã hủy đơn và ghi nhận hoàn tiền." : "Đã hủy đơn.");
    }
}
