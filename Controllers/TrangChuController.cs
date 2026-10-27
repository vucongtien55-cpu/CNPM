using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixueDatMon.DuLieu;
using MixueDatMon.MoHinh.VM;

namespace MixueDatMon.Controllers;

public class TrangChuController : Controller
{
    private readonly DuLieuContext _csdl;
    public TrangChuController(DuLieuContext csdl) => _csdl = csdl;

    public async Task<IActionResult> Index()
    {
        var vm = new TrangChuVM
        {
            MonNoiBat = await _csdl.MonAn.AsNoTracking().Include(m => m.NhomMon)
                .Where(m => m.DangBan && m.LaBanChay).OrderBy(m => m.MaMonAn).Take(4).ToListAsync(),
            DanhSachNhom = await _csdl.NhomMon.AsNoTracking().Where(n => n.DangHoatDong).OrderBy(n => n.ThuTuHienThi).ToListAsync()
        };
        var danhSachMa = vm.MonNoiBat.Select(m => m.MaMonAn).ToList();
        vm.ThongKe = await _csdl.ThongKeDanhGia.AsNoTracking()
            .Where(t => danhSachMa.Contains(t.MaMonAn)).ToDictionaryAsync(t => t.MaMonAn);
        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Loi() => View();
}
