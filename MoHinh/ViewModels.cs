using System.ComponentModel.DataAnnotations;

namespace MixueDatMon.MoHinh.VM;

// ===== Khách hàng =====
public class TrangChuVM
{
    public List<MonAn> MonNoiBat { get; set; } = new();
    public List<NhomMon> DanhSachNhom { get; set; } = new();
    public Dictionary<int, ThongKeDanhGia> ThongKe { get; set; } = new();
}

public class ThucDonVM
{
    public List<NhomMon> DanhSachNhom { get; set; } = new();
    public List<Topping> DanhSachTopping { get; set; } = new();
    public List<MucDa> DanhSachMucDa { get; set; } = new();
    public List<MucDuong> DanhSachMucDuong { get; set; } = new();
    public string? TenBan { get; set; }
    public Dictionary<int, ThongKeDanhGia> ThongKe { get; set; } = new();
    public bool DuocDatHang { get; set; } = true;
    public string? ThongBaoDongCua { get; set; }
    public HashSet<int> MonHetNguyenLieu { get; set; } = new();
    public HashSet<int> ToppingHetNguyenLieu { get; set; } = new();
    public List<MonGoiYVM> MonBanChayTuan { get; set; } = new();
    /// <summary>Size + giá riêng của từng món (chỉ món có khai báo size mới có mặt ở đây). Khóa = MaMonAn.</summary>
    public Dictionary<int, List<KichCoGiaVM>> GiaTheoSize { get; set; } = new();
}

/// <summary>1 lựa chọn size kèm giá, dùng để hiển thị cho khách/thu ngân chọn.</summary>
public class KichCoGiaVM
{
    public int MaKichCo { get; set; }
    public string TenKichCo { get; set; } = "";
    public decimal Gia { get; set; }
}

public class YeuCauDatHang
{
    public string? TenKhach { get; set; }
    public string? GhiChu { get; set; }
    public string? SoDienThoai { get; set; }
    public string? MaGiamGia { get; set; }
    public List<MonDat> DanhSachMon { get; set; } = new();
}

public class MonDat
{
    public int MaMonAn { get; set; }
    public int SoLuong { get; set; }
    public int? MaMucDa { get; set; }
    public int? MaMucDuong { get; set; }
    public int? MaKichCo { get; set; }
    public List<int>? DanhSachMaTopping { get; set; }
    public string? GhiChu { get; set; }
}

// ===== Tài khoản =====
public class DangNhapVM
{
    [Required(ErrorMessage = "Nhập tên đăng nhập")] public string TenDangNhap { get; set; } = "";
    [Required(ErrorMessage = "Nhập mật khẩu")] public string MatKhau { get; set; } = "";
    public bool GhiNho { get; set; }
    public string? DuongDanQuayLai { get; set; }
}

public class DoiMatKhauVM
{
    [Required(ErrorMessage = "Nhập mật khẩu hiện tại")] public string MatKhauHienTai { get; set; } = "";
    [Required(ErrorMessage = "Nhập mật khẩu mới"), MinLength(8, ErrorMessage = "Mật khẩu mới tối thiểu 8 ký tự")] public string MatKhauMoi { get; set; } = "";
    [Compare(nameof(MatKhauMoi), ErrorMessage = "Mật khẩu nhập lại không khớp")] public string NhapLaiMatKhau { get; set; } = "";
}

/// <summary>Quên mật khẩu (dành cho Thu ngân/Quản lý) - xác thực bằng Tên đăng nhập khớp đúng Số điện thoại đã đăng ký trong tài khoản.</summary>
public class QuenMatKhauVM
{
    [Required(ErrorMessage = "Nhập tên đăng nhập")] public string TenDangNhap { get; set; } = "";
    [Required(ErrorMessage = "Nhập số điện thoại đã đăng ký với tài khoản")] public string SoDienThoai { get; set; } = "";
    [Required(ErrorMessage = "Nhập mật khẩu mới"), MinLength(8, ErrorMessage = "Mật khẩu mới tối thiểu 8 ký tự")] public string MatKhauMoi { get; set; } = "";
    [Compare(nameof(MatKhauMoi), ErrorMessage = "Mật khẩu nhập lại không khớp")] public string NhapLaiMatKhau { get; set; } = "";
}

// ===== Thu ngân =====
public class YeuCauDoiTrangThai { public int MaDonHang { get; set; } public int MaTrangThai { get; set; } }
public class YeuCauThanhToan { public int MaDonHang { get; set; } public int MaPhuongThuc { get; set; } }

