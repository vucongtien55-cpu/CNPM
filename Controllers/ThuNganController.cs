using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MixueDatMon.DichVu;
using MixueDatMon.DuLieu;
using MixueDatMon.HubThoiGianThuc;
using MixueDatMon.MoHinh;
using MixueDatMon.MoHinh.VM;

namespace MixueDatMon.Controllers;

/// <summary>Thu ngân kiêm pha chế: nhận đơn -> làm -> giao -> thu tiền -> đối soát cuối ngày.</summary>
[Authorize(Roles = "ThuNgan,QuanLy")]
public class ThuNganController : Controller
{
    private const int SoPhutHienDonVuaXong = 10;   // đơn xong/hủy quá thời gian này chỉ còn trong Lịch sử

    private readonly DuLieuContext _csdl;
    private readonly IHubContext<DonHangHub> _hub;
    private readonly DichVuKho _dichVuKho;
    private readonly DichVuDatHang _dichVuDatHang;
    private readonly DichVuVietQr _vietQr;
    private readonly DichVuHuyDon _huyDon;
    private readonly DichVuNhatKy _nhatKy;
    private readonly DichVuKhuyenMai _khuyenMai;
    private readonly DichVuGiaVon _giaVon;
    private readonly DichVuXuatExcel _excel;
    public ThuNganController(DuLieuContext csdl, IHubContext<DonHangHub> hub, DichVuKho dichVuKho, DichVuDatHang dichVuDatHang,
        DichVuVietQr vietQr, DichVuHuyDon huyDon, DichVuNhatKy nhatKy, DichVuKhuyenMai khuyenMai, DichVuGiaVon giaVon, DichVuXuatExcel excel)
    {
        _csdl = csdl; _hub = hub; _dichVuKho = dichVuKho; _dichVuDatHang = dichVuDatHang; _vietQr = vietQr; _huyDon = huyDon; _nhatKy = nhatKy;
        _khuyenMai = khuyenMai; _giaVon = giaVon; _excel = excel;
    }

    private int MaNguoiDungHienTai => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ================= BẢNG ĐƠN (thời gian thực) =================
    public IActionResult Index() => View();

    /// <summary>Danh sách bàn đang hoạt động, dùng cho hộp "Chuyển / gộp bàn".</summary>
    [HttpGet]
    public async Task<IActionResult> DanhSachBan() =>
        Json(await _csdl.BanAn.AsNoTracking().Where(b => b.DangHoatDong).OrderBy(b => b.TenBan)
            .Select(b => new { maBan = b.MaBan, tenBan = b.TenBan }).ToListAsync());

