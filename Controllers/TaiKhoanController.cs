using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixueDatMon.DichVu;
using MixueDatMon.DuLieu;
using MixueDatMon.MoHinh;
using MixueDatMon.MoHinh.VM;
using MixueDatMon.TroGiup;

namespace MixueDatMon.Controllers;

public class TaiKhoanController : Controller
{
    private readonly DuLieuContext _csdl;
    private readonly DichVuNhatKy _nhatKy;
    public TaiKhoanController(DuLieuContext csdl, DichVuNhatKy nhatKy) { _csdl = csdl; _nhatKy = nhatKy; }

    [HttpGet]
    public IActionResult DangNhap(string? duongDanQuayLai = null)
    {
        if (User.Identity?.IsAuthenticated == true) return ChuyenTheoVaiTro(User.FindFirstValue(ClaimTypes.Role));
        return View(new DangNhapVM { DuongDanQuayLai = duongDanQuayLai });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DangNhap(DangNhapVM vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var tenDangNhap = vm.TenDangNhap.Trim();
        var nguoiDung = await _csdl.NguoiDung.Include(n => n.VaiTro).FirstOrDefaultAsync(n => n.TenDangNhap == tenDangNhap);

        // Luôn băm 1 lần để thời gian phản hồi không lộ việc tài khoản có tồn tại hay không
        var hopLe = nguoiDung != null
            ? MaHoaMatKhau.KiemTra(vm.MatKhau, nguoiDung.MuoiMatKhau, nguoiDung.MatKhauHash) && nguoiDung.DangHoatDong
            : MaHoaMatKhau.Bam(vm.MatKhau, "00000000000000000000000000000000") == "";
        if (!hopLe || nguoiDung == null)
        {
            await Task.Delay(500);   // làm chậm việc dò mật khẩu
            ModelState.AddModelError("", "Sai tên đăng nhập hoặc mật khẩu.");
            return View(vm);
        }

        var danhSachClaim = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, nguoiDung.MaNguoiDung.ToString()),
            new(ClaimTypes.Name, nguoiDung.TenDangNhap),
            new("HoTen", nguoiDung.HoTen),
            new(ClaimTypes.Role, nguoiDung.VaiTro.TenVaiTro)
        };
        var chuThe = new ClaimsPrincipal(new ClaimsIdentity(danhSachClaim, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, chuThe,
            new AuthenticationProperties { IsPersistent = vm.GhiNho });

        nguoiDung.LanDangNhapCuoi = DateTime.Now;
        await _csdl.SaveChangesAsync();

        if (!string.IsNullOrEmpty(vm.DuongDanQuayLai) && Url.IsLocalUrl(vm.DuongDanQuayLai)) return LocalRedirect(vm.DuongDanQuayLai);
        return ChuyenTheoVaiTro(nguoiDung.VaiTro.TenVaiTro);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DangXuat()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "TrangChu");
    }

    public IActionResult TuChoiTruyCap() => View();

    [HttpGet]
    public IActionResult QuenMatKhau()
    {
        if (User.Identity?.IsAuthenticated == true) return ChuyenTheoVaiTro(User.FindFirstValue(ClaimTypes.Role));
        return View(new QuenMatKhauVM());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> QuenMatKhau(QuenMatKhauVM vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var tenDangNhap = vm.TenDangNhap.Trim();
        var soNhap = DichVuKhuyenMai.ChuanHoaSoDienThoai(vm.SoDienThoai);
        var nguoiDung = await _csdl.NguoiDung.FirstOrDefaultAsync(n => n.TenDangNhap == tenDangNhap);

        var khopSo = nguoiDung != null && soNhap != null
            && DichVuKhuyenMai.ChuanHoaSoDienThoai(nguoiDung.SoDienThoai) == soNhap;

        if (nguoiDung == null || !nguoiDung.DangHoatDong || !khopSo)
        {
            await Task.Delay(500);   // làm chậm việc dò số điện thoại, không lộ tài khoản có tồn tại hay không
            ModelState.AddModelError("", "Tên đăng nhập hoặc số điện thoại không khớp với tài khoản nào đang hoạt động. Vui lòng liên hệ quản lý nếu cần hỗ trợ.");
            return View(vm);
        }

        nguoiDung.MuoiMatKhau = MaHoaMatKhau.TaoMuoi();
        nguoiDung.MatKhauHash = MaHoaMatKhau.Bam(vm.MatKhauMoi, nguoiDung.MuoiMatKhau);
        _nhatKy.Ghi(HangSoNhatKy.DatLaiMatKhau, $"Tài khoản \"{nguoiDung.TenDangNhap}\" tự đặt lại mật khẩu qua chức năng Quên mật khẩu (xác thực bằng SĐT).", nguoiDung.MaNguoiDung);
        await _csdl.SaveChangesAsync();

        TempData["ThanhCong"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(DangNhap));
    }

    [Authorize, HttpGet]
    public IActionResult DoiMatKhau() => View(new DoiMatKhauVM());

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DoiMatKhau(DoiMatKhauVM vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var maNguoiDung = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var nguoiDung = await _csdl.NguoiDung.FindAsync(maNguoiDung);
        if (nguoiDung == null || !MaHoaMatKhau.KiemTra(vm.MatKhauHienTai, nguoiDung.MuoiMatKhau, nguoiDung.MatKhauHash))
        {
            ModelState.AddModelError(nameof(vm.MatKhauHienTai), "Mật khẩu hiện tại không đúng.");
            return View(vm);
        }
        nguoiDung.MuoiMatKhau = MaHoaMatKhau.TaoMuoi();
        nguoiDung.MatKhauHash = MaHoaMatKhau.Bam(vm.MatKhauMoi, nguoiDung.MuoiMatKhau);
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = "Đã đổi mật khẩu.";
        return ChuyenTheoVaiTro(User.FindFirstValue(ClaimTypes.Role));
    }

    private IActionResult ChuyenTheoVaiTro(string? tenVaiTro) =>
        tenVaiTro == HangSoVaiTro.QuanLy ? RedirectToAction("TongQuan", "QuanLy") : RedirectToAction("Index", "ThuNgan");
}