// ===== Quản lý =====
public class TongQuanVM
{
    public decimal DoanhThuHomNay { get; set; }
    public decimal DoanhThuTuan { get; set; }
    public decimal DoanhThuThang { get; set; }
    public int SoDonHomNay { get; set; }
    public int DonChoXacNhan { get; set; }
    public List<(string Nhan, decimal GiaTri)> DoanhThu7Ngay { get; set; } = new();
    public List<(string Ten, int SoLuong)> MonBanChay { get; set; } = new();
    /// <summary>Sắp hết ở kho DÙNG HÀNG NGÀY (quy theo số cốc còn làm được).</summary>
    public List<NguyenLieu> NguyenLieuSapHet { get; set; } = new();
    /// <summary>Sắp hết ở kho TỔNG (cần đặt hàng nhà cung cấp).</summary>
    public List<NguyenLieu> NguyenLieuSapHetKhoTong { get; set; } = new();
    public decimal DiemDichVu { get; set; }
    public int SoDanhGiaDichVu { get; set; }
}

public class FormMonVM
{
    public int MaMonAn { get; set; }
    [Required(ErrorMessage = "Chọn nhóm món")] public int MaNhomMon { get; set; }
    [Required(ErrorMessage = "Nhập tên món"), StringLength(150)] public string TenMon { get; set; } = "";
    [StringLength(150)] public string? TenTiengAnh { get; set; }
    [StringLength(500)] public string? MoTa { get; set; }
    [Range(0, 10000000, ErrorMessage = "Giá không hợp lệ")] public decimal Gia { get; set; }
    public string? DuongDanAnh { get; set; }
    public IFormFile? TepAnh { get; set; }
    public bool LaBanChay { get; set; }
    public bool DangBan { get; set; } = true;
    public List<NhomMon> DanhSachNhom { get; set; } = new();
}

