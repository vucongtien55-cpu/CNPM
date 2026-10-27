using Microsoft.AspNetCore.SignalR;
using MixueDatMon.MoHinh;

namespace MixueDatMon.HubThoiGianThuc;

/// <summary>
/// Nhóm "nhanvien": thu ngân / quản lý nhận đơn mới và thay đổi.
/// Nhóm "don-{ma}": khách đang theo dõi đúng đơn của mình.
/// Sự kiện gửi xuống trình duyệt: CoDonMoi, DonThayDoi, TrangThaiThayDoi.
/// </summary>
public class DonHangHub : Hub
{
    public async Task ThamGiaNhanVien()
    {
        var nguoiDung = Context.User;
        if (nguoiDung?.Identity?.IsAuthenticated == true &&
            (nguoiDung.IsInRole(HangSoVaiTro.ThuNgan) || nguoiDung.IsInRole(HangSoVaiTro.QuanLy)))
            await Groups.AddToGroupAsync(Context.ConnectionId, "nhanvien");
    }

    public Task ThamGiaDon(string maTheoDoi) =>
        Groups.AddToGroupAsync(Context.ConnectionId, "don-" + maTheoDoi);
}
