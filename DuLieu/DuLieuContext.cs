using Microsoft.EntityFrameworkCore;
using MixueDatMon.MoHinh;

namespace MixueDatMon.DuLieu;

public class DuLieuContext : DbContext
{
    public DuLieuContext(DbContextOptions<DuLieuContext> tuyChon) : base(tuyChon) { }

    // Tên DbSet trùng tên bảng trong MixueDatMonDb.sql
    public DbSet<VaiTro> VaiTro => Set<VaiTro>();
    public DbSet<NguoiDung> NguoiDung => Set<NguoiDung>();
    public DbSet<NhomMon> NhomMon => Set<NhomMon>();
    public DbSet<MonAn> MonAn => Set<MonAn>();
    public DbSet<Topping> Topping => Set<Topping>();
    public DbSet<MucDa> MucDa => Set<MucDa>();
    public DbSet<MucDuong> MucDuong => Set<MucDuong>();
    public DbSet<BanAn> BanAn => Set<BanAn>();
    public DbSet<TrangThaiDon> TrangThaiDon => Set<TrangThaiDon>();
    public DbSet<PhuongThucThanhToan> PhuongThucThanhToan => Set<PhuongThucThanhToan>();
    public DbSet<DonHang> DonHang => Set<DonHang>();
    public DbSet<ChiTietDonHang> ChiTietDonHang => Set<ChiTietDonHang>();
    public DbSet<ChiTietTopping> ChiTietTopping => Set<ChiTietTopping>();
    public DbSet<TongTienDon> TongTienDon => Set<TongTienDon>();
    public DbSet<NguyenLieu> NguyenLieu => Set<NguyenLieu>();
    public DbSet<CongThucMon> CongThucMon => Set<CongThucMon>();
    public DbSet<CongThucTopping> CongThucTopping => Set<CongThucTopping>();
    public DbSet<NhaCungCap> NhaCungCap => Set<NhaCungCap>();
    public DbSet<PhieuNhap> PhieuNhap => Set<PhieuNhap>();
    public DbSet<ChiTietPhieuNhap> ChiTietPhieuNhap => Set<ChiTietPhieuNhap>();
    public DbSet<BienDongKho> BienDongKho => Set<BienDongKho>();
    public DbSet<PhieuXuatKho> PhieuXuatKho => Set<PhieuXuatKho>();
    public DbSet<ChiTietPhieuXuat> ChiTietPhieuXuat => Set<ChiTietPhieuXuat>();
    public DbSet<DanhGiaMon> DanhGiaMon => Set<DanhGiaMon>();
    public DbSet<DanhGiaDonHang> DanhGiaDonHang => Set<DanhGiaDonHang>();
    public DbSet<ThongKeDanhGia> ThongKeDanhGia => Set<ThongKeDanhGia>();
    public DbSet<DoiSoatCuoiNgay> DoiSoatCuoiNgay => Set<DoiSoatCuoiNgay>();
    public DbSet<CaiDatCuaHang> CaiDatCuaHang => Set<CaiDatCuaHang>();
    public DbSet<ChiPhiVanHanh> ChiPhiVanHanh => Set<ChiPhiVanHanh>();
    public DbSet<NhatKyThaoTac> NhatKyThaoTac => Set<NhatKyThaoTac>();
    public DbSet<YeuCauTaiBan> YeuCauTaiBan => Set<YeuCauTaiBan>();
    public DbSet<KhachHang> KhachHang => Set<KhachHang>();
    public DbSet<MaGiamGia> MaGiamGia => Set<MaGiamGia>();
    public DbSet<KichCo> KichCo => Set<KichCo>();
    public DbSet<MonAnKichCo> MonAnKichCo => Set<MonAnKichCo>();