public class FormNhanVienVM
{
    [Required(ErrorMessage = "Nhập tên đăng nhập"), RegularExpression("^[a-zA-Z0-9_.]{3,50}$", ErrorMessage = "3-50 ký tự, chỉ gồm chữ, số, . và _")]
    public string TenDangNhap { get; set; } = "";
    [Required(ErrorMessage = "Nhập họ tên"), StringLength(100)] public string HoTen { get; set; } = "";
    [Required(ErrorMessage = "Nhập số điện thoại (dùng để lấy lại mật khẩu khi quên)"), StringLength(20)]
    public string? SoDienThoai { get; set; }
    [Required(ErrorMessage = "Nhập mật khẩu"), MinLength(8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự")] public string MatKhau { get; set; } = "";
    [Required] public int MaVaiTro { get; set; }
    public List<VaiTro> DanhSachVaiTro { get; set; } = new();
}

public class FormNhapKhoVM
{
    public string? TenNhaCungCap { get; set; }
    public string? GhiChu { get; set; }
    public List<int> DanhSachMaNguyenLieu { get; set; } = new();
    /// <summary>Theo đơn vị NHẬP của nguyên liệu (chai/túi...), không phải đơn vị dùng hàng ngày.</summary>
    public List<decimal> DanhSachSoLuong { get; set; } = new();
    public List<decimal> DanhSachDonGia { get; set; } = new();
}

// ===== Kho 2 tầng: phiếu xuất kho (kho tổng -> kho dùng hàng ngày) =====
public class FormPhieuXuatVM
{
    public string? GhiChu { get; set; }
    public List<int> DanhSachMaNguyenLieu { get; set; } = new();
    /// <summary>Theo đơn vị NHẬP của nguyên liệu (chai/túi...).</summary>
    public List<decimal> DanhSachSoLuong { get; set; } = new();
}

/// <summary>Trang cảnh báo kho (cho cả nhân viên & quản lý) + lập phiếu xuất kho.</summary>
public class CanhBaoKhoVM
{
    /// <summary>Nguyên liệu sắp hết ở kho DÙNG HÀNG NGÀY (quy ra số cốc còn làm được).</summary>
    public List<NguyenLieu> SapHetKhoDung { get; set; } = new();
    /// <summary>Nguyên liệu sắp hết ở kho TỔNG (cần đặt hàng nhà cung cấp).</summary>
    public List<NguyenLieu> SapHetKhoTong { get; set; } = new();
    public List<NguyenLieu> TatCaNguyenLieu { get; set; } = new();
    public List<PhieuXuatKho> PhieuDangChoDuyet { get; set; } = new();
}

public class CongThucVM
{
    public MonAn MonAn { get; set; } = null!;
    public List<NguyenLieu> DanhSachNguyenLieu { get; set; } = new();
    public Dictionary<int, decimal> DangDung { get; set; } = new();
    /// <summary>Size đang xem/sửa công thức (null = công thức mặc định, dùng khi món không phân size).</summary>
    public int? MaKichCo { get; set; }
    /// <summary>Danh sách size món này đang có (rỗng = món không phân size).</summary>
    public List<KichCo> DanhSachKichCoMon { get; set; } = new();
}

/// <summary>Trang quản lý size + giá riêng của 1 món.</summary>
public class KichCoMonVM
{
    public MonAn MonAn { get; set; } = null!;
    public List<KichCo> TatCaKichCo { get; set; } = new();
    /// <summary>Giá theo từng size món này đang bán (rỗng = chưa phân size, bán theo MonAn.Gia).</summary>
    public Dictionary<int, decimal> GiaTheoSize { get; set; } = new();
}

public class DongDonHangVM
{
    public int MaDonHang { get; set; }
    public string MaTheoDoi { get; set; } = "";
    public string NoiPhucVu { get; set; } = "";
    public int MaTrangThai { get; set; }
    public decimal TongTien { get; set; }
    public DateTime NgayTao { get; set; }
    public DateTime? NgayThanhToan { get; set; }
    public string? NguoiXuLy { get; set; }
}

public class LichSuDonVM
{
    public List<DongDonHangVM> DanhSachDong { get; set; } = new();
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public int? TrangThai { get; set; }
}

// ===== Đánh giá =====
public class YeuCauDanhGia
{
    public string MaTheoDoi { get; set; } = "";
    public int? SoSaoDichVu { get; set; }
    public string? GopY { get; set; }
    public List<DanhGiaMonGui> DanhSachMon { get; set; } = new();
}

public class DanhGiaMonGui
{
    public int MaChiTiet { get; set; }
    public int SoSao { get; set; }
    public string? NhanXet { get; set; }
}

public class DongThongKeMonVM
{
    public string TenMon { get; set; } = "";
    public int SoLuot { get; set; }
    public decimal DiemTrungBinh { get; set; }
}

public class DongGopYVM
{
    public int MaDanhGiaDon { get; set; }
    public string MaTheoDoi { get; set; } = "";
    public string NoiPhucVu { get; set; } = "";
    public int SoSao { get; set; }
    public string? GopY { get; set; }
    public DateTime NgayTao { get; set; }
    public bool DaXem { get; set; }
}

public class DongNhanXetMonVM
{
    public int MaDanhGiaMon { get; set; }
    public string TenMon { get; set; } = "";
    public string MaTheoDoi { get; set; } = "";
    public string? TenKhach { get; set; }
    public int SoSao { get; set; }
    public string? NhanXet { get; set; }
    public DateTime NgayTao { get; set; }
    public bool DaAn { get; set; }
}

public class DanhGiaVM
{
    public int SoDanhGiaDon { get; set; }
    public decimal DiemDichVu { get; set; }
    public int SoDanhGiaMon { get; set; }
    public decimal DiemMon { get; set; }
    public int SoDanhGiaThap { get; set; }
    public int? ToiDaSao { get; set; }
    public List<DongThongKeMonVM> ThongKeMon { get; set; } = new();
    public List<DongGopYVM> DanhSachGopY { get; set; } = new();
    public List<DongNhanXetMonVM> DanhSachNhanXet { get; set; } = new();
}

// ===== Thu ngân: lịch sử và đối soát cuối ngày =====
public class DongLichSuVM
{
    public int MaDonHang { get; set; }
    public string MaTheoDoi { get; set; } = "";
    public string NoiPhucVu { get; set; } = "";
    public string? TenKhach { get; set; }
    public string MoTaMon { get; set; } = "";
    public int MaTrangThai { get; set; }
    public decimal TongTien { get; set; }
    public DateTime NgayTao { get; set; }
    public DateTime? NgayThanhToan { get; set; }
    public int? MaPhuongThuc { get; set; }
    public string? NguoiXuLy { get; set; }
    public string? NguoiThuTien { get; set; }
    public string? LyDoHuy { get; set; }
    public DateTime? NgayHoanTien { get; set; }
}

public class LichSuThuNganVM
{
    public List<DongLichSuVM> DanhSachDong { get; set; } = new();
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public int? TrangThai { get; set; }
    public string? ThanhToan { get; set; }
    public string? TuKhoa { get; set; }
    public decimal TongDaThu { get; set; }
    public int SoDonChuaThu { get; set; }
    public bool DaCatBot { get; set; }
}

public class DongThuTienVM
{
    public int MaDonHang { get; set; }
    public string MaTheoDoi { get; set; } = "";
    public string NoiPhucVu { get; set; } = "";
    public int MaPhuongThuc { get; set; }
    public DateTime NgayThanhToan { get; set; }
    public decimal SoTien { get; set; }
    public string? NguoiThu { get; set; }
    public bool DaHuy { get; set; }
    public DateTime? NgayHoanTien { get; set; }
}

public class DongHoanTienVM
{
    public int MaDonHang { get; set; }
    public string MaTheoDoi { get; set; } = "";
    public string NoiPhucVu { get; set; } = "";
    public int MaPhuongThuc { get; set; }
    public decimal SoTien { get; set; }
    public DateTime NgayHoanTien { get; set; }
    public string? LyDo { get; set; }
    public string? NguoiHuy { get; set; }
}

public class DongChuaThuVM
{
    public int MaDonHang { get; set; }
    public string MaTheoDoi { get; set; } = "";
    public string NoiPhucVu { get; set; } = "";
    public int MaTrangThai { get; set; }
    public DateTime NgayTao { get; set; }
    public decimal SoTien { get; set; }
}

public class DongNgayVM
{
    public DateTime Ngay { get; set; }
    public int SoDon { get; set; }
    public decimal TienMat { get; set; }
    public decimal ChuyenKhoan { get; set; }
    public decimal Tong => TienMat + ChuyenKhoan;
}

public class DoiSoatVM
{
    public DateTime Ngay { get; set; }
    public decimal TienMat { get; set; }
    public int SoDonTienMat { get; set; }
    public decimal ChuyenKhoan { get; set; }
    public int SoDonChuyenKhoan { get; set; }
    public decimal Tong => TienMat + ChuyenKhoan;
    public decimal TienChuaThu { get; set; }
    public int SoDonHuy { get; set; }
    public List<DongThuTienVM> DanhSachThu { get; set; } = new();
    public List<DongHoanTienVM> DanhSachHoan { get; set; } = new();
    public decimal TienHoanMat { get; set; }
    public decimal TienHoanChuyenKhoan { get; set; }
    public List<DongChuaThuVM> DanhSachChuaThu { get; set; } = new();
    public List<DongNgayVM> BayNgay { get; set; } = new();
    public List<DoiSoatCuoiNgay> DaLuu { get; set; } = new();
}

// ===== Thu ngân: tạo đơn tại quầy, thu tiền cả bàn =====
public class YeuCauThuCaBan { public int MaBan { get; set; } public int MaPhuongThuc { get; set; } }

public class YeuCauTaoDonTaiQuay
{
    public int? MaBan { get; set; }              // null = mang đi
    public string? TenKhach { get; set; }
    public string? GhiChu { get; set; }
    public string? SoDienThoai { get; set; }
    public string? MaGiamGia { get; set; }
    public int? MaPhuongThuc { get; set; }       // có = thu tiền ngay; null = thu sau (chỉ cho khách ngồi bàn)
    public List<MonDat> DanhSachMon { get; set; } = new();
}

// ===== Mã giảm giá & tích điểm =====
public class YeuCauKiemTraMa
{
    public string MaCode { get; set; } = "";
    public List<MonDat> DanhSachMon { get; set; } = new();
}

public class KetQuaKiemTraMa
{
    public bool HopLe { get; set; }
    public string ThongBao { get; set; } = "";
    public decimal SoTienGiam { get; set; }
    public decimal TongTienGoc { get; set; }
    public decimal TongTienSauGiam { get; set; }
}

public class FormMaGiamGiaVM
{
    public int MaMaGiamGia { get; set; }
    [Required(ErrorMessage = "Nhập mã code")] public string MaCode { get; set; } = "";
    public string? MoTa { get; set; }
    public string LoaiGiam { get; set; } = "SO_TIEN";
    [Range(1, 100000000, ErrorMessage = "Giá trị giảm phải lớn hơn 0")] public decimal GiaTriGiam { get; set; }
    public decimal? GiamToiDa { get; set; }
    public decimal DonHangToiThieu { get; set; }
    public int? SoLuotToiDa { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public bool DangHoatDong { get; set; } = true;
}

public class KhachHangVM
{
    public string SoDienThoai { get; set; } = "";
    public string? HoTen { get; set; }
    public int DiemTichLuy { get; set; }
    public decimal TongChiTieu { get; set; }
    public DateTime? LanCuoiMua { get; set; }
    public int SoDonDaMua { get; set; }
}

public class DiemCuaToiVM
{
    public string SoDienThoai { get; set; } = "";
    public int DiemTichLuy { get; set; }
    public decimal TongChiTieu { get; set; }
    public int SoDongTichMotDiem { get; set; }
}

public class MonGoiYVM
{
    public int MaMonAn { get; set; }
    public string TenMon { get; set; } = "";
    public decimal Gia { get; set; }
    public string? DuongDanAnh { get; set; }
    public bool CoTuyChon { get; set; }
    public int SoLanMuaCung { get; set; }
}

// ===== Báo cáo lãi gộp =====
public class DongLaiGopMonVM
{
    public string TenMon { get; set; } = "";
    public int SoLuongBan { get; set; }
    public decimal DoanhThu { get; set; }
    public decimal GiaVon { get; set; }
    public decimal LaiGop { get; set; }
    public bool ThieuGiaVon { get; set; }
    public decimal TyLeLaiGop => DoanhThu <= 0 ? 0 : Math.Round(LaiGop / DoanhThu * 100, 1);
}

public class BaoCaoLaiGopVM
{
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public List<DongLaiGopMonVM> DanhSachMon { get; set; } = new();
    public decimal TongDoanhThu => DanhSachMon.Sum(x => x.DoanhThu);
    public decimal TongGiaVon => DanhSachMon.Sum(x => x.GiaVon);
    public decimal TongLaiGop => DanhSachMon.Sum(x => x.LaiGop);
    public bool CoMonThieuGiaVon => DanhSachMon.Any(x => x.ThieuGiaVon);
}

// ===== Lãi ròng theo tháng (lãi gộp - chi phí vận hành nhập tay) =====
public class FormChiPhiVM
{
    public int Thang { get; set; } = DateTime.Today.Month;
    public int Nam { get; set; } = DateTime.Today.Year;
    [Required(ErrorMessage = "Chọn loại chi phí")] public string LoaiChiPhi { get; set; } = HangSoLoaiChiPhi.Khac;
    [Range(0, 999999999, ErrorMessage = "Số tiền không hợp lệ")] public decimal SoTien { get; set; }
    [StringLength(300)] public string? GhiChu { get; set; }
}

public class BaoCaoLaiRongVM
{
    public int Thang { get; set; }
    public int Nam { get; set; }

    // Tự động tính từ đơn hàng trong tháng (giống báo cáo lãi gộp)
    public decimal TongDoanhThu { get; set; }
    public decimal TongGiaVon { get; set; }
    public decimal TongLaiGop => TongDoanhThu - TongGiaVon;
    public bool CoMonThieuGiaVon { get; set; }

    // Nhập tay
    public List<ChiPhiVanHanh> DanhSachChiPhi { get; set; } = new();
    public decimal TongChiPhi => DanhSachChiPhi.Sum(x => x.SoTien);

    public decimal LaiRong => TongLaiGop - TongChiPhi;
    public decimal TyLeLaiRong => TongDoanhThu <= 0 ? 0 : Math.Round(LaiRong / TongDoanhThu * 100, 1);

    public FormChiPhiVM FormMoi { get; set; } = new();
}

public class TaoDonVM
{
    public List<NhomMon> DanhSachNhom { get; set; } = new();
    public List<Topping> DanhSachTopping { get; set; } = new();
    public List<MucDa> DanhSachMucDa { get; set; } = new();
    public List<MucDuong> DanhSachMucDuong { get; set; } = new();
    public List<BanAn> DanhSachBan { get; set; } = new();
    public HashSet<int> MonHetNguyenLieu { get; set; } = new();
    public HashSet<int> ToppingHetNguyenLieu { get; set; } = new();
    public Dictionary<int, List<KichCoGiaVM>> GiaTheoSize { get; set; } = new();
}

// ===== Vận hành: hủy đơn, chuyển bàn, tách bill, yêu cầu từ bàn =====
public class YeuCauHuyDon
{
    public int MaDonHang { get; set; }
    public string? LyDo { get; set; }
    public string? GhiChu { get; set; }
    public bool HoanKho { get; set; }
    public bool DaHoanTien { get; set; }
}

public class YeuCauChuyenBan { public int TuMaBan { get; set; } public int DenMaBan { get; set; } }
public class YeuCauTachDon { public int MaDonHang { get; set; } public List<int> DanhSachMaChiTiet { get; set; } = new(); }
public class YeuCauXuLy { public int Ma { get; set; } }
public class YeuCauGoiNhanVien { public string? MaTheoDoi { get; set; } public string Loai { get; set; } = ""; }

public class NhatKyVM
{
    public List<NhatKyThaoTac> DanhSachDong { get; set; } = new();
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public string? HanhDong { get; set; }
    public bool DaCatBot { get; set; }
}