    /// <summary>
    /// Dữ liệu cho bảng đơn và ma trận bàn: đơn đang xử lý, đơn vừa xong / vừa hủy trong vài phút gần nhất,
    /// và đơn của bàn đã giao nhưng chưa thu tiền (để hiện bàn còn nợ). Đơn cũ chỉ xem ở Lịch sử.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DuLieuBang()
    {
        var homNay = DateTime.Today;
        var mocVuaXong = DateTime.Now.AddMinutes(-SoPhutHienDonVuaXong);
        var danhSachDon = await _csdl.DonHang.AsNoTracking().AsSplitQuery()
            .Include(d => d.BanAn)
            .Include(d => d.DanhSachChiTiet).ThenInclude(c => c.MonAn)
            .Include(d => d.DanhSachChiTiet).ThenInclude(c => c.MucDa)
            .Include(d => d.DanhSachChiTiet).ThenInclude(c => c.MucDuong)
            .Include(d => d.DanhSachChiTiet).ThenInclude(c => c.KichCo)
            .Include(d => d.DanhSachChiTiet).ThenInclude(c => c.DanhSachTopping).ThenInclude(t => t.Topping)
            .Where(d => d.MaTrangThai <= HangSoTrangThai.SanSang
                     || (d.MaTrangThai == HangSoTrangThai.HoanThanh && d.NgayThanhToan == null && d.MaBan != null && d.NgayTao >= homNay)
                     || (d.NgayCapNhat ?? d.NgayTao) >= mocVuaXong)
            .OrderBy(d => d.NgayTao).Take(200)
            .ToListAsync();

        var duLieu = danhSachDon.Select(d => new
        {
            maDonHang = d.MaDonHang,
            maTheoDoi = d.MaTheoDoi,
            noiPhucVu = d.NoiPhucVu,
            maBan = d.MaBan,
            mangDi = d.MaBan == null,
            maTrangThai = d.MaTrangThai,
            vuaXong = d.MaTrangThai >= HangSoTrangThai.HoanThanh && (d.NgayCapNhat ?? d.NgayTao) >= mocVuaXong,
            ngayCapNhat = d.NgayCapNhat,
            tenKhach = d.TenKhach,
            ghiChu = d.GhiChuKhach,
            ngayTao = d.NgayTao,
            daThanhToan = d.NgayThanhToan != null,
            tongTien = d.TongTien,
            danhSachMon = d.DanhSachChiTiet.Select(c => new
            {
                maChiTiet = c.MaChiTiet,
                tenMon = c.MonAn.TenMon,
                soLuong = c.SoLuong,
                donGia = c.ThanhTien,
                mucDa = c.MucDa?.TenMuc,
                mucDuong = c.MucDuong?.TenMuc,
                danhSachTopping = c.DanhSachTopping.Select(t => t.Topping.TenTopping).ToList(),
                ghiChu = c.GhiChu
            })
        });
        return Json(duLieu);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatTrangThai([FromBody] YeuCauDoiTrangThai yeuCau)
    {
        var donHang = await _csdl.DonHang.FirstOrDefaultAsync(d => d.MaDonHang == yeuCau.MaDonHang);
        if (donHang == null) return NotFound(new { thanhCong = false, thongBao = "Không tìm thấy đơn." });

        if (yeuCau.MaTrangThai == HangSoTrangThai.DaHuy)
            return BadRequest(new { thanhCong = false, thongBao = "Hãy dùng nút \"Hủy đơn\" để chọn lý do (và hoàn tiền / hoàn kho nếu cần)." });

        int[] choPhep = donHang.MaTrangThai switch
        {
            HangSoTrangThai.ChoXacNhan => new[] { HangSoTrangThai.DangLam },
            HangSoTrangThai.DangLam => new[] { HangSoTrangThai.SanSang },
            HangSoTrangThai.SanSang => new[] { HangSoTrangThai.HoanThanh },
            _ => Array.Empty<int>()
        };
        if (!choPhep.Contains(yeuCau.MaTrangThai))
            return BadRequest(new { thanhCong = false, thongBao = "Đơn đã được cập nhật ở máy khác. Đang tải lại danh sách." });

        // Đơn mang đi: khách không ngồi bàn nên phải thanh toán TRƯỚC khi quán pha chế
        if (yeuCau.MaTrangThai == HangSoTrangThai.DangLam && donHang.MaBan == null && donHang.NgayThanhToan == null)
            return BadRequest(new { thanhCong = false, thongBao = "Đơn mang đi phải thu tiền trước khi làm. Hãy bấm Thu tiền mặt hoặc Thu chuyển khoản." });

        await using var giaoDich = await _csdl.Database.BeginTransactionAsync();

        if (yeuCau.MaTrangThai == HangSoTrangThai.DangLam)
        {
            donHang.MaNguoiXuLy = MaNguoiDungHienTai;
            await _dichVuKho.TruKhoTheoDonAsync(donHang.MaDonHang, MaNguoiDungHienTai);   // trừ kho khi bắt đầu làm
        }
        donHang.MaTrangThai = yeuCau.MaTrangThai;
        donHang.NgayCapNhat = DateTime.Now;
        await _csdl.SaveChangesAsync();
        await giaoDich.CommitAsync();

        await PhatThongBaoAsync(donHang);
        return Ok(new { thanhCong = true });
    }

    /// <summary>Thu tiền từ bảng đơn (gọi bằng JavaScript).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DanhDauDaThanhToan([FromBody] YeuCauThanhToan yeuCau)
    {
        var (thanhCong, thongBao) = await ThuTienAsync(yeuCau.MaDonHang, yeuCau.MaPhuongThuc);
        return thanhCong ? Ok(new { thanhCong = true }) : BadRequest(new { thanhCong = false, thongBao });
    }

    /// <summary>Thu tiền từ trang Lịch sử / Đối soát (gửi form thường, xong quay lại đúng trang đó).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ThuTien(int ma, int maPhuongThuc, string? quayLai)
    {
        var (thanhCong, thongBao) = await ThuTienAsync(ma, maPhuongThuc);
        if (thanhCong) TempData["ThanhCong"] = "Đã ghi nhận thu tiền.";
        else TempData["CanhBao"] = thongBao;
        if (!string.IsNullOrEmpty(quayLai) && Url.IsLocalUrl(quayLai)) return LocalRedirect(quayLai);
        return RedirectToAction(nameof(LichSu));
    }

    // ================= THU CHUYỂN KHOẢN BẰNG VIETQR =================
    /// <summary>Thông tin hiển thị trong hộp "Thu chuyển khoản": số tiền, nội dung, tài khoản nhận và địa chỉ ảnh QR.</summary>
    [HttpGet]
    public async Task<IActionResult> ThongTinQr(int? maDonHang, int? maBan)
    {
        var thongTin = await LayThongTinQrAsync(maDonHang, maBan);
        if (thongTin == null) return NotFound(new { thanhCong = false, thongBao = "Không còn khoản nào cần thu (đơn đã thanh toán hoặc đã hủy)." });

        return Json(new
        {
            thanhCong = true,
            daCauHinh = _vietQr.DaCauHinh,
            tieuDe = thongTin.Value.TieuDe,
            soTien = thongTin.Value.SoTien,
            noiDung = thongTin.Value.NoiDung,
            tenNganHang = _vietQr.TenNganHang,
            soTaiKhoan = _vietQr.SoTaiKhoan,
            tenChuTaiKhoan = _vietQr.TenChuTaiKhoan,
            duongDanAnh = _vietQr.DaCauHinh ? Url.Action(nameof(AnhQr), "ThuNgan", new { maDonHang, maBan, t = DateTime.Now.Ticks }) : null
        });
    }

    [HttpGet]
    public async Task<IActionResult> AnhQr(int? maDonHang, int? maBan)
    {
        if (!_vietQr.DaCauHinh) return NotFound();
        var thongTin = await LayThongTinQrAsync(maDonHang, maBan);
        if (thongTin == null) return NotFound();

        Response.Headers.CacheControl = "no-store";
        return File(_vietQr.TaoAnhPng(_vietQr.TaoChuoiVietQr(thongTin.Value.SoTien, thongTin.Value.NoiDung)), "image/png");
    }

    /// <summary>Số tiền và nội dung cho mã QR: một đơn, hoặc tổng các đơn chưa thu của một bàn trong hôm nay.</summary>
    private async Task<(decimal SoTien, string NoiDung, string TieuDe)?> LayThongTinQrAsync(int? maDonHang, int? maBan)
    {
        if (maDonHang != null)
        {
            var don = await (from d in _csdl.DonHang
                             join t in _csdl.TongTienDon on d.MaDonHang equals t.MaDonHang
                             where d.MaDonHang == maDonHang && d.NgayThanhToan == null && d.MaTrangThai != HangSoTrangThai.DaHuy
                             select new { d.MaTheoDoi, TenBan = d.BanAn != null ? d.BanAn.TenBan : "Mang đi", t.TongTien }).FirstOrDefaultAsync();
            if (don == null || don.TongTien <= 0) return null;
            return (don.TongTien, _vietQr.NoiDungChoDon(don.MaTheoDoi), $"Đơn {don.MaTheoDoi} ({don.TenBan})");
        }
        if (maBan != null)
        {
            var homNay = DateTime.Today;
            var ban = await _csdl.BanAn.AsNoTracking().FirstOrDefaultAsync(b => b.MaBan == maBan);
            if (ban == null) return null;
            var tong = await (from d in _csdl.DonHang
                              join t in _csdl.TongTienDon on d.MaDonHang equals t.MaDonHang
                              where d.MaBan == maBan && d.NgayThanhToan == null && d.MaTrangThai != HangSoTrangThai.DaHuy && d.NgayTao >= homNay
                              select t.TongTien).SumAsync();
            if (tong <= 0) return null;
            return (tong, _vietQr.NoiDungChoBan(ban.TenBan), $"Thu cả {ban.TenBan}");
        }
        return null;
    }

    /// <summary>Thu tiền một lượt cho tất cả đơn chưa thanh toán của một bàn (khách ngồi bàn gọi nhiều lần, trả một lần).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ThuTienCaBan([FromBody] YeuCauThuCaBan yeuCau)
    {
        if (!await _csdl.PhuongThucThanhToan.AnyAsync(p => p.MaPhuongThuc == yeuCau.MaPhuongThuc))
            return BadRequest(new { thanhCong = false, thongBao = "Phương thức thanh toán không hợp lệ." });

        var homNay = DateTime.Today;
        var danhSachDon = await _csdl.DonHang
            .Where(d => d.MaBan == yeuCau.MaBan && d.NgayThanhToan == null && d.MaTrangThai != HangSoTrangThai.DaHuy && d.NgayTao >= homNay)
            .Include(d => d.DanhSachChiTiet).ThenInclude(c => c.DanhSachTopping)
            .ToListAsync();
        if (danhSachDon.Count == 0)
            return BadRequest(new { thanhCong = false, thongBao = "Bàn này không còn đơn nào chưa thu tiền." });

        var bayGio = DateTime.Now;
        foreach (var don in danhSachDon)
        {
            don.MaPhuongThuc = yeuCau.MaPhuongThuc;
            don.NgayThanhToan = bayGio;
            don.MaNguoiThuTien = MaNguoiDungHienTai;
            if (don.MaTrangThai < HangSoTrangThai.HoanThanh) don.NgayCapNhat = bayGio;
        }
        await _csdl.SaveChangesAsync();
        foreach (var don in danhSachDon) { await _khuyenMai.GhiNhanThanhToanAsync(don); await PhatThongBaoAsync(don); }

        return Ok(new { thanhCong = true, soDon = danhSachDon.Count, tongTien = danhSachDon.Sum(d => d.TongTien) });
    }

    // ================= TẠO ĐƠN TẠI QUẦY =================
    /// <summary>Màn hình gọi món cho khách không dùng điện thoại: hiện toàn bộ món của quán.</summary>
    [HttpGet]
    public async Task<IActionResult> TaoDon()
    {
        var (monHet, toppingHet) = await _dichVuKho.LayHetNguyenLieuAsync();   // chỉ để cảnh báo, thu ngân vẫn bán được nếu cần
        var vm = new TaoDonVM
        {
            DanhSachNhom = await _csdl.NhomMon.AsNoTracking().Where(n => n.DangHoatDong).OrderBy(n => n.ThuTuHienThi)
                .Include(n => n.DanhSachMon.Where(m => m.DangBan).OrderByDescending(m => m.LaBanChay).ThenBy(m => m.TenMon))
                .ToListAsync(),
            DanhSachTopping = await _csdl.Topping.AsNoTracking().Where(t => t.DangBan).OrderBy(t => t.MaTopping).ToListAsync(),
            DanhSachMucDa = await _csdl.MucDa.AsNoTracking().OrderBy(x => x.MaMucDa).ToListAsync(),
            DanhSachMucDuong = await _csdl.MucDuong.AsNoTracking().OrderBy(x => x.MaMucDuong).ToListAsync(),
            DanhSachBan = await _csdl.BanAn.AsNoTracking().Where(b => b.DangHoatDong).OrderBy(b => b.MaBan).ToListAsync(),
            MonHetNguyenLieu = monHet, ToppingHetNguyenLieu = toppingHet,
            GiaTheoSize = (await _csdl.MonAnKichCo.AsNoTracking().Where(k => k.Gia > 0)
                .OrderBy(k => k.KichCo.ThuTu)
                .Select(k => new { k.MaMonAn, Size = new KichCoGiaVM { MaKichCo = k.MaKichCo, TenKichCo = k.KichCo.TenKichCo, Gia = k.Gia } })
                .ToListAsync())
                .GroupBy(x => x.MaMonAn).ToDictionary(g => g.Key, g => g.Select(x => x.Size).ToList())
        };
        return View(vm);
    }

    /// <summary>
    /// Thu ngân tạo đơn cho khách tại quầy. Đơn vào thẳng trạng thái Đang làm (trừ kho ngay).
    /// Khách mang đi BẮT BUỘC thu tiền ngay; khách ngồi bàn được phép thu sau.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoDonTaiQuay([FromBody] YeuCauTaoDonTaiQuay yeuCau)
    {
        if (yeuCau == null) return BadRequest(new { thanhCong = false, thongBao = "Yêu cầu không hợp lệ." });

        int? maBan = null;
        string noiPhucVu = "Mang đi";
        if (yeuCau.MaBan != null)
        {
            var ban = await _csdl.BanAn.AsNoTracking().FirstOrDefaultAsync(b => b.MaBan == yeuCau.MaBan && b.DangHoatDong);
            if (ban == null) return BadRequest(new { thanhCong = false, thongBao = "Bàn không hợp lệ hoặc đã ngừng dùng." });
            maBan = ban.MaBan;
            noiPhucVu = ban.TenBan;
        }

        if (maBan == null && yeuCau.MaPhuongThuc == null)
            return BadRequest(new { thanhCong = false, thongBao = "Khách mang đi phải thanh toán ngay. Hãy chọn Tiền mặt hoặc Chuyển khoản." });
        if (yeuCau.MaPhuongThuc != null && !await _csdl.PhuongThucThanhToan.AnyAsync(p => p.MaPhuongThuc == yeuCau.MaPhuongThuc))
            return BadRequest(new { thanhCong = false, thongBao = "Phương thức thanh toán không hợp lệ." });

        var (donHang, loi) = await _dichVuDatHang.DungDonAsync(
            new YeuCauDatHang { TenKhach = yeuCau.TenKhach, GhiChu = yeuCau.GhiChu, SoDienThoai = yeuCau.SoDienThoai, MaGiamGia = yeuCau.MaGiamGia, DanhSachMon = yeuCau.DanhSachMon }, maBan);
        if (donHang == null) return BadRequest(new { thanhCong = false, thongBao = loi });

        var maNguoiDung = MaNguoiDungHienTai;
        var bayGio = DateTime.Now;

        // Chuyển khoản: chưa được coi là đã thu. Đơn giữ ở "Chờ xác nhận"; thu ngân cho khách quét mã VietQR,
        // kiểm tra tiền đã vào tài khoản rồi mới xác nhận (khi đó đơn mới chuyển sang Đang làm).
        if (yeuCau.MaPhuongThuc == HangSoPhuongThuc.ChuyenKhoan)
        {
            _csdl.DonHang.Add(donHang);
            await _csdl.SaveChangesAsync();
            await _khuyenMai.GiuLuotAsync(donHang);
            await _hub.Clients.Group("nhanvien").SendAsync("DonThayDoi");
            return Ok(new { thanhCong = true, canChuyenKhoan = true, maDonHang = donHang.MaDonHang, maTheoDoi = donHang.MaTheoDoi, noiPhucVu, tongTien = donHang.TongTien, daThu = false });
        }

        donHang.MaTrangThai = HangSoTrangThai.DangLam;
        donHang.MaNguoiXuLy = maNguoiDung;
        donHang.NgayCapNhat = bayGio;
        if (yeuCau.MaPhuongThuc is int maPhuongThuc)
        {
            donHang.MaPhuongThuc = maPhuongThuc;
            donHang.NgayThanhToan = bayGio;
            donHang.MaNguoiThuTien = maNguoiDung;
        }

        await using var giaoDich = await _csdl.Database.BeginTransactionAsync();
        _csdl.DonHang.Add(donHang);
        await _csdl.SaveChangesAsync();                                       // lấy MaDonHang
        await _dichVuKho.TruKhoTheoDonAsync(donHang.MaDonHang, maNguoiDung);  // trừ kho như khi bấm "Nhận đơn và làm"
        await _csdl.SaveChangesAsync();
        await giaoDich.CommitAsync();

        await _khuyenMai.GiuLuotAsync(donHang);
        if (yeuCau.MaPhuongThuc != null) await _khuyenMai.GhiNhanThanhToanAsync(donHang);
        await _hub.Clients.Group("nhanvien").SendAsync("DonThayDoi");
        return Ok(new { thanhCong = true, canChuyenKhoan = false, maDonHang = donHang.MaDonHang, maTheoDoi = donHang.MaTheoDoi, noiPhucVu, tongTien = donHang.TongTien, daThu = yeuCau.MaPhuongThuc != null });
    }

    /// <summary>Hủy đơn có lý do; nếu đã thu tiền thì đánh dấu hoàn tiền; tùy chọn hoàn nguyên liệu vào kho.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> HuyDon([FromBody] YeuCauHuyDon yeuCau)
    {
        var laQuanLy = User.IsInRole(HangSoVaiTro.QuanLy);
        var (thanhCong, thongBao) = await _huyDon.HuyAsync(
            yeuCau.MaDonHang, yeuCau.LyDo, yeuCau.GhiChu, yeuCau.HoanKho, yeuCau.DaHoanTien, MaNguoiDungHienTai, laQuanLy);
        return thanhCong ? Ok(new { thanhCong = true, thongBao }) : BadRequest(new { thanhCong = false, thongBao });
    }

    public IActionResult DanhSachLyDoHuy() => Json(DichVuHuyDon.DanhSachLyDo);

    // ================= CHUYỂN BÀN / GỘP BÀN / TÁCH BILL =================
    /// <summary>Chuyển toàn bộ đơn CHƯA THANH TOÁN từ bàn này sang bàn khác (khách đổi chỗ, hoặc gộp 2 bàn làm 1).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChuyenBan([FromBody] YeuCauChuyenBan yeuCau)
    {
        if (yeuCau.TuMaBan == yeuCau.DenMaBan) return BadRequest(new { thanhCong = false, thongBao = "Bàn đi và bàn đến phải khác nhau." });
        var banDen = await _csdl.BanAn.AsNoTracking().FirstOrDefaultAsync(b => b.MaBan == yeuCau.DenMaBan && b.DangHoatDong);
        if (banDen == null) return BadRequest(new { thanhCong = false, thongBao = "Bàn đến không hợp lệ." });

        var homNay = DateTime.Today;
        var danhSachDon = await _csdl.DonHang
            .Where(d => d.MaBan == yeuCau.TuMaBan && d.NgayThanhToan == null && d.MaTrangThai != HangSoTrangThai.DaHuy && d.NgayTao >= homNay)
            .ToListAsync();
        if (danhSachDon.Count == 0) return BadRequest(new { thanhCong = false, thongBao = "Bàn đi không có đơn nào để chuyển." });

        var banDi = await _csdl.BanAn.AsNoTracking().FirstOrDefaultAsync(b => b.MaBan == yeuCau.TuMaBan);
        foreach (var don in danhSachDon) don.MaBan = yeuCau.DenMaBan;
        _nhatKy.Ghi(HangSoNhatKy.ChuyenBan, $"Chuyển {danhSachDon.Count} đơn từ {banDi?.TenBan} sang {banDen.TenBan}.", MaNguoiDungHienTai);
        await _csdl.SaveChangesAsync();
        await _hub.Clients.Group("nhanvien").SendAsync("DonThayDoi");
        return Ok(new { thanhCong = true, soDon = danhSachDon.Count });
    }

    /// <summary>Tách một số món trong đơn ra thành đơn mới cùng bàn (chưa thanh toán) để khách trả tiền riêng.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TachDon([FromBody] YeuCauTachDon yeuCau)
    {
        var donGoc = await _csdl.DonHang.Include(d => d.DanhSachChiTiet).ThenInclude(c => c.DanhSachTopping)
            .FirstOrDefaultAsync(d => d.MaDonHang == yeuCau.MaDonHang);
        if (donGoc == null) return NotFound(new { thanhCong = false, thongBao = "Không tìm thấy đơn." });
        if (donGoc.NgayThanhToan != null) return BadRequest(new { thanhCong = false, thongBao = "Đơn đã thanh toán, không thể tách." });
        if (donGoc.MaTrangThai == HangSoTrangThai.DaHuy) return BadRequest(new { thanhCong = false, thongBao = "Đơn đã hủy." });

        var dongTach = donGoc.DanhSachChiTiet.Where(c => yeuCau.DanhSachMaChiTiet.Contains(c.MaChiTiet)).ToList();
        if (dongTach.Count == 0) return BadRequest(new { thanhCong = false, thongBao = "Chưa chọn món nào để tách." });
        if (dongTach.Count == donGoc.DanhSachChiTiet.Count) return BadRequest(new { thanhCong = false, thongBao = "Không thể tách hết toàn bộ món, đơn gốc sẽ trống." });

        var donMoi = new DonHang
        {
            MaTheoDoi = await _dichVuDatHang.TaoMaTheoDoiAsync(), MaBan = donGoc.MaBan, MaTrangThai = donGoc.MaTrangThai,
            MaNguoiXuLy = donGoc.MaNguoiXuLy, NgayTao = DateTime.Now, GhiChuKhach = "Tách từ đơn " + donGoc.MaTheoDoi
        };
        foreach (var dong in dongTach)
        {
            donGoc.DanhSachChiTiet.Remove(dong);
            donMoi.DanhSachChiTiet.Add(dong);
        }
        _csdl.DonHang.Add(donMoi);
        _nhatKy.Ghi(HangSoNhatKy.TachDon, $"Tách {dongTach.Count} món từ đơn {donGoc.MaTheoDoi} sang đơn mới {donMoi.MaTheoDoi}.", MaNguoiDungHienTai, donGoc.MaDonHang);
        await _csdl.SaveChangesAsync();
        await _hub.Clients.Group("nhanvien").SendAsync("DonThayDoi");
        return Ok(new { thanhCong = true, maTheoDoiMoi = donMoi.MaTheoDoi });
    }

    // ================= YÊU CẦU TỪ BÀN (gọi nhân viên / xin thanh toán) =================
    [HttpGet]
    public async Task<IActionResult> DuLieuYeuCau() =>
        Json(await _csdl.YeuCauTaiBan.AsNoTracking().Include(y => y.BanAn).Where(y => !y.DaXuLy).OrderBy(y => y.NgayTao)
            .Select(y => new { y.MaYeuCau, tenBan = y.BanAn.TenBan, loaiYeuCau = y.LoaiYeuCau, tenLoai = HangSoYeuCau.Ten(y.LoaiYeuCau), y.NgayTao })
            .ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> XuLyYeuCau([FromBody] YeuCauXuLy yeuCau)
    {
        var y = await _csdl.YeuCauTaiBan.FindAsync(yeuCau.Ma);
        if (y == null) return NotFound(new { thanhCong = false });
        y.DaXuLy = true; y.NgayXuLy = DateTime.Now; y.MaNguoiXuLy = MaNguoiDungHienTai;
        await _csdl.SaveChangesAsync();
        await _hub.Clients.Group("nhanvien").SendAsync("YeuCauThayDoi");
        return Ok(new { thanhCong = true });
    }

    // ================= IN PHIẾU =================
    /// <summary>Hóa đơn khổ giấy in nhiệt 80mm để đưa khách hoặc phiếu pha chế cho quầy.</summary>
    public async Task<IActionResult> InPhieu(int ma)
    {
        var don = await _csdl.DonHang.AsNoTracking().AsSplitQuery()
            .Include(d => d.BanAn)
            .Include(d => d.DanhSachChiTiet).ThenInclude(c => c.MonAn)
            .Include(d => d.DanhSachChiTiet).ThenInclude(c => c.MucDa)
            .Include(d => d.DanhSachChiTiet).ThenInclude(c => c.MucDuong)
            .Include(d => d.DanhSachChiTiet).ThenInclude(c => c.KichCo)
            .Include(d => d.DanhSachChiTiet).ThenInclude(c => c.DanhSachTopping).ThenInclude(t => t.Topping)
            .FirstOrDefaultAsync(d => d.MaDonHang == ma);
        if (don == null) return NotFound();
        return View(don);
    }

    // ================= LỊCH SỬ ĐƠN =================
    /// <summary>Tra cứu đơn của mọi ngày (mặc định là hôm nay). Đơn cũ đã rời khỏi bảng đơn nằm ở đây.</summary>
    public async Task<IActionResult> LichSu(DateTime? tuNgay, DateTime? denNgay, int? trangThai, string? thanhToan, string? tuKhoa)
    {
        var tu = (tuNgay ?? denNgay ?? DateTime.Today).Date;
        var den = (denNgay ?? tuNgay ?? DateTime.Today).Date;
        if (den < tu) (tu, den) = (den, tu);
        var hetNgayDen = den.AddDays(1);

        var truyVan = _csdl.DonHang.AsNoTracking().Where(d => d.NgayTao >= tu && d.NgayTao < hetNgayDen);
        if (trangThai != null) truyVan = truyVan.Where(d => d.MaTrangThai == trangThai);
        if (thanhToan == "da") truyVan = truyVan.Where(d => d.NgayThanhToan != null);
        else if (thanhToan == "chua") truyVan = truyVan.Where(d => d.NgayThanhToan == null && d.MaTrangThai != HangSoTrangThai.DaHuy);
        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var khoa = tuKhoa.Trim();
            truyVan = truyVan.Where(d => d.MaTheoDoi.Contains(khoa) || (d.TenKhach != null && d.TenKhach.Contains(khoa)));
        }

        var danhSachDon = await truyVan.AsSplitQuery()
            .Include(d => d.BanAn).Include(d => d.NguoiXuLy).Include(d => d.NguoiThuTien)
            .Include(d => d.DanhSachChiTiet).ThenInclude(c => c.MonAn)
            .Include(d => d.DanhSachChiTiet).ThenInclude(c => c.DanhSachTopping).ThenInclude(t => t.Topping)
            .OrderByDescending(d => d.NgayTao).Take(301)
            .ToListAsync();

        var daCatBot = danhSachDon.Count > 300;
        if (daCatBot) danhSachDon = danhSachDon.Take(300).ToList();

        var danhSachDong = danhSachDon.Select(d => new DongLichSuVM
        {
            MaDonHang = d.MaDonHang, MaTheoDoi = d.MaTheoDoi, NoiPhucVu = d.NoiPhucVu, TenKhach = d.TenKhach,
            MoTaMon = string.Join("; ", d.DanhSachChiTiet.Select(c =>
                $"{c.SoLuong}x {c.MonAn.TenMon}" + (c.DanhSachTopping.Count > 0 ? " (+ " + string.Join(", ", c.DanhSachTopping.Select(t => t.Topping.TenTopping)) + ")" : ""))),
            MaTrangThai = d.MaTrangThai, TongTien = d.TongTien, NgayTao = d.NgayTao, NgayThanhToan = d.NgayThanhToan,
            MaPhuongThuc = d.MaPhuongThuc, NguoiXuLy = d.NguoiXuLy?.HoTen, NguoiThuTien = d.NguoiThuTien?.HoTen
        }).ToList();

        return View(new LichSuThuNganVM
        {
            DanhSachDong = danhSachDong, TuNgay = tu, DenNgay = den, TrangThai = trangThai, ThanhToan = thanhToan, TuKhoa = tuKhoa,
            TongDaThu = danhSachDong.Where(d => d.NgayThanhToan != null && d.MaTrangThai != HangSoTrangThai.DaHuy).Sum(d => d.TongTien),
            SoDonChuaThu = danhSachDong.Count(d => d.NgayThanhToan == null && d.MaTrangThai != HangSoTrangThai.DaHuy),
            DaCatBot = daCatBot
        });
    }

    // ================= ĐỐI SOÁT CUỐI NGÀY =================
    /// <summary>
    /// Doanh thu theo ngày để kiểm đếm tiền. Tính theo NGÀY THU TIỀN (khớp với tiền thực tế trong két / tài khoản).
    /// </summary>
    public async Task<IActionResult> DoiSoat(DateTime? ngay) => View(await DungDoiSoatAsync(ngay));

    public async Task<IActionResult> XuatExcelDoiSoat(DateTime? ngay)
    {
        var vm = await DungDoiSoatAsync(ngay);
        var cot = new[] { "Mã đơn", "Nơi phục vụ", "Phương thức", "Giờ thu", "Người thu", "Số tiền" };
        var dong = vm.DanhSachThu.Select(d => new object?[]
        {
            d.MaTheoDoi, d.NoiPhucVu, HangSoPhuongThuc.Ten(d.MaPhuongThuc), d.NgayThanhToan, d.NguoiThu ?? "-", d.SoTien
        });
        var thongTinDau = new[]
        {
            $"Ngày đối soát: {vm.Ngay:dd/MM/yyyy}",
            $"Tiền mặt: {vm.TienMat:N0}đ ({vm.SoDonTienMat} đơn)",
            $"Chuyển khoản: {vm.ChuyenKhoan:N0}đ ({vm.SoDonChuyenKhoan} đơn)",
            $"Chưa thu: {vm.TienChuaThu:N0}đ",
            $"Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}"
        };
        var file = _excel.Tao("Đối soát", "Đối soát cuối ngày", thongTinDau, cot, dong, cotTien: new[] { 5 });
        return File(file, DichVuXuatExcel.LoaiTep, $"DoiSoat_{vm.Ngay:yyyyMMdd}.xlsx");
    }

    private async Task<DoiSoatVM> DungDoiSoatAsync(DateTime? ngay)
    {
        var ngayChon = (ngay ?? DateTime.Today).Date;
        if (ngayChon > DateTime.Today) ngayChon = DateTime.Today;
        var tu = ngayChon;
        var den = ngayChon.AddDays(1);

        // Các khoản đã thu trong ngày (không tính đơn hủy)
        var danhSachThu = await (from d in _csdl.DonHang
                                 join t in _csdl.TongTienDon on d.MaDonHang equals t.MaDonHang
                                 where d.NgayThanhToan != null && d.NgayThanhToan >= tu && d.NgayThanhToan < den
                                       && d.MaTrangThai != HangSoTrangThai.DaHuy
                                 orderby d.NgayThanhToan
                                 select new DongThuTienVM
                                 {
                                     MaDonHang = d.MaDonHang, MaTheoDoi = d.MaTheoDoi,
                                     NoiPhucVu = d.BanAn != null ? d.BanAn.TenBan : "Mang đi",
                                     MaPhuongThuc = d.MaPhuongThuc ?? 0, NgayThanhToan = d.NgayThanhToan!.Value, SoTien = t.TongTien,
                                     NguoiThu = d.NguoiThuTien != null ? d.NguoiThuTien.HoTen : null
                                 }).ToListAsync();

        // Đơn tạo trong ngày nhưng chưa thu tiền (nguy cơ thất thoát)
        var danhSachChuaThu = await (from d in _csdl.DonHang
                                     join t in _csdl.TongTienDon on d.MaDonHang equals t.MaDonHang
                                     where d.NgayThanhToan == null && d.MaTrangThai != HangSoTrangThai.DaHuy
                                           && d.NgayTao >= tu && d.NgayTao < den
                                     orderby d.NgayTao
                                     select new DongChuaThuVM
                                     {
                                         MaDonHang = d.MaDonHang, MaTheoDoi = d.MaTheoDoi,
                                         NoiPhucVu = d.BanAn != null ? d.BanAn.TenBan : "Mang đi",
                                         MaTrangThai = d.MaTrangThai, NgayTao = d.NgayTao, SoTien = t.TongTien
                                     }).ToListAsync();

        // 7 ngày kết thúc ở ngày đang chọn
        var dauBayNgay = ngayChon.AddDays(-6);
        var bayNgayDuLieu = await (from d in _csdl.DonHang
                                   join t in _csdl.TongTienDon on d.MaDonHang equals t.MaDonHang
                                   where d.NgayThanhToan != null && d.NgayThanhToan >= dauBayNgay && d.NgayThanhToan < den
                                         && d.MaTrangThai != HangSoTrangThai.DaHuy
                                   select new { NgayThanhToan = d.NgayThanhToan!.Value, MaPhuongThuc = d.MaPhuongThuc ?? 0, t.TongTien }).ToListAsync();
        var bayNgay = Enumerable.Range(0, 7).Select(i =>
        {
            var ngayI = dauBayNgay.AddDays(i);
            var dongNgay = bayNgayDuLieu.Where(x => x.NgayThanhToan.Date == ngayI).ToList();
            return new DongNgayVM
            {
                Ngay = ngayI, SoDon = dongNgay.Count,
                TienMat = dongNgay.Where(x => x.MaPhuongThuc == HangSoPhuongThuc.TienMat).Sum(x => x.TongTien),
                ChuyenKhoan = dongNgay.Where(x => x.MaPhuongThuc == HangSoPhuongThuc.ChuyenKhoan).Sum(x => x.TongTien)
            };
        }).Reverse().ToList();

        var tienMat = danhSachThu.Where(d => d.MaPhuongThuc == HangSoPhuongThuc.TienMat).ToList();
        var chuyenKhoan = danhSachThu.Where(d => d.MaPhuongThuc == HangSoPhuongThuc.ChuyenKhoan).ToList();

        return new DoiSoatVM
        {
            Ngay = ngayChon,
            TienMat = tienMat.Sum(d => d.SoTien), SoDonTienMat = tienMat.Count,
            ChuyenKhoan = chuyenKhoan.Sum(d => d.SoTien), SoDonChuyenKhoan = chuyenKhoan.Count,
            TienChuaThu = danhSachChuaThu.Sum(d => d.SoTien),
            SoDonHuy = await _csdl.DonHang.CountAsync(d => d.MaTrangThai == HangSoTrangThai.DaHuy && d.NgayTao >= tu && d.NgayTao < den),
            DanhSachThu = danhSachThu, DanhSachChuaThu = danhSachChuaThu, BayNgay = bayNgay,
            DaLuu = await _csdl.DoiSoatCuoiNgay.AsNoTracking().Include(x => x.NguoiThucHien)
                .Where(x => x.NgayDoiSoat == ngayChon).OrderByDescending(x => x.NgayTao).ToListAsync()
        };
    }

    /// <summary>Lưu kết quả kiểm đếm. Số tiền mặt của hệ thống do server tự tính lại, không nhận từ trình duyệt.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LuuDoiSoat(DateTime ngay, decimal tienDauCa, decimal tienMatThucDem, string? ghiChu)
    {
        var ngayChon = ngay.Date;
        if (ngayChon > DateTime.Today || tienDauCa < 0 || tienMatThucDem < 0)
        {
            TempData["CanhBao"] = "Ngày hoặc số tiền không hợp lệ.";
            return RedirectToAction(nameof(DoiSoat), new { ngay = ngayChon.ToString("yyyy-MM-dd") });
        }

        var tu = ngayChon;
        var den = ngayChon.AddDays(1);
        var tienMatHeThong = await (from d in _csdl.DonHang
                                    join t in _csdl.TongTienDon on d.MaDonHang equals t.MaDonHang
                                    where d.NgayThanhToan != null && d.NgayThanhToan >= tu && d.NgayThanhToan < den
                                          && d.MaPhuongThuc == HangSoPhuongThuc.TienMat && d.MaTrangThai != HangSoTrangThai.DaHuy
                                    select t.TongTien).SumAsync();

        _csdl.DoiSoatCuoiNgay.Add(new DoiSoatCuoiNgay
        {
            NgayDoiSoat = ngayChon, MaNguoiThucHien = MaNguoiDungHienTai, TienDauCa = tienDauCa,
            TienMatHeThong = tienMatHeThong, TienMatThucDem = tienMatThucDem,
            GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : (ghiChu.Trim().Length > 500 ? ghiChu.Trim()[..500] : ghiChu.Trim()),
            NgayTao = DateTime.Now
        });
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = "Đã lưu kết quả đối soát.";
        return RedirectToAction(nameof(DoiSoat), new { ngay = ngayChon.ToString("yyyy-MM-dd") });
    }

    // ================= HÀM DÙNG CHUNG =================
    private async Task<(bool ThanhCong, string ThongBao)> ThuTienAsync(int maDonHang, int maPhuongThuc)
    {
        var donHang = await _csdl.DonHang.Include(d => d.DanhSachChiTiet).ThenInclude(c => c.DanhSachTopping)
            .FirstOrDefaultAsync(d => d.MaDonHang == maDonHang);
        if (donHang == null) return (false, "Không tìm thấy đơn.");
        if (donHang.MaTrangThai == HangSoTrangThai.DaHuy || donHang.NgayThanhToan != null)
            return (false, "Đơn đã hủy hoặc đã thanh toán.");
        if (!await _csdl.PhuongThucThanhToan.AnyAsync(p => p.MaPhuongThuc == maPhuongThuc))
            return (false, "Phương thức thanh toán không hợp lệ.");

        donHang.MaPhuongThuc = maPhuongThuc;
        donHang.NgayThanhToan = DateTime.Now;
        donHang.MaNguoiThuTien = MaNguoiDungHienTai;
        if (donHang.MaTrangThai < HangSoTrangThai.HoanThanh) donHang.NgayCapNhat = DateTime.Now;   // đơn đã xong thì giữ nguyên giờ hoàn thành
        await _csdl.SaveChangesAsync();
        await _khuyenMai.GhiNhanThanhToanAsync(donHang);
        await PhatThongBaoAsync(donHang);
        return (true, "");
    }

    // ================= CẢNH BÁO KHO & PHIẾU XUẤT KHO (nhân viên + quản lý) =================
    /// <summary>Cảnh báo nguyên liệu sắp hết (kho dùng hàng ngày theo số cốc còn làm được, kho tổng theo mức tối thiểu)
    /// + lập phiếu xuất kho gửi quản lý duyệt.</summary>
    public async Task<IActionResult> CanhBaoKho()
    {
        var tatCa = await _csdl.NguyenLieu.AsNoTracking().Where(n => n.DangHoatDong).OrderBy(n => n.TenNguyenLieu).ToListAsync();
        var vm = new CanhBaoKhoVM
        {
            TatCaNguyenLieu = tatCa,
            SapHetKhoDung = tatCa.Where(n => n.SapHetKhoDung).OrderBy(n => n.SoCocConLai).ToList(),
            SapHetKhoTong = tatCa.Where(n => n.SapHetKhoTong).OrderBy(n => n.SoLuongTonKhoTong).ToList(),
            PhieuDangChoDuyet = await _csdl.PhieuXuatKho.AsNoTracking().Include(p => p.NguoiTao)
                .Include(p => p.DanhSachChiTiet).ThenInclude(c => c.NguyenLieu)
                .Where(p => p.TrangThai == HangSoTrangThaiXuatKho.ChoDuyet).OrderByDescending(p => p.MaPhieuXuat).ToListAsync()
        };
        return View(vm);
    }

    /// <summary>Nhân viên hoặc quản lý lập phiếu xuất kho (theo đơn vị nhập, vd chai/túi) để gửi quản lý duyệt & thực hiện xuất kho.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoPhieuXuatKho(FormPhieuXuatVM vm)
    {
        var danhSachDong = new List<(int MaNguyenLieu, decimal SoLuong)>();
        for (var i = 0; i < vm.DanhSachMaNguyenLieu.Count; i++)
        {
            var soLuong = i < vm.DanhSachSoLuong.Count ? vm.DanhSachSoLuong[i] : 0;
            if (soLuong > 0) danhSachDong.Add((vm.DanhSachMaNguyenLieu[i], soLuong));
        }
        if (danhSachDong.Count == 0)
        { TempData["CanhBao"] = "Phiếu xuất kho cần ít nhất 1 dòng có số lượng > 0."; return RedirectToAction(nameof(CanhBaoKho)); }

        var phieu = new PhieuXuatKho { MaNguoiTao = MaNguoiDungHienTai, GhiChu = vm.GhiChu?.Trim() };
        foreach (var (maNguyenLieu, soLuong) in danhSachDong)
            phieu.DanhSachChiTiet.Add(new ChiTietPhieuXuat { MaNguyenLieu = maNguyenLieu, SoLuong = soLuong });
        _csdl.PhieuXuatKho.Add(phieu);
        await _csdl.SaveChangesAsync();   // lấy MaPhieuXuat
        _nhatKy.Ghi(HangSoNhatKy.TaoPhieuXuatKho, $"Lập phiếu xuất kho #{phieu.MaPhieuXuat} ({danhSachDong.Count} dòng nguyên liệu), gửi quản lý duyệt.", MaNguoiDungHienTai);
        await _csdl.SaveChangesAsync();
        TempData["ThanhCong"] = "Đã gửi phiếu xuất kho cho quản lý duyệt.";
        return RedirectToAction(nameof(CanhBaoKho));
    }

    private async Task PhatThongBaoAsync(DonHang donHang)
    {
        await _hub.Clients.Group("nhanvien").SendAsync("DonThayDoi");
        await _hub.Clients.Group("don-" + donHang.MaTheoDoi).SendAsync("TrangThaiThayDoi", new
        {
            maTrangThai = donHang.MaTrangThai,
            tenTrangThai = HangSoTrangThai.Ten(donHang.MaTrangThai),
            daThanhToan = donHang.NgayThanhToan != null
        });
    }
}