    protected override void ConfigureConventions(ModelConfigurationBuilder cauHinh)
    {
        cauHinh.Properties<decimal>().HavePrecision(12, 0);   // VND không có phần lẻ
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ----- Khóa chính ghép / view -----
        mb.Entity<ChiTietTopping>().HasKey(x => new { x.MaChiTiet, x.MaTopping });
        mb.Entity<CongThucTopping>().HasKey(x => new { x.MaTopping, x.MaNguyenLieu });
        mb.Entity<MonAnKichCo>().HasKey(x => new { x.MaMonAn, x.MaKichCo });
        mb.Entity<TongTienDon>().HasNoKey().ToView("vw_TongTienDonHang");
        mb.Entity<ThongKeDanhGia>().HasNoKey().ToView("vw_ThongKeDanhGiaMon");

        // Bảng danh mục có mã cố định, không tự tăng
        mb.Entity<TrangThaiDon>().Property(x => x.MaTrangThai).ValueGeneratedNever();
        mb.Entity<PhuongThucThanhToan>().Property(x => x.MaPhuongThuc).ValueGeneratedNever();

        // ----- Khai báo rõ TẤT CẢ khóa ngoại (tên cột như MaVaiTro không theo quy ước mặc định của EF,
        //       nếu không khai báo EF sẽ tự tạo cột giả kiểu "VaiTroMaVaiTro" và báo lỗi Invalid column name) -----
        mb.Entity<NguoiDung>().HasOne(n => n.VaiTro).WithMany().HasForeignKey(n => n.MaVaiTro);

        mb.Entity<MonAn>().HasOne(m => m.NhomMon).WithMany(n => n.DanhSachMon).HasForeignKey(m => m.MaNhomMon);

        mb.Entity<DonHang>().HasOne(d => d.BanAn).WithMany().HasForeignKey(d => d.MaBan);
        mb.Entity<DonHang>().HasOne(d => d.TrangThaiDon).WithMany().HasForeignKey(d => d.MaTrangThai);
        mb.Entity<DonHang>().HasOne(d => d.PhuongThucThanhToan).WithMany().HasForeignKey(d => d.MaPhuongThuc);
        mb.Entity<DonHang>().HasOne(d => d.NguoiXuLy).WithMany().HasForeignKey(d => d.MaNguoiXuLy);
        mb.Entity<DonHang>().HasOne(d => d.NguoiThuTien).WithMany().HasForeignKey(d => d.MaNguoiThuTien);
        mb.Entity<DonHang>().HasOne(d => d.NguoiHuy).WithMany().HasForeignKey(d => d.MaNguoiHuy);
        mb.Entity<CaiDatCuaHang>().Property(x => x.MaCaiDat).ValueGeneratedNever();
        mb.Entity<DonHang>().HasOne(d => d.MaGiamGia).WithMany().HasForeignKey(d => d.MaGiamGiaApDung);
        mb.Entity<KhachHang>().Property(k => k.SoDienThoai).HasMaxLength(20).ValueGeneratedNever();
        mb.Entity<MaGiamGia>().HasIndex(m => m.MaCode).IsUnique();
        mb.Entity<NhatKyThaoTac>().HasOne(n => n.NguoiThucHien).WithMany().HasForeignKey(n => n.MaNguoiThucHien);
        mb.Entity<ChiPhiVanHanh>().HasOne(c => c.NguoiTao).WithMany().HasForeignKey(c => c.MaNguoiTao);
        mb.Entity<ChiPhiVanHanh>().HasIndex(c => new { c.Nam, c.Thang });
        mb.Entity<YeuCauTaiBan>().HasOne(y => y.BanAn).WithMany().HasForeignKey(y => y.MaBan);
        mb.Entity<YeuCauTaiBan>().HasOne(y => y.NguoiXuLy).WithMany().HasForeignKey(y => y.MaNguoiXuLy);
        mb.Entity<DoiSoatCuoiNgay>().HasOne(d => d.NguoiThucHien).WithMany().HasForeignKey(d => d.MaNguoiThucHien);

        mb.Entity<ChiTietDonHang>().HasOne(c => c.DonHang).WithMany(d => d.DanhSachChiTiet).HasForeignKey(c => c.MaDonHang);
        mb.Entity<ChiTietDonHang>().HasOne(c => c.MonAn).WithMany().HasForeignKey(c => c.MaMonAn);
        mb.Entity<ChiTietDonHang>().HasOne(c => c.MucDa).WithMany().HasForeignKey(c => c.MaMucDa);
        mb.Entity<ChiTietDonHang>().HasOne(c => c.MucDuong).WithMany().HasForeignKey(c => c.MaMucDuong);
        mb.Entity<ChiTietDonHang>().HasOne(c => c.KichCo).WithMany().HasForeignKey(c => c.MaKichCo);

        mb.Entity<MonAnKichCo>().HasOne(x => x.MonAn).WithMany().HasForeignKey(x => x.MaMonAn);
        mb.Entity<MonAnKichCo>().HasOne(x => x.KichCo).WithMany().HasForeignKey(x => x.MaKichCo);
        mb.Entity<CongThucMon>().HasOne(x => x.KichCo).WithMany().HasForeignKey(x => x.MaKichCo);
        mb.Entity<KichCo>().HasIndex(k => k.TenKichCo).IsUnique();

        mb.Entity<ChiTietTopping>().HasOne(t => t.ChiTietDonHang).WithMany(c => c.DanhSachTopping).HasForeignKey(t => t.MaChiTiet);
        mb.Entity<ChiTietTopping>().HasOne(t => t.Topping).WithMany().HasForeignKey(t => t.MaTopping);

        mb.Entity<PhieuNhap>().HasOne(p => p.NhaCungCap).WithMany().HasForeignKey(p => p.MaNhaCungCap);
        mb.Entity<PhieuNhap>().HasMany(p => p.DanhSachChiTiet).WithOne().HasForeignKey(c => c.MaPhieuNhap);

        mb.Entity<BienDongKho>().HasOne(b => b.NguyenLieu).WithMany().HasForeignKey(b => b.MaNguyenLieu);

        mb.Entity<PhieuXuatKho>().HasOne(p => p.NguoiTao).WithMany().HasForeignKey(p => p.MaNguoiTao);
        mb.Entity<PhieuXuatKho>().HasOne(p => p.NguoiDuyet).WithMany().HasForeignKey(p => p.MaNguoiDuyet);
        mb.Entity<PhieuXuatKho>().HasMany(p => p.DanhSachChiTiet).WithOne().HasForeignKey(c => c.MaPhieuXuat);
        mb.Entity<ChiTietPhieuXuat>().HasOne(c => c.NguyenLieu).WithMany().HasForeignKey(c => c.MaNguyenLieu);

        // Mỗi dòng món / mỗi đơn chỉ có tối đa 1 đánh giá (quan hệ 1-1)
        mb.Entity<DanhGiaMon>().HasOne(d => d.ChiTietDonHang).WithOne(c => c.DanhGia).HasForeignKey<DanhGiaMon>(d => d.MaChiTiet);
        mb.Entity<DanhGiaDonHang>().HasOne(d => d.DonHang).WithOne(o => o.DanhGia).HasForeignKey<DanhGiaDonHang>(d => d.MaDonHang);
    }
}
