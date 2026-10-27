using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MixueDatMon.DichVu;
using MixueDatMon.DuLieu;
using MixueDatMon.MoHinh;
using MixueDatMon.MoHinh.VM;
using MixueDatMon.TroGiup;
using QRCoder;

namespace MixueDatMon.Controllers;

/// <summary>Quản lý: toàn quyền hệ thống + quản lý kho.</summary>
[Authorize(Roles = "QuanLy")]
public class QuanLyController : Controller
{
    private readonly DuLieuContext _csdl;
    private readonly IWebHostEnvironment _moiTruong;
    private readonly IConfiguration _cauHinh;
    private readonly DichVuNhatKy _nhatKy;
    private readonly DichVuGiaVon _giaVon;
    private readonly DichVuXuatExcel _excel;
    public QuanLyController(DuLieuContext csdl, IWebHostEnvironment moiTruong, IConfiguration cauHinh, DichVuNhatKy nhatKy,
        DichVuGiaVon giaVon, DichVuXuatExcel excel)
    { _csdl = csdl; _moiTruong = moiTruong; _cauHinh = cauHinh; _nhatKy = nhatKy; _giaVon = giaVon; _excel = excel; }

    private int MaNguoiDungHienTai => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ================= TỔNG QUAN =================
    public async Task<IActionResult> TongQuan()
    {
        var homNay = DateTime.Today;
        var dauTuan = homNay.AddDays(-6);
        var dauThang = new DateTime(homNay.Year, homNay.Month, 1);
        var tuNgay = dauThang < dauTuan ? dauThang : dauTuan;

        var danhSachDaThu = await (from d in _csdl.DonHang
                                   join t in _csdl.TongTienDon on d.MaDonHang equals t.MaDonHang
                                   where d.NgayThanhToan != null && d.NgayThanhToan >= tuNgay && d.MaTrangThai != HangSoTrangThai.DaHuy
                                   select new { NgayThanhToan = d.NgayThanhToan!.Value, t.TongTien }).ToListAsync();

        var moc30Ngay = homNay.AddDays(-30);
        var monBanChay = await _csdl.ChiTietDonHang
            .Where(c => c.DonHang.NgayThanhToan >= moc30Ngay && c.DonHang.MaTrangThai != HangSoTrangThai.DaHuy)
            .GroupBy(c => c.MonAn.TenMon)
            .Select(g => new { Ten = g.Key, SoLuong = g.Sum(x => x.SoLuong) })
            .OrderByDescending(x => x.SoLuong).Take(5).ToListAsync();

        var soDanhGiaDon = await _csdl.DanhGiaDonHang.CountAsync();
        var diemDichVu = soDanhGiaDon == 0 ? 0 : await _csdl.DanhGiaDonHang.AverageAsync(d => (double)d.SoSao);

        var vm = new TongQuanVM
        {
            SoDanhGiaDichVu = soDanhGiaDon,
            DiemDichVu = Math.Round((decimal)diemDichVu, 1),
            DoanhThuHomNay = danhSachDaThu.Where(r => r.NgayThanhToan >= homNay).Sum(r => r.TongTien),
            DoanhThuTuan = danhSachDaThu.Where(r => r.NgayThanhToan >= dauTuan).Sum(r => r.TongTien),
            DoanhThuThang = danhSachDaThu.Where(r => r.NgayThanhToan >= dauThang).Sum(r => r.TongTien),
            SoDonHomNay = await _csdl.DonHang.CountAsync(d => d.NgayTao >= homNay && d.MaTrangThai != HangSoTrangThai.DaHuy),
            DonChoXacNhan = await _csdl.DonHang.CountAsync(d => d.MaTrangThai == HangSoTrangThai.ChoXacNhan),
            DoanhThu7Ngay = Enumerable.Range(0, 7).Select(i =>
            {
                var ngay = dauTuan.AddDays(i);
                return (ngay.ToString("dd/MM"), danhSachDaThu.Where(r => r.NgayThanhToan.Date == ngay).Sum(r => r.TongTien));
            }).ToList(),
            MonBanChay = monBanChay.Select(x => (x.Ten, x.SoLuong)).ToList(),
            NguyenLieuSapHet = await _csdl.NguyenLieu.AsNoTracking()
                .Where(n => n.DangHoatDong && n.DinhMucMoiCoc != null && n.DinhMucMoiCoc > 0
                    && n.SoLuongTon <= n.DinhMucMoiCoc.Value * n.SoCocCanhBao)
                .OrderBy(n => n.SoLuongTon).ToListAsync(),
            NguyenLieuSapHetKhoTong = await _csdl.NguyenLieu.AsNoTracking()
                .Where(n => n.DangHoatDong && n.SoLuongTonKhoTong <= n.MucToiThieuKhoTong)
                .OrderBy(n => n.SoLuongTonKhoTong).ToListAsync()
        };
        return View(vm);
    }

    // ================= ĐÁNH GIÁ CỦA KHÁCH =================
    public async Task<IActionResult> DanhGia(int? toiDaSao)
    {
        var soDon = await _csdl.DanhGiaDonHang.CountAsync();
        var diemDon = soDon == 0 ? 0 : await _csdl.DanhGiaDonHang.AverageAsync(d => (double)d.SoSao);
        var soMon = await _csdl.DanhGiaMon.CountAsync(d => !d.DaAn);
        var diemMon = soMon == 0 ? 0 : await _csdl.DanhGiaMon.Where(d => !d.DaAn).AverageAsync(d => (double)d.SoSao);

        var thongKeMon = await (from t in _csdl.ThongKeDanhGia
                                join m in _csdl.MonAn on t.MaMonAn equals m.MaMonAn
                                orderby t.DiemTrungBinh, t.SoLuot descending
                                select new DongThongKeMonVM { TenMon = m.TenMon, SoLuot = t.SoLuot, DiemTrungBinh = t.DiemTrungBinh })
                               .ToListAsync();

        var truyVanGopY = _csdl.DanhGiaDonHang.AsQueryable();
        if (toiDaSao != null) truyVanGopY = truyVanGopY.Where(d => d.SoSao <= toiDaSao);
        var danhSachGopY = await truyVanGopY.OrderByDescending(d => d.NgayTao).Take(100)
            .Select(d => new DongGopYVM
            {
                MaDanhGiaDon = d.MaDanhGiaDon, MaTheoDoi = d.DonHang.MaTheoDoi,
                NoiPhucVu = d.DonHang.BanAn != null ? d.DonHang.BanAn.TenBan : "Mang đi",
                SoSao = d.SoSao, GopY = d.GopY, NgayTao = d.NgayTao, DaXem = d.DaXem
            }).ToListAsync();

        var truyVanNhanXet = _csdl.DanhGiaMon.AsQueryable();
        if (toiDaSao != null) truyVanNhanXet = truyVanNhanXet.Where(d => d.SoSao <= toiDaSao);
        var danhSachNhanXet = await truyVanNhanXet.OrderByDescending(d => d.NgayTao).Take(100)
            .Select(d => new DongNhanXetMonVM
            {
                MaDanhGiaMon = d.MaDanhGiaMon, TenMon = d.ChiTietDonHang.MonAn.TenMon,
                MaTheoDoi = d.ChiTietDonHang.DonHang.MaTheoDoi, TenKhach = d.ChiTietDonHang.DonHang.TenKhach,
                SoSao = d.SoSao, NhanXet = d.NhanXet, NgayTao = d.NgayTao, DaAn = d.DaAn
            }).ToListAsync();

        return View(new DanhGiaVM
        {
            SoDanhGiaDon = soDon, DiemDichVu = Math.Round((decimal)diemDon, 1),
            SoDanhGiaMon = soMon, DiemMon = Math.Round((decimal)diemMon, 1),
            SoDanhGiaThap = await _csdl.DanhGiaDonHang.CountAsync(d => d.SoSao <= 2 && !d.DaXem),
            ToiDaSao = toiDaSao, ThongKeMon = thongKeMon, DanhSachGopY = danhSachGopY, DanhSachNhanXet = danhSachNhanXet
        });
    }

    /// <summary>Ẩn / hiện một nhận xét về món (nhận xét bị ẩn không tính vào điểm và không hiện cho khách).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AnHienDanhGiaMon(int ma, int? toiDaSao)
    {
        var danhGia = await _csdl.DanhGiaMon.FindAsync(ma);
        if (danhGia == null) return NotFound();
        danhGia.DaAn = !danhGia.DaAn;
        await _csdl.SaveChangesAsync();
        return RedirectToAction(nameof(DanhGia), new { toiDaSao });
    }

