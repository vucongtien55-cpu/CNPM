using Microsoft.EntityFrameworkCore;
using MixueDatMon.DuLieu;
using MixueDatMon.MoHinh;

namespace MixueDatMon.DichVu;

/// <summary>
/// Chạy nền mỗi phút: tự hủy đơn MANG ĐI chưa thanh toán quá thời hạn (cài đặt quán, mặc định 15 phút).
/// Đơn khách ngồi bàn không bị tự hủy vì thanh toán sau tại quầy.
/// </summary>
public class DichVuTuHuyDon : BackgroundService
{
    private readonly IServiceScopeFactory _nhaMayPhamVi;
    private readonly ILogger<DichVuTuHuyDon> _log;
    public DichVuTuHuyDon(IServiceScopeFactory nhaMayPhamVi, ILogger<DichVuTuHuyDon> log) { _nhaMayPhamVi = nhaMayPhamVi; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken tokenDung)
    {
        while (!tokenDung.IsCancellationRequested)
        {
            try { await QuetAsync(); }
            catch (Exception loi) { _log.LogError(loi, "Lỗi khi quét đơn mang đi quá hạn thanh toán"); }

            try { await Task.Delay(TimeSpan.FromSeconds(60), tokenDung); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task QuetAsync()
    {
        using var phamVi = _nhaMayPhamVi.CreateScope();
        var csdl = phamVi.ServiceProvider.GetRequiredService<DuLieuContext>();
        var caiDat = await csdl.CaiDatCuaHang.AsNoTracking().FirstOrDefaultAsync();
        var soPhut = caiDat?.SoPhutTuHuyDonMangDi ?? 15;
        if (soPhut <= 0) return;   // 0 = tắt chức năng

        var han = DateTime.Now.AddMinutes(-soPhut);
        var danhSachMa = await csdl.DonHang.AsNoTracking()
            .Where(d => d.MaBan == null && d.MaTrangThai == HangSoTrangThai.ChoXacNhan && d.NgayThanhToan == null && d.NgayTao <= han)
            .Select(d => d.MaDonHang).ToListAsync();

        var huyDon = phamVi.ServiceProvider.GetRequiredService<DichVuHuyDon>();
        foreach (var ma in danhSachMa)
        {
            var (thanhCong, thongBao) = await huyDon.HuyAsync(ma, "Quá hạn thanh toán (tự động)", null, false, false, null, true, tuDong: true);
            if (thanhCong) _log.LogInformation("Đã tự hủy đơn #{Ma} vì mang đi quá {Phut} phút chưa thanh toán", ma, soPhut);
            else _log.LogWarning("Không tự hủy được đơn #{Ma}: {ThongBao}", ma, thongBao);
        }
    }
}
