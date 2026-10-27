using Microsoft.EntityFrameworkCore;
using MixueDatMon.DuLieu;
using MixueDatMon.MoHinh;

namespace MixueDatMon.DichVu;

/// <summary>Đọc cài đặt quán (giờ mở cửa, tạm ngưng nhận đơn...) và kiểm tra quán có đang nhận đơn của khách không.</summary>
public class DichVuCaiDat
{
    private readonly DuLieuContext _csdl;
    public DichVuCaiDat(DuLieuContext csdl) => _csdl = csdl;

    public async Task<CaiDatCuaHang> LayAsync() =>
        await _csdl.CaiDatCuaHang.AsNoTracking().FirstOrDefaultAsync() ?? new CaiDatCuaHang();

    /// <summary>Hỗ trợ cả trường hợp quán mở qua nửa đêm (giờ đóng nhỏ hơn giờ mở). Giờ mở = giờ đóng nghĩa là mở cả ngày.</summary>
    public static bool DangMoCua(CaiDatCuaHang caiDat, DateTime luc)
    {
        var gio = luc.TimeOfDay;
        if (caiDat.GioMo == caiDat.GioDong) return true;
        return caiDat.GioMo < caiDat.GioDong
            ? gio >= caiDat.GioMo && gio < caiDat.GioDong
            : gio >= caiDat.GioMo || gio < caiDat.GioDong;
    }

    public async Task<(bool DuocDat, string? ThongBao)> KiemTraNhanDonAsync()
    {
        var caiDat = await LayAsync();
        if (caiDat.TamNgungNhanDon)
            return (false, "Quán đang tạm ngưng nhận đơn trên web. Bạn vui lòng đến quầy để gọi món nhé.");
        if (!DangMoCua(caiDat, DateTime.Now))
            return (false, $"Quán hiện đã đóng cửa. Giờ mở cửa: {caiDat.GioMo:hh\\:mm} - {caiDat.GioDong:hh\\:mm}.");
        return (true, null);
    }
}