    /// <summary>Đánh dấu góp ý đã xem / chưa xem.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DoiTrangThaiGopY(int ma, int? toiDaSao)
    {
        var danhGia = await _csdl.DanhGiaDonHang.FindAsync(ma);
        if (danhGia == null) return NotFound();
        danhGia.DaXem = !danhGia.DaXem;
        await _csdl.SaveChangesAsync();
        return RedirectToAction(nameof(DanhGia), new { toiDaSao });
    }

    // ================= MÓN ĂN =================
    public async Task<IActionResult> DanhSachMon() =>
        View(await _csdl.MonAn.AsNoTracking().Include(m => m.NhomMon)
            .OrderBy(m => m.NhomMon.ThuTuHienThi).ThenBy(m => m.TenMon).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> FormMon(int? ma)
    {
        var vm = new FormMonVM { DanhSachNhom = await _csdl.NhomMon.AsNoTracking().OrderBy(n => n.ThuTuHienThi).ToListAsync() };
        if (ma != null)
        {
            var mon = await _csdl.MonAn.AsNoTracking().FirstOrDefaultAsync(m => m.MaMonAn == ma);
            if (mon == null) return NotFound();
            vm.MaMonAn = mon.MaMonAn; vm.MaNhomMon = mon.MaNhomMon; vm.TenMon = mon.TenMon; vm.TenTiengAnh = mon.TenTiengAnh;
            vm.MoTa = mon.MoTa; vm.Gia = mon.Gia; vm.DuongDanAnh = mon.DuongDanAnh; vm.LaBanChay = mon.LaBanChay; vm.DangBan = mon.DangBan;
        }
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> FormMon(FormMonVM vm)
    {
        vm.DanhSachNhom = await _csdl.NhomMon.AsNoTracking().OrderBy(n => n.ThuTuHienThi).ToListAsync();

        string? duongDanAnhMoi = null;
        if (vm.TepAnh is { Length: > 0 })
        {
            var duoiTep = Path.GetExtension(vm.TepAnh.FileName).ToLowerInvariant();
            if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(duoiTep) || vm.TepAnh.Length > 2 * 1024 * 1024)
                ModelState.AddModelError(nameof(vm.TepAnh), "Chỉ nhận ảnh .jpg, .png, .webp dưới 2MB.");
            else
            {
                var thuMuc = Path.Combine(_moiTruong.WebRootPath, "uploads");
                Directory.CreateDirectory(thuMuc);
                var tenTep = Guid.NewGuid().ToString("N") + duoiTep;
                await using var luong = System.IO.File.Create(Path.Combine(thuMuc, tenTep));
                await vm.TepAnh.CopyToAsync(luong);
                duongDanAnhMoi = "/uploads/" + tenTep;
            }
        }
        if (!ModelState.IsValid) return View(vm);

        MonAn mon;
        if (vm.MaMonAn == 0) { mon = new MonAn(); _csdl.MonAn.Add(mon); }
        else mon = await _csdl.MonAn.FindAsync(vm.MaMonAn) ?? throw new InvalidOperationException("Không tìm thấy món");

        mon.MaNhomMon = vm.MaNhomMon; mon.TenMon = vm.TenMon.Trim(); mon.TenTiengAnh = vm.TenTiengAnh?.Trim();
        mon.MoTa = vm.MoTa?.Trim(); mon.Gia = vm.Gia; mon.LaBanChay = vm.LaBanChay; mon.DangBan = vm.DangBan;
        if (duongDanAnhMoi != null) mon.DuongDanAnh = duongDanAnhMoi;

        try { await _csdl.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(vm.TenMon), "Tên món đã tồn tại trong nhóm này.");
            return View(vm);
        }
        TempData["ThanhCong"] = "Đã lưu món.";
        return RedirectToAction(nameof(DanhSachMon));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DoiTrangThaiMon(int ma, string truong)
    {
        var mon = await _csdl.MonAn.FindAsync(ma);
        if (mon == null) return NotFound();
        if (truong == "dangban") mon.DangBan = !mon.DangBan;
        else if (truong == "banchay") mon.LaBanChay = !mon.LaBanChay;
        await _csdl.SaveChangesAsync();
        return RedirectToAction(nameof(DanhSachMon));
    }

    // ---- Công thức (định mức nguyên liệu) của 1 món - theo size nếu món có phân size ----
    [HttpGet]
    public async Task<IActionResult> CongThuc(int ma, int? maKichCo)
    {
        var mon = await _csdl.MonAn.AsNoTracking().FirstOrDefaultAsync(m => m.MaMonAn == ma);
        if (mon == null) return NotFound();
        var danhSachSize = await (from k in _csdl.MonAnKichCo.AsNoTracking()
                                  where k.MaMonAn == ma
                                  orderby k.KichCo.ThuTu
                                  select k.KichCo).ToListAsync();
        // Món có size: mặc định mở size đầu tiên nếu chưa chọn. Món không size: luôn dùng công thức mặc định (null).
        if (danhSachSize.Count > 0 && (maKichCo == null || !danhSachSize.Any(s => s.MaKichCo == maKichCo)))
            maKichCo = danhSachSize[0].MaKichCo;
        else if (danhSachSize.Count == 0) maKichCo = null;

        return View(new CongThucVM
        {
            MonAn = mon,
            MaKichCo = maKichCo,
            DanhSachKichCoMon = danhSachSize,
            DanhSachNguyenLieu = await _csdl.NguyenLieu.AsNoTracking().Where(n => n.DangHoatDong).OrderBy(n => n.TenNguyenLieu).ToListAsync(),
            DangDung = await _csdl.CongThucMon.AsNoTracking().Where(r => r.MaMonAn == ma && r.MaKichCo == maKichCo)
                .ToDictionaryAsync(r => r.MaNguyenLieu, r => r.SoLuongDung)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CongThuc(int ma, int? maKichCo, List<int> danhSachMaNguyenLieu, List<decimal?> danhSachSoLuong)
    {
        var hienCo = await _csdl.CongThucMon.Where(r => r.MaMonAn == ma && r.MaKichCo == maKichCo).ToListAsync();
        for (var i = 0; i < danhSachMaNguyenLieu.Count && i < danhSachSoLuong.Count; i++)
        {
            var maNguyenLieu = danhSachMaNguyenLieu[i];
            var soLuong = danhSachSoLuong[i] ?? 0;
            var dong = hienCo.FirstOrDefault(r => r.MaNguyenLieu == maNguyenLieu);
            if (soLuong > 0)
            {
                if (dong == null) _csdl.CongThucMon.Add(new CongThucMon { MaMonAn = ma, MaKichCo = maKichCo, MaNguyenLieu = maNguyenLieu, SoLuongDung = soLuong });
                else dong.SoLuongDung = soLuong;
            }
            else if (dong != null) _csdl.CongThucMon.Remove(dong);
        }
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = "Đã lưu công thức.";
        return RedirectToAction(nameof(CongThuc), new { ma, maKichCo });
    }

    // ---- Size & giá riêng theo size của 1 món ----
    [HttpGet]
    public async Task<IActionResult> KichCoMon(int ma)
    {
        var mon = await _csdl.MonAn.AsNoTracking().FirstOrDefaultAsync(m => m.MaMonAn == ma);
        if (mon == null) return NotFound();
        return View(new KichCoMonVM
        {
            MonAn = mon,
            TatCaKichCo = await _csdl.KichCo.AsNoTracking().OrderBy(k => k.ThuTu).ToListAsync(),
            GiaTheoSize = await _csdl.MonAnKichCo.AsNoTracking().Where(k => k.MaMonAn == ma).ToDictionaryAsync(k => k.MaKichCo, k => k.Gia)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> KichCoMon(int ma, List<int> danhSachMaKichCo, List<decimal?> danhSachGia)
    {
        var mon = await _csdl.MonAn.FindAsync(ma);
        if (mon == null) return NotFound();
        var hienCo = await _csdl.MonAnKichCo.Where(k => k.MaMonAn == ma).ToListAsync();
        for (var i = 0; i < danhSachMaKichCo.Count && i < danhSachGia.Count; i++)
        {
            var maKichCo = danhSachMaKichCo[i];
            var gia = danhSachGia[i];
            var dong = hienCo.FirstOrDefault(k => k.MaKichCo == maKichCo);
            if (gia is > 0)
            {
                if (dong == null) _csdl.MonAnKichCo.Add(new MonAnKichCo { MaMonAn = ma, MaKichCo = maKichCo, Gia = gia.Value });
                else dong.Gia = gia.Value;
            }
            else if (dong != null) _csdl.MonAnKichCo.Remove(dong);
        }
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = "Đã lưu size & giá. Nhớ vào \"Công thức\" khai báo định mức nguyên liệu cho từng size.";
        return RedirectToAction(nameof(KichCoMon), new { ma });
    }

    // ---- Topping ----
    public async Task<IActionResult> DanhSachTopping() =>
        View(await _csdl.Topping.AsNoTracking().OrderBy(t => t.MaTopping).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LuuTopping(int maTopping, string tenTopping, decimal gia, bool dangBan)
    {
        if (string.IsNullOrWhiteSpace(tenTopping) || gia < 0)
        { TempData["CanhBao"] = "Tên topping và giá không hợp lệ."; return RedirectToAction(nameof(DanhSachTopping)); }
        var topping = maTopping == 0 ? new Topping() : await _csdl.Topping.FindAsync(maTopping);
        if (topping == null) return NotFound();
        topping.TenTopping = tenTopping.Trim(); topping.Gia = gia; topping.DangBan = dangBan;
        if (maTopping == 0) _csdl.Topping.Add(topping);
        try { await _csdl.SaveChangesAsync(); TempData["ThanhCong"] = "Đã lưu topping."; }
        catch (DbUpdateException) { TempData["CanhBao"] = "Tên topping đã tồn tại."; }
        return RedirectToAction(nameof(DanhSachTopping));
    }

    // ================= NHÂN VIÊN =================
    public async Task<IActionResult> NhanVien() =>
        View(await _csdl.NguoiDung.AsNoTracking().Include(n => n.VaiTro).OrderBy(n => n.MaVaiTro).ThenBy(n => n.TenDangNhap).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> FormNhanVien() =>
        View(new FormNhanVienVM { DanhSachVaiTro = await _csdl.VaiTro.AsNoTracking().ToListAsync() });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> FormNhanVien(FormNhanVienVM vm)
    {
        vm.DanhSachVaiTro = await _csdl.VaiTro.AsNoTracking().ToListAsync();
        if (await _csdl.NguoiDung.AnyAsync(n => n.TenDangNhap == vm.TenDangNhap.Trim()))
            ModelState.AddModelError(nameof(vm.TenDangNhap), "Tên đăng nhập đã tồn tại.");
        if (!vm.DanhSachVaiTro.Any(v => v.MaVaiTro == vm.MaVaiTro))
            ModelState.AddModelError(nameof(vm.MaVaiTro), "Chọn vai trò hợp lệ.");

        var soChuan = DichVuKhuyenMai.ChuanHoaSoDienThoai(vm.SoDienThoai);
        if (soChuan == null)
            ModelState.AddModelError(nameof(vm.SoDienThoai), "Số điện thoại không hợp lệ.");
        if (!ModelState.IsValid) return View(vm);

        var muoi = MaHoaMatKhau.TaoMuoi();
        _csdl.NguoiDung.Add(new NguoiDung
        {
            TenDangNhap = vm.TenDangNhap.Trim(), HoTen = vm.HoTen.Trim(), SoDienThoai = soChuan, MaVaiTro = vm.MaVaiTro,
            MuoiMatKhau = muoi, MatKhauHash = MaHoaMatKhau.Bam(vm.MatKhau, muoi)
        });
        _nhatKy.Ghi(HangSoNhatKy.TaoTaiKhoan, $"Tạo tài khoản \"{vm.TenDangNhap.Trim()}\" ({vm.HoTen.Trim()}), vai trò {vm.DanhSachVaiTro.First(v => v.MaVaiTro == vm.MaVaiTro).TenVaiTro}.", MaNguoiDungHienTai);
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = "Đã tạo tài khoản.";
        return RedirectToAction(nameof(NhanVien));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DatLaiMatKhau(int ma, string matKhauMoi)
    {
        if (string.IsNullOrEmpty(matKhauMoi) || matKhauMoi.Length < 8)
        {
            TempData["CanhBao"] = "Mật khẩu mới tối thiểu 8 ký tự.";
            return RedirectToAction(nameof(NhanVien));
        }
        var nguoiDung = await _csdl.NguoiDung.FindAsync(ma);
        if (nguoiDung == null) return NotFound();
        nguoiDung.MuoiMatKhau = MaHoaMatKhau.TaoMuoi();
        nguoiDung.MatKhauHash = MaHoaMatKhau.Bam(matKhauMoi, nguoiDung.MuoiMatKhau);
        _nhatKy.Ghi(HangSoNhatKy.DatLaiMatKhau, $"Đặt lại mật khẩu cho tài khoản \"{nguoiDung.TenDangNhap}\".", MaNguoiDungHienTai);
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = $"Đã đặt lại mật khẩu cho {nguoiDung.TenDangNhap}.";
        return RedirectToAction(nameof(NhanVien));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SuaSoDienThoaiNhanVien(int ma, string? soDienThoai)
    {
        var nguoiDung = await _csdl.NguoiDung.FindAsync(ma);
        if (nguoiDung == null) return NotFound();

        var soChuan = DichVuKhuyenMai.ChuanHoaSoDienThoai(soDienThoai);
        if (soChuan == null)
        {
            TempData["CanhBao"] = "Số điện thoại không hợp lệ.";
            return RedirectToAction(nameof(NhanVien));
        }
        if (await _csdl.NguoiDung.AnyAsync(n => n.MaNguoiDung != ma && n.SoDienThoai == soChuan))
        {
            TempData["CanhBao"] = "Số điện thoại này đã gắn với tài khoản khác.";
            return RedirectToAction(nameof(NhanVien));
        }

        nguoiDung.SoDienThoai = soChuan;
        _nhatKy.Ghi(HangSoNhatKy.SuaCaiDat, $"Cập nhật số điện thoại cho tài khoản \"{nguoiDung.TenDangNhap}\".", MaNguoiDungHienTai);
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = $"Đã cập nhật số điện thoại cho {nguoiDung.TenDangNhap}.";
        return RedirectToAction(nameof(NhanVien));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> KhoaMoNhanVien(int ma)
    {
        if (ma == MaNguoiDungHienTai) { TempData["CanhBao"] = "Không thể khóa chính tài khoản đang đăng nhập."; return RedirectToAction(nameof(NhanVien)); }
        var nguoiDung = await _csdl.NguoiDung.FindAsync(ma);
        if (nguoiDung == null) return NotFound();
        nguoiDung.DangHoatDong = !nguoiDung.DangHoatDong;
        _nhatKy.Ghi(HangSoNhatKy.KhoaMoTaiKhoan, $"{(nguoiDung.DangHoatDong ? "Mở lại" : "Khóa")} tài khoản \"{nguoiDung.TenDangNhap}\".", MaNguoiDungHienTai);
        await _csdl.SaveChangesAsync();
        return RedirectToAction(nameof(NhanVien));
    }

    // ================= KHO =================
    public async Task<IActionResult> Kho() =>
        View(await _csdl.NguyenLieu.AsNoTracking().OrderBy(n => n.TenNguyenLieu).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ThemNguyenLieu(string tenNguyenLieu, string donViTinh, string? donViNhap, decimal quyDoiSoLuong,
        decimal? dinhMucMoiCoc, int soCocCanhBao, decimal mucToiThieuKhoTong)
    {
        if (string.IsNullOrWhiteSpace(tenNguyenLieu) || string.IsNullOrWhiteSpace(donViTinh) || quyDoiSoLuong <= 0 || mucToiThieuKhoTong < 0)
        { TempData["CanhBao"] = "Nhập đủ tên, đơn vị tính và quy đổi hợp lệ (quy đổi phải lớn hơn 0)."; return RedirectToAction(nameof(Kho)); }
        _csdl.NguyenLieu.Add(new NguyenLieu
        {
            TenNguyenLieu = tenNguyenLieu.Trim(), DonViTinh = donViTinh.Trim(),
            DonViNhap = string.IsNullOrWhiteSpace(donViNhap) ? null : donViNhap.Trim(), QuyDoiSoLuong = quyDoiSoLuong,
            DinhMucMoiCoc = dinhMucMoiCoc is > 0 ? dinhMucMoiCoc : null, SoCocCanhBao = soCocCanhBao <= 0 ? 10 : soCocCanhBao,
            MucToiThieuKhoTong = mucToiThieuKhoTong
        });
        try { await _csdl.SaveChangesAsync(); TempData["ThanhCong"] = "Đã thêm nguyên liệu."; }
        catch (DbUpdateException) { TempData["CanhBao"] = "Nguyên liệu này đã tồn tại."; }
        return RedirectToAction(nameof(Kho));
    }

    /// <summary>Cập nhật đơn vị nhập + quy đổi + định mức 1 cốc (cảnh báo kho dùng hàng ngày) + mức tối thiểu kho tổng.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatDinhMucKho(int ma, string? donViNhap, decimal quyDoiSoLuong, decimal? dinhMucMoiCoc,
        int soCocCanhBao, decimal mucToiThieuKhoTong)
    {
        var nguyenLieu = await _csdl.NguyenLieu.FindAsync(ma);
        if (nguyenLieu == null || quyDoiSoLuong <= 0 || mucToiThieuKhoTong < 0) return BadRequest();
        nguyenLieu.DonViNhap = string.IsNullOrWhiteSpace(donViNhap) ? null : donViNhap.Trim();
        nguyenLieu.QuyDoiSoLuong = quyDoiSoLuong;
        nguyenLieu.DinhMucMoiCoc = dinhMucMoiCoc is > 0 ? dinhMucMoiCoc : null;
        nguyenLieu.SoCocCanhBao = soCocCanhBao <= 0 ? 10 : soCocCanhBao;
        nguyenLieu.MucToiThieuKhoTong = mucToiThieuKhoTong;
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = "Đã lưu định mức kho.";
        return RedirectToAction(nameof(Kho));
    }

    /// <summary>Kiểm kê / hao hụt: cộng hoặc trừ tồn kho kèm lý do - áp dụng cho kho TỔNG hoặc kho DÙNG HÀNG NGÀY tùy "thuocKho".</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DieuChinhTon(int ma, decimal chenhLech, string? ghiChu, string thuocKho)
    {
        var nguyenLieu = await _csdl.NguyenLieu.FindAsync(ma);
        if (nguyenLieu == null || chenhLech == 0) return RedirectToAction(nameof(Kho));
        var laKhoTong = thuocKho == HangSoThuocKho.Tong;
        if (laKhoTong) nguyenLieu.SoLuongTonKhoTong += chenhLech; else nguyenLieu.SoLuongTon += chenhLech;
        _csdl.BienDongKho.Add(new BienDongKho
        {
            MaNguyenLieu = ma, SoLuongThayDoi = chenhLech, LoaiBienDong = HangSoBienDong.DieuChinh,
            ThuocKho = laKhoTong ? HangSoThuocKho.Tong : HangSoThuocKho.SuDung,
            MaNguoiThucHien = MaNguoiDungHienTai, GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? "Điều chỉnh kiểm kê" : ghiChu.Trim()
        });
        var donVi = laKhoTong ? (nguyenLieu.DonViNhap ?? nguyenLieu.DonViTinh) : nguyenLieu.DonViTinh;
        _nhatKy.Ghi(HangSoNhatKy.DieuChinhKho,
            $"Điều chỉnh {(laKhoTong ? "kho tổng" : "kho dùng hàng ngày")} - {nguyenLieu.TenNguyenLieu}: {(chenhLech > 0 ? "+" : "")}{chenhLech.SoThapPhan()} {donVi}. " +
            $"Lý do: {(string.IsNullOrWhiteSpace(ghiChu) ? "Điều chỉnh kiểm kê" : ghiChu.Trim())}.", MaNguoiDungHienTai);
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = "Đã điều chỉnh tồn kho.";
        return RedirectToAction(nameof(Kho));
    }

    [HttpGet]
    public async Task<IActionResult> NhapKho()
    {
        ViewBag.DanhSachNguyenLieu = await _csdl.NguyenLieu.AsNoTracking().Where(n => n.DangHoatDong).OrderBy(n => n.TenNguyenLieu).ToListAsync();
        ViewBag.DanhSachNhaCungCap = await _csdl.NhaCungCap.AsNoTracking().OrderBy(n => n.TenNhaCungCap).ToListAsync();
        return View(new FormNhapKhoVM());
    }

    /// <summary>Nhập kho TỔNG (từ nhà cung cấp) - số lượng tính theo đơn vị NHẬP (chai/túi...), không phải đơn vị dùng hàng ngày.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NhapKho(FormNhapKhoVM vm)
    {
        var danhSachDong = new List<(int MaNguyenLieu, decimal SoLuong, decimal DonGia)>();
        for (var i = 0; i < vm.DanhSachMaNguyenLieu.Count; i++)
        {
            var soLuong = i < vm.DanhSachSoLuong.Count ? vm.DanhSachSoLuong[i] : 0;
            var donGia = i < vm.DanhSachDonGia.Count ? vm.DanhSachDonGia[i] : 0;
            if (soLuong > 0 && donGia >= 0) danhSachDong.Add((vm.DanhSachMaNguyenLieu[i], soLuong, donGia));
        }
        if (danhSachDong.Count == 0) { TempData["CanhBao"] = "Phiếu nhập cần ít nhất 1 dòng có số lượng > 0."; return RedirectToAction(nameof(NhapKho)); }

        NhaCungCap? nhaCungCap = null;
        if (!string.IsNullOrWhiteSpace(vm.TenNhaCungCap))
        {
            var ten = vm.TenNhaCungCap.Trim();
            nhaCungCap = await _csdl.NhaCungCap.FirstOrDefaultAsync(n => n.TenNhaCungCap == ten) ?? new NhaCungCap { TenNhaCungCap = ten };
        }

        var phieuNhap = new PhieuNhap { NhaCungCap = nhaCungCap, MaNguoiNhap = MaNguoiDungHienTai, GhiChu = vm.GhiChu?.Trim(), NgayNhap = DateTime.Now };
        var danhSachMa = danhSachDong.Select(d => d.MaNguyenLieu).Distinct().ToList();
        var nguyenLieu = await _csdl.NguyenLieu.Where(n => danhSachMa.Contains(n.MaNguyenLieu)).ToDictionaryAsync(n => n.MaNguyenLieu);

        await using var giaoDich = await _csdl.Database.BeginTransactionAsync();
        _csdl.PhieuNhap.Add(phieuNhap);
        await _csdl.SaveChangesAsync();   // lấy MaPhieuNhap

        foreach (var (maNguyenLieu, soLuong, donGia) in danhSachDong)
        {
            if (!nguyenLieu.TryGetValue(maNguyenLieu, out var nl)) continue;
            phieuNhap.DanhSachChiTiet.Add(new ChiTietPhieuNhap { MaNguyenLieu = maNguyenLieu, SoLuong = soLuong, DonGiaNhap = donGia });
            nl.SoLuongTonKhoTong += soLuong;
            _csdl.BienDongKho.Add(new BienDongKho
            {
                MaNguyenLieu = maNguyenLieu, SoLuongThayDoi = soLuong, LoaiBienDong = HangSoBienDong.Nhap, ThuocKho = HangSoThuocKho.Tong,
                MaPhieuNhap = phieuNhap.MaPhieuNhap, MaNguoiThucHien = MaNguoiDungHienTai, GhiChu = "Nhập kho"
            });
        }
        _nhatKy.Ghi(HangSoNhatKy.NhapKho, $"Nhập kho {danhSachDong.Count} dòng nguyên liệu" + (nhaCungCap != null ? $" từ {nhaCungCap.TenNhaCungCap}" : "") + ".", MaNguoiDungHienTai);
        await _csdl.SaveChangesAsync();
        await giaoDich.CommitAsync();
        TempData["ThanhCong"] = "Đã nhập kho tổng và cập nhật tồn.";
        return RedirectToAction(nameof(Kho));
    }

    /// <summary>Sổ kho: chỉ còn vết của kho TỔNG (nhập từ nhà cung cấp + xuất kho sang kho dùng hàng ngày + điều chỉnh kho tổng).
    /// Trừ nguyên liệu theo từng đơn hàng mỗi ngày xem ở "Nguyên liệu hàng ngày".</summary>
    public async Task<IActionResult> SoKho() =>
        View(await _csdl.BienDongKho.AsNoTracking().Include(b => b.NguyenLieu)
            .Where(b => b.ThuocKho == HangSoThuocKho.Tong)
            .OrderByDescending(b => b.MaBienDong).Take(200).ToListAsync());

    /// <summary>Nguyên liệu hàng ngày: vết trừ theo công thức món (tự động khi lên đơn), hoàn kho khi hủy đơn,
    /// và nhận từ phiếu xuất kho - tất cả đều ở kho DÙNG HÀNG NGÀY.</summary>
    public async Task<IActionResult> NguyenLieuHangNgay(DateTime? tuNgay, DateTime? denNgay)
    {
        var tu = (tuNgay ?? DateTime.Today).Date;
        var den = (denNgay ?? DateTime.Today).Date.AddDays(1);
        ViewBag.TuNgay = tu;
        ViewBag.DenNgay = denNgay ?? DateTime.Today;
        return View(await _csdl.BienDongKho.AsNoTracking().Include(b => b.NguyenLieu)
            .Where(b => b.ThuocKho == HangSoThuocKho.SuDung && b.NgayTao >= tu && b.NgayTao < den)
            .OrderByDescending(b => b.MaBienDong).Take(500).ToListAsync());
    }

    // ================= PHIẾU XUẤT KHO (nhân viên/quản lý lập ở trang Cảnh báo kho, quản lý duyệt tại đây) =================
    public async Task<IActionResult> PhieuXuatKho() =>
        View(await _csdl.PhieuXuatKho.AsNoTracking().Include(p => p.NguoiTao).Include(p => p.NguoiDuyet)
            .Include(p => p.DanhSachChiTiet).ThenInclude(c => c.NguyenLieu)
            .OrderByDescending(p => p.MaPhieuXuat).Take(100).ToListAsync());

    /// <summary>Duyệt & thực hiện xuất kho: trừ kho tổng (đơn vị nhập), quy đổi cộng vào kho dùng hàng ngày (đơn vị tính).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DuyetPhieuXuatKho(int ma)
    {
        var phieu = await _csdl.PhieuXuatKho.Include(p => p.DanhSachChiTiet).ThenInclude(c => c.NguyenLieu)
            .FirstOrDefaultAsync(p => p.MaPhieuXuat == ma);
        if (phieu == null) return NotFound();
        if (phieu.TrangThai != HangSoTrangThaiXuatKho.ChoDuyet)
        { TempData["CanhBao"] = "Phiếu này đã được xử lý trước đó."; return RedirectToAction(nameof(PhieuXuatKho)); }

        await using var giaoDich = await _csdl.Database.BeginTransactionAsync();
        foreach (var ct in phieu.DanhSachChiTiet)
        {
            var nl = ct.NguyenLieu;
            nl.SoLuongTonKhoTong -= ct.SoLuong;   // có thể âm -> quản lý biết kho tổng đang thiếu, cần nhập thêm
            var soLuongQuyDoi = ct.SoLuong * nl.QuyDoiSoLuong;
            nl.SoLuongTon += soLuongQuyDoi;
            _csdl.BienDongKho.Add(new BienDongKho
            {
                MaNguyenLieu = nl.MaNguyenLieu, SoLuongThayDoi = -ct.SoLuong, LoaiBienDong = HangSoBienDong.XuatKho, ThuocKho = HangSoThuocKho.Tong,
                MaPhieuXuat = phieu.MaPhieuXuat, MaNguoiThucHien = MaNguoiDungHienTai, GhiChu = "Xuất kho sang kho dùng hàng ngày"
            });
            _csdl.BienDongKho.Add(new BienDongKho
            {
                MaNguyenLieu = nl.MaNguyenLieu, SoLuongThayDoi = soLuongQuyDoi, LoaiBienDong = HangSoBienDong.NhapTuXuatKho, ThuocKho = HangSoThuocKho.SuDung,
                MaPhieuXuat = phieu.MaPhieuXuat, MaNguoiThucHien = MaNguoiDungHienTai, GhiChu = "Nhận từ phiếu xuất kho"
            });
        }
        phieu.TrangThai = HangSoTrangThaiXuatKho.DaXuat;
        phieu.MaNguoiDuyet = MaNguoiDungHienTai;
        phieu.NgayDuyet = DateTime.Now;
        _nhatKy.Ghi(HangSoNhatKy.DuyetPhieuXuatKho, $"Duyệt & thực hiện phiếu xuất kho #{phieu.MaPhieuXuat} ({phieu.DanhSachChiTiet.Count} dòng nguyên liệu).", MaNguoiDungHienTai);
        await _csdl.SaveChangesAsync();
        await giaoDich.CommitAsync();
        TempData["ThanhCong"] = "Đã xuất kho và cộng vào kho dùng hàng ngày.";
        return RedirectToAction(nameof(PhieuXuatKho));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TuChoiPhieuXuatKho(int ma, string? lyDo)
    {
        var phieu = await _csdl.PhieuXuatKho.FindAsync(ma);
        if (phieu == null) return NotFound();
        if (phieu.TrangThai != HangSoTrangThaiXuatKho.ChoDuyet)
        { TempData["CanhBao"] = "Phiếu này đã được xử lý trước đó."; return RedirectToAction(nameof(PhieuXuatKho)); }
        phieu.TrangThai = HangSoTrangThaiXuatKho.TuChoi;
        phieu.MaNguoiDuyet = MaNguoiDungHienTai;
        phieu.NgayDuyet = DateTime.Now;
        phieu.LyDoTuChoi = string.IsNullOrWhiteSpace(lyDo) ? null : lyDo.Trim();
        _nhatKy.Ghi(HangSoNhatKy.TuChoiPhieuXuatKho,
            $"Từ chối phiếu xuất kho #{phieu.MaPhieuXuat}." + (string.IsNullOrWhiteSpace(lyDo) ? "" : $" Lý do: {lyDo.Trim()}."), MaNguoiDungHienTai);
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = "Đã từ chối phiếu xuất kho.";
        return RedirectToAction(nameof(PhieuXuatKho));
    }

    // ================= BÀN & QR =================
    public async Task<IActionResult> DanhSachBan() =>
        View(await _csdl.BanAn.AsNoTracking().OrderBy(b => b.MaBan).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ThemBan(string tenBan)
    {
        if (string.IsNullOrWhiteSpace(tenBan)) return RedirectToAction(nameof(DanhSachBan));
        _csdl.BanAn.Add(new BanAn { TenBan = tenBan.Trim(), MaQr = TaoMaQr() });
        try { await _csdl.SaveChangesAsync(); TempData["ThanhCong"] = "Đã thêm bàn."; }
        catch (DbUpdateException) { TempData["CanhBao"] = "Tên bàn đã tồn tại."; }
        return RedirectToAction(nameof(DanhSachBan));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> KhoaMoBan(int ma)
    {
        var ban = await _csdl.BanAn.FindAsync(ma);
        if (ban == null) return NotFound();
        ban.DangHoatDong = !ban.DangHoatDong;
        await _csdl.SaveChangesAsync();
        return RedirectToAction(nameof(DanhSachBan));
    }

    /// <summary>Đổi mã khi QR bị lộ / cần vô hiệu hóa QR cũ (phải in lại).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DoiMaQr(int ma)
    {
        var ban = await _csdl.BanAn.FindAsync(ma);
        if (ban == null) return NotFound();
        ban.MaQr = TaoMaQr();
        _nhatKy.Ghi(HangSoNhatKy.DoiMaQr, $"Đổi mã QR của {ban.TenBan} (mã cũ ngừng dùng).", MaNguoiDungHienTai);
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = $"{ban.TenBan}: mã QR mới đã sẵn sàng, mã cũ không còn dùng được.";
        return RedirectToAction(nameof(DanhSachBan));
    }

    public async Task<IActionResult> AnhQrBan(int ma)
    {
        var ban = await _csdl.BanAn.AsNoTracking().FirstOrDefaultAsync(b => b.MaBan == ma);
        if (ban == null) return NotFound();
        var diaChiGoc = _cauHinh["CuaHang:DiaChiCongKhai"];
        if (string.IsNullOrWhiteSpace(diaChiGoc)) diaChiGoc = $"{Request.Scheme}://{Request.Host}";
        using var boTaoQr = new QRCodeGenerator();
        using var duLieuQr = boTaoQr.CreateQrCode($"{diaChiGoc.TrimEnd('/')}/t/{ban.MaQr}", QRCodeGenerator.ECCLevel.Q);
        var anhPng = new PngByteQRCode(duLieuQr).GetGraphic(12);
        return File(anhPng, "image/png");
    }

    public async Task<IActionResult> InQr() =>
        View(await _csdl.BanAn.AsNoTracking().Where(b => b.DangHoatDong).OrderBy(b => b.MaBan).ToListAsync());

    private static string TaoMaQr() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    // ================= LỊCH SỬ ĐƠN =================
    public async Task<IActionResult> LichSuDon(DateTime? tuNgay, DateTime? denNgay, int? trangThai)
    {
        var truyVan = _csdl.DonHang.AsQueryable();
        if (tuNgay != null) truyVan = truyVan.Where(d => d.NgayTao >= tuNgay.Value.Date);
        if (denNgay != null) truyVan = truyVan.Where(d => d.NgayTao < denNgay.Value.Date.AddDays(1));
        if (trangThai != null) truyVan = truyVan.Where(d => d.MaTrangThai == trangThai);

        var danhSachDong = await (from d in truyVan
                                  join t in _csdl.TongTienDon on d.MaDonHang equals t.MaDonHang
                                  orderby d.NgayTao descending
                                  select new DongDonHangVM
                                  {
                                      MaDonHang = d.MaDonHang, MaTheoDoi = d.MaTheoDoi, MaTrangThai = d.MaTrangThai, TongTien = t.TongTien,
                                      NgayTao = d.NgayTao, NgayThanhToan = d.NgayThanhToan,
                                      NoiPhucVu = d.BanAn != null ? d.BanAn.TenBan : "Mang đi",
                                      NguoiXuLy = d.NguoiXuLy != null ? d.NguoiXuLy.HoTen : null
                                  }).Take(300).ToListAsync();

        return View(new LichSuDonVM { DanhSachDong = danhSachDong, TuNgay = tuNgay, DenNgay = denNgay, TrangThai = trangThai });
    }

    // ================= CÀI ĐẶT QUÁN =================
    [HttpGet]
    public async Task<IActionResult> CaiDat() =>
        View(await _csdl.CaiDatCuaHang.AsNoTracking().FirstOrDefaultAsync() ?? new CaiDatCuaHang());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CaiDat(CaiDatCuaHang vm)
    {
        if (vm.SoPhutTuHuyDonMangDi < 0 || vm.SoPhutTuHuyDonMangDi > 240)
        {
            TempData["CanhBao"] = "Số phút tự hủy đơn phải từ 0 đến 240.";
            return RedirectToAction(nameof(CaiDat));
        }
        if (vm.SoDongTichMotDiem < 1000)
        {
            TempData["CanhBao"] = "Số đồng để tích 1 điểm phải từ 1.000đ trở lên.";
            return RedirectToAction(nameof(CaiDat));
        }
        var caiDat = await _csdl.CaiDatCuaHang.FirstOrDefaultAsync() ?? new CaiDatCuaHang { MaCaiDat = 1 };
        if (caiDat.MaCaiDat == 0) { caiDat.MaCaiDat = 1; _csdl.CaiDatCuaHang.Add(caiDat); }

        var moTa = new List<string>();
        if (caiDat.GioMo != vm.GioMo || caiDat.GioDong != vm.GioDong) moTa.Add($"giờ mở cửa {vm.GioMo:hh\\:mm}-{vm.GioDong:hh\\:mm}");
        if (caiDat.TamNgungNhanDon != vm.TamNgungNhanDon) moTa.Add(vm.TamNgungNhanDon ? "tạm ngưng nhận đơn" : "mở lại nhận đơn");
        if (caiDat.SoPhutTuHuyDonMangDi != vm.SoPhutTuHuyDonMangDi) moTa.Add($"tự hủy đơn mang đi sau {vm.SoPhutTuHuyDonMangDi} phút" + (vm.SoPhutTuHuyDonMangDi == 0 ? " (đã tắt)" : ""));
        if (caiDat.TuAnMonHetNguyenLieu != vm.TuAnMonHetNguyenLieu) moTa.Add(vm.TuAnMonHetNguyenLieu ? "bật tự ẩn món hết nguyên liệu" : "tắt tự ẩn món hết nguyên liệu");
        if (caiDat.SoDongTichMotDiem != vm.SoDongTichMotDiem) moTa.Add($"tích điểm: {vm.SoDongTichMotDiem:N0}đ / điểm");

        caiDat.GioMo = vm.GioMo; caiDat.GioDong = vm.GioDong; caiDat.TamNgungNhanDon = vm.TamNgungNhanDon;
        caiDat.SoPhutTuHuyDonMangDi = vm.SoPhutTuHuyDonMangDi; caiDat.TuAnMonHetNguyenLieu = vm.TuAnMonHetNguyenLieu;
        caiDat.SoDongTichMotDiem = vm.SoDongTichMotDiem;
        if (moTa.Count > 0) _nhatKy.Ghi(HangSoNhatKy.SuaCaiDat, "Đổi cài đặt quán: " + string.Join(", ", moTa) + ".", MaNguoiDungHienTai);
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = "Đã lưu cài đặt quán.";
        return RedirectToAction(nameof(CaiDat));
    }

    // ================= NHẬT KÝ THAO TÁC =================
    public async Task<IActionResult> NhatKy(DateTime? tuNgay, DateTime? denNgay, string? hanhDong)
    {
        var truyVan = _csdl.NhatKyThaoTac.AsNoTracking().Include(n => n.NguoiThucHien).AsQueryable();
        if (tuNgay != null) truyVan = truyVan.Where(n => n.NgayTao >= tuNgay.Value.Date);
        if (denNgay != null) truyVan = truyVan.Where(n => n.NgayTao < denNgay.Value.Date.AddDays(1));
        if (!string.IsNullOrEmpty(hanhDong)) truyVan = truyVan.Where(n => n.HanhDong == hanhDong);

        var danhSach = await truyVan.OrderByDescending(n => n.NgayTao).Take(301).ToListAsync();
        var daCatBot = danhSach.Count > 300;
        if (daCatBot) danhSach = danhSach.Take(300).ToList();

        return View(new NhatKyVM { DanhSachDong = danhSach, TuNgay = tuNgay, DenNgay = denNgay, HanhDong = hanhDong, DaCatBot = daCatBot });
    }

    // ================= BÁO CÁO LÃI GỘP =================
    /// <summary>Doanh thu - giá vốn (bình quân gia quyền) theo từng món, trong khoảng ngày chọn (mặc định 7 ngày gần nhất).</summary>
    public async Task<IActionResult> BaoCaoLaiGop(DateTime? tuNgay, DateTime? denNgay)
    {
        var vm = await DungBaoCaoLaiGopAsync(tuNgay, denNgay);
        return View(vm);
    }

    public async Task<IActionResult> XuatExcelLaiGop(DateTime? tuNgay, DateTime? denNgay)
    {
        var vm = await DungBaoCaoLaiGopAsync(tuNgay, denNgay);
        var cot = new[] { "Tên món", "Số lượng bán", "Doanh thu", "Giá vốn", "Lãi gộp", "Tỷ lệ lãi gộp (%)", "Ghi chú" };
        var dong = vm.DanhSachMon.Select(m => new object?[]
        {
            m.TenMon, m.SoLuongBan, m.DoanhThu, m.GiaVon, m.LaiGop, m.TyLeLaiGop,
            m.ThieuGiaVon ? "Chưa đủ dữ liệu giá vốn (thiếu công thức hoặc chưa nhập kho)" : ""
        });
        var file = _excel.Tao("Lãi gộp", "Báo cáo lãi gộp theo món",
            new[] { $"Từ ngày: {vm.TuNgay:dd/MM/yyyy}", $"Đến ngày: {vm.DenNgay:dd/MM/yyyy}", $"Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}" },
            cot, dong, cotTien: new[] { 2, 3, 4 });
        return File(file, DichVuXuatExcel.LoaiTep, $"LaiGop_{vm.TuNgay:yyyyMMdd}_{vm.DenNgay:yyyyMMdd}.xlsx");
    }

    private async Task<BaoCaoLaiGopVM> DungBaoCaoLaiGopAsync(DateTime? tuNgay, DateTime? denNgay)
    {
        var tu = (tuNgay ?? DateTime.Today.AddDays(-6)).Date;
        var den = (denNgay ?? DateTime.Today).Date;

        // Tính doanh thu từng dòng chi tiết trước (bao gồm tổng topping của riêng dòng đó qua subquery),
        // rồi mới gộp nhóm theo món ở phía ứng dụng - tránh lỗi SQL Server không cho lồng SUM bên trong SUM.
        var chiTietTrongKy = await _csdl.ChiTietDonHang.AsNoTracking()
            .Where(c => c.DonHang.NgayTao >= tu && c.DonHang.NgayTao < den.AddDays(1) && c.DonHang.MaTrangThai != HangSoTrangThai.DaHuy)
            .Select(c => new
            {
                c.MaMonAn,
                c.MonAn.TenMon,
                c.MaKichCo,
                TenKichCo = c.KichCo != null ? c.KichCo.TenKichCo : null,
                c.SoLuong,
                ThanhTien = (c.DonGia + c.DanhSachTopping.Sum(t => t.DonGia)) * c.SoLuong
            })
            .ToListAsync();

        var doanhThuTheoMon = chiTietTrongKy
            .GroupBy(c => new { c.MaMonAn, c.TenMon, c.MaKichCo, c.TenKichCo })
            .Select(g => new
            {
                g.Key.MaMonAn, g.Key.TenMon, g.Key.MaKichCo, g.Key.TenKichCo,
                SoLuong = g.Sum(c => c.SoLuong),
                DoanhThu = g.Sum(c => c.ThanhTien)
            })
            .OrderByDescending(x => x.DoanhThu).ToList();

        var giaVon = await _giaVon.TinhAsync();

        var danhSach = doanhThuTheoMon.Select(m =>
        {
            var gv = giaVon.GiaVonCuaDong(m.MaMonAn, m.MaKichCo);
            return new DongLaiGopMonVM
            {
                TenMon = m.TenKichCo != null ? $"{m.TenMon} (size {m.TenKichCo})" : m.TenMon,
                SoLuongBan = m.SoLuong,
                DoanhThu = m.DoanhThu,
                GiaVon = (gv ?? 0) * m.SoLuong,
                ThieuGiaVon = giaVon.ThieuGiaVonDong(m.MaMonAn, m.MaKichCo),
                LaiGop = m.DoanhThu - (gv ?? 0) * m.SoLuong
            };
        }).ToList();

        return new BaoCaoLaiGopVM { TuNgay = tu, DenNgay = den, DanhSachMon = danhSach };
    }

    // ================= LÃI RÒNG THEO THÁNG (lãi gộp - chi phí vận hành nhập tay) =================
    /// <summary>Lãi ròng = (Doanh thu - Giá vốn) tự tính từ đơn hàng trong tháng, trừ đi các khoản chi phí
    /// vận hành (tiền nhà, lương nhân viên, điện nước...) do quản lý tự nhập tay cho từng tháng.</summary>
    public async Task<IActionResult> BaoCaoLaiRong(int? thang, int? nam)
    {
        var t = thang is >= 1 and <= 12 ? thang.Value : DateTime.Today.Month;
        var n = nam is >= 2000 and <= 2100 ? nam.Value : DateTime.Today.Year;
        var vm = await DungBaoCaoLaiRongAsync(t, n);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ThemChiPhi(FormChiPhiVM vm)
    {
        if (!ModelState.IsValid || vm.Thang < 1 || vm.Thang > 12 || vm.Nam < 2000)
        {
            TempData["CanhBao"] = "Dữ liệu chi phí không hợp lệ.";
            return RedirectToAction(nameof(BaoCaoLaiRong), new { thang = vm.Thang, nam = vm.Nam });
        }

        var chiPhi = new ChiPhiVanHanh
        {
            Thang = vm.Thang, Nam = vm.Nam, LoaiChiPhi = vm.LoaiChiPhi, SoTien = vm.SoTien,
            GhiChu = vm.GhiChu, MaNguoiTao = MaNguoiDungHienTai
        };
        _csdl.ChiPhiVanHanh.Add(chiPhi);
        _nhatKy.Ghi(HangSoNhatKy.ThemChiPhi,
            $"Thêm chi phí \"{HangSoLoaiChiPhi.Ten(vm.LoaiChiPhi)}\" {vm.SoTien:N0}đ cho tháng {vm.Thang}/{vm.Nam}.", MaNguoiDungHienTai);
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = "Đã thêm chi phí.";
        return RedirectToAction(nameof(BaoCaoLaiRong), new { thang = vm.Thang, nam = vm.Nam });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SuaChiPhi(int ma, decimal soTien, string? ghiChu)
    {
        var chiPhi = await _csdl.ChiPhiVanHanh.FindAsync(ma);
        if (chiPhi == null) return NotFound();
        if (soTien < 0)
        {
            TempData["CanhBao"] = "Số tiền không hợp lệ.";
            return RedirectToAction(nameof(BaoCaoLaiRong), new { thang = chiPhi.Thang, nam = chiPhi.Nam });
        }

        chiPhi.SoTien = soTien;
        chiPhi.GhiChu = ghiChu;
        _nhatKy.Ghi(HangSoNhatKy.SuaChiPhi,
            $"Sửa chi phí \"{HangSoLoaiChiPhi.Ten(chiPhi.LoaiChiPhi)}\" tháng {chiPhi.Thang}/{chiPhi.Nam} thành {soTien:N0}đ.", MaNguoiDungHienTai);
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = "Đã cập nhật chi phí.";
        return RedirectToAction(nameof(BaoCaoLaiRong), new { thang = chiPhi.Thang, nam = chiPhi.Nam });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> XoaChiPhi(int ma)
    {
        var chiPhi = await _csdl.ChiPhiVanHanh.FindAsync(ma);
        if (chiPhi == null) return NotFound();
        var thang = chiPhi.Thang; var nam = chiPhi.Nam;
        _nhatKy.Ghi(HangSoNhatKy.XoaChiPhi,
            $"Xóa chi phí \"{HangSoLoaiChiPhi.Ten(chiPhi.LoaiChiPhi)}\" {chiPhi.SoTien:N0}đ tháng {thang}/{nam}.", MaNguoiDungHienTai);
        _csdl.ChiPhiVanHanh.Remove(chiPhi);
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = "Đã xóa chi phí.";
        return RedirectToAction(nameof(BaoCaoLaiRong), new { thang, nam });
    }

    public async Task<IActionResult> XuatExcelLaiRong(int? thang, int? nam)
    {
        var t = thang is >= 1 and <= 12 ? thang.Value : DateTime.Today.Month;
        var n = nam is >= 2000 and <= 2100 ? nam.Value : DateTime.Today.Year;
        var vm = await DungBaoCaoLaiRongAsync(t, n);

        var cot = new[] { "Khoản mục", "Số tiền", "Ghi chú" };
        var dong = new List<object?[]>
        {
            new object?[] { "Doanh thu", vm.TongDoanhThu, "" },
            new object?[] { "Giá vốn", vm.TongGiaVon, "" },
            new object?[] { "Lãi gộp", vm.TongLaiGop, "" },
        };
        foreach (var cp in vm.DanhSachChiPhi)
            dong.Add(new object?[] { HangSoLoaiChiPhi.Ten(cp.LoaiChiPhi), cp.SoTien, cp.GhiChu ?? "" });
        dong.Add(new object?[] { "Tổng chi phí vận hành", vm.TongChiPhi, "" });
        dong.Add(new object?[] { "LÃI RÒNG", vm.LaiRong, "" });

        var file = _excel.Tao("Lãi ròng", $"Báo cáo lãi ròng tháng {vm.Thang}/{vm.Nam}",
            new[] { $"Tháng: {vm.Thang}/{vm.Nam}", $"Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}" },
            cot, dong, cotTien: new[] { 1 });
        return File(file, DichVuXuatExcel.LoaiTep, $"LaiRong_{vm.Nam}{vm.Thang:D2}.xlsx");
    }

    private async Task<BaoCaoLaiRongVM> DungBaoCaoLaiRongAsync(int thang, int nam)
    {
        var tu = new DateTime(nam, thang, 1);
        var den = tu.AddMonths(1);

        var chiTietTrongThang = await _csdl.ChiTietDonHang.AsNoTracking()
            .Where(c => c.DonHang.NgayTao >= tu && c.DonHang.NgayTao < den && c.DonHang.MaTrangThai != HangSoTrangThai.DaHuy)
            .Select(c => new
            {
                c.MaMonAn,
                c.MaKichCo,
                c.SoLuong,
                ThanhTien = (c.DonGia + c.DanhSachTopping.Sum(x => x.DonGia)) * c.SoLuong
            })
            .ToListAsync();

        var giaVon = await _giaVon.TinhAsync();

        var tongDoanhThu = chiTietTrongThang.Sum(c => c.ThanhTien);
        decimal tongGiaVon = 0;
        var coMonThieu = false;
        foreach (var nhom in chiTietTrongThang.GroupBy(c => new { c.MaMonAn, c.MaKichCo }))
        {
            var gv = giaVon.GiaVonCuaDong(nhom.Key.MaMonAn, nhom.Key.MaKichCo);
            tongGiaVon += (gv ?? 0) * nhom.Sum(x => x.SoLuong);
            if (giaVon.ThieuGiaVonDong(nhom.Key.MaMonAn, nhom.Key.MaKichCo)) coMonThieu = true;
        }

        var danhSachChiPhi = await _csdl.ChiPhiVanHanh.AsNoTracking().Include(c => c.NguoiTao)
            .Where(c => c.Thang == thang && c.Nam == nam)
            .OrderBy(c => c.LoaiChiPhi).ThenByDescending(c => c.MaChiPhi)
            .ToListAsync();

        return new BaoCaoLaiRongVM
        {
            Thang = thang, Nam = nam,
            TongDoanhThu = tongDoanhThu, TongGiaVon = tongGiaVon, CoMonThieuGiaVon = coMonThieu,
            DanhSachChiPhi = danhSachChiPhi,
            FormMoi = new FormChiPhiVM { Thang = thang, Nam = nam }
        };
    }

    // ================= MÃ GIẢM GIÁ =================
    public async Task<IActionResult> MaGiamGia() =>
        View(await _csdl.MaGiamGia.AsNoTracking().OrderByDescending(m => m.MaMaGiamGia).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> FormMaGiamGia(int? ma)
    {
        if (ma == null) return View(new FormMaGiamGiaVM());
        var m = await _csdl.MaGiamGia.AsNoTracking().FirstOrDefaultAsync(x => x.MaMaGiamGia == ma);
        if (m == null) return NotFound();
        return View(new FormMaGiamGiaVM
        {
            MaMaGiamGia = m.MaMaGiamGia, MaCode = m.MaCode, MoTa = m.MoTa, LoaiGiam = m.LoaiGiam, GiaTriGiam = m.GiaTriGiam,
            GiamToiDa = m.GiamToiDa, DonHangToiThieu = m.DonHangToiThieu, SoLuotToiDa = m.SoLuotToiDa,
            NgayBatDau = m.NgayBatDau, NgayKetThuc = m.NgayKetThuc, DangHoatDong = m.DangHoatDong
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> FormMaGiamGia(FormMaGiamGiaVM vm)
    {
        vm.MaCode = (vm.MaCode ?? "").Trim().ToUpper();
        if (string.IsNullOrWhiteSpace(vm.MaCode) || vm.GiaTriGiam <= 0 ||
            (vm.LoaiGiam == HangSoLoaiGiam.PhanTram && vm.GiaTriGiam > 100))
        {
            TempData["CanhBao"] = "Nhập mã code và giá trị giảm hợp lệ (phần trăm không quá 100).";
            return RedirectToAction(nameof(FormMaGiamGia), new { ma = vm.MaMaGiamGia == 0 ? (int?)null : vm.MaMaGiamGia });
        }

        MaGiamGia ma;
        if (vm.MaMaGiamGia == 0)
        {
            ma = new MaGiamGia { MaCode = vm.MaCode };
            _csdl.MaGiamGia.Add(ma);
        }
        else
        {
            var tim = await _csdl.MaGiamGia.FirstOrDefaultAsync(x => x.MaMaGiamGia == vm.MaMaGiamGia);
            if (tim == null) return NotFound();
            ma = tim;
            ma.MaCode = vm.MaCode;
        }
        ma.MoTa = DichVuDatHang.LamSach(vm.MoTa, 200);
        ma.LoaiGiam = vm.LoaiGiam == HangSoLoaiGiam.PhanTram ? HangSoLoaiGiam.PhanTram : HangSoLoaiGiam.SoTien;
        ma.GiaTriGiam = vm.GiaTriGiam;
        ma.GiamToiDa = vm.GiamToiDa is > 0 ? vm.GiamToiDa : null;
        ma.DonHangToiThieu = Math.Max(0, vm.DonHangToiThieu);
        ma.SoLuotToiDa = vm.SoLuotToiDa is > 0 ? vm.SoLuotToiDa : null;
        ma.NgayBatDau = vm.NgayBatDau;
        ma.NgayKetThuc = vm.NgayKetThuc;
        ma.DangHoatDong = vm.DangHoatDong;

        try
        {
            await _csdl.SaveChangesAsync();
            _nhatKy.Ghi(HangSoNhatKy.SuaCaiDat, $"{(vm.MaMaGiamGia == 0 ? "Tạo" : "Sửa")} mã giảm giá \"{ma.MaCode}\".", MaNguoiDungHienTai);
            TempData["ThanhCong"] = "Đã lưu mã giảm giá.";
        }
        catch (DbUpdateException) { TempData["CanhBao"] = "Mã code này đã tồn tại."; }
        return RedirectToAction(nameof(MaGiamGia));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BatTatMaGiamGia(int ma)
    {
        var m = await _csdl.MaGiamGia.FindAsync(ma);
        if (m == null) return NotFound();
        m.DangHoatDong = !m.DangHoatDong;
        await _csdl.SaveChangesAsync();
        return RedirectToAction(nameof(MaGiamGia));
    }

    // ================= KHÁCH HÀNG THÂN THIẾT =================
    public async Task<IActionResult> KhachHang()
    {
        var soDonTheoKhach = await _csdl.DonHang.AsNoTracking()
            .Where(d => d.SoDienThoaiKhach != null && d.MaTrangThai != HangSoTrangThai.DaHuy)
            .GroupBy(d => d.SoDienThoaiKhach!)
            .Select(g => new { SoDienThoai = g.Key, SoDon = g.Count() })
            .ToDictionaryAsync(x => x.SoDienThoai, x => x.SoDon);

        var danhSach = await _csdl.KhachHang.AsNoTracking().OrderByDescending(k => k.TongChiTieu).Take(500)
            .Select(k => new KhachHangVM
            {
                SoDienThoai = k.SoDienThoai, HoTen = k.HoTen, DiemTichLuy = k.DiemTichLuy,
                TongChiTieu = k.TongChiTieu, LanCuoiMua = k.LanCuoiMua
            }).ToListAsync();
        foreach (var kh in danhSach) kh.SoDonDaMua = soDonTheoKhach.GetValueOrDefault(kh.SoDienThoai);

        return View(danhSach);
    }

    // ================= XUẤT EXCEL KHO =================
    public async Task<IActionResult> XuatExcelKho()
    {
        var danhSach = await _csdl.NguyenLieu.AsNoTracking().OrderBy(n => n.TenNguyenLieu).ToListAsync();
        var cot = new[] { "Tên nguyên liệu", "Đơn vị tính", "Tồn kho dùng hàng ngày", "Còn ~ (cốc)", "Đơn vị nhập", "Tồn kho tổng", "Mức tối thiểu kho tổng", "Trạng thái" };
        var dong = danhSach.Select(n => new object?[]
        {
            n.TenNguyenLieu, n.DonViTinh, n.SoLuongTon, n.SoCocConLai?.ToString("0") ?? "—",
            n.DonViNhap ?? "", n.SoLuongTonKhoTong, n.MucToiThieuKhoTong,
            !n.DangHoatDong ? "Ngừng dùng" : n.SapHetKhoDung || n.SapHetKhoTong ? "Sắp hết" : "Đủ dùng"
        });
        var file = _excel.Tao("Tồn kho", "Danh sách tồn kho nguyên liệu",
            new[] { $"Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}" }, cot, dong);
        return File(file, DichVuXuatExcel.LoaiTep, $"TonKho_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
    }
}
