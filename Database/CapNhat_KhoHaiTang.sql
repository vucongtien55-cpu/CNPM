/* =====================================================================
   CẬP NHẬT: Kho 2 tầng.
   - Kho TỔNG: nhập từ nhà cung cấp theo đơn vị NHẬP (chai, túi, thùng...).
   - Kho DÙNG HÀNG NGÀY: theo đơn vị tính (ml, g...), bị trừ tự động theo công thức món
     mỗi khi lên đơn (không đổi so với trước) - nay là trang "Nguyên liệu hàng ngày".
   - Phiếu xuất kho: chuyển nguyên liệu từ kho tổng sang kho dùng hàng ngày. Nhân viên
     hoặc quản lý lập phiếu -> quản lý duyệt & thực hiện xuất kho -> "Sổ kho" chỉ còn
     lưu vết các lần xuất kho này (+ nhập kho), không còn lẫn với trừ theo đơn.
   - Cảnh báo sắp hết: kho dùng hàng ngày quy đổi ra "còn làm được bao nhiêu cốc"
     (theo định mức 1 cốc tự nhập cho từng nguyên liệu); kho tổng cảnh báo theo mức tối thiểu
     (đơn vị nhập).
   Chạy 1 lần trong SSMS (chạy lại nhiều lần không lỗi, không mất dữ liệu).
   ===================================================================== */
USE MixueDatMonDb;
GO

/* 1. NguyenLieu: thêm các cột cho kho tổng + định mức cốc (giữ nguyên SoLuongTon, SoLuongToiThieu cũ) */
IF COL_LENGTH(N'dbo.NguyenLieu', N'DinhMucMoiCoc') IS NULL
    ALTER TABLE dbo.NguyenLieu ADD DinhMucMoiCoc DECIMAL(18,3) NULL;
GO
IF COL_LENGTH(N'dbo.NguyenLieu', N'SoCocCanhBao') IS NULL
    ALTER TABLE dbo.NguyenLieu ADD SoCocCanhBao INT NOT NULL DEFAULT 10;
GO
IF COL_LENGTH(N'dbo.NguyenLieu', N'DonViNhap') IS NULL
    ALTER TABLE dbo.NguyenLieu ADD DonViNhap NVARCHAR(30) NULL;
GO
IF COL_LENGTH(N'dbo.NguyenLieu', N'QuyDoiSoLuong') IS NULL
    ALTER TABLE dbo.NguyenLieu ADD QuyDoiSoLuong DECIMAL(18,3) NOT NULL DEFAULT 1;
GO
IF COL_LENGTH(N'dbo.NguyenLieu', N'SoLuongTonKhoTong') IS NULL
    ALTER TABLE dbo.NguyenLieu ADD SoLuongTonKhoTong DECIMAL(18,3) NOT NULL DEFAULT 0;
GO
IF COL_LENGTH(N'dbo.NguyenLieu', N'MucToiThieuKhoTong') IS NULL
    ALTER TABLE dbo.NguyenLieu ADD MucToiThieuKhoTong DECIMAL(18,3) NOT NULL DEFAULT 0;
GO

/* 2. Phiếu xuất kho (kho tổng -> kho dùng hàng ngày) */
IF OBJECT_ID(N'dbo.PhieuXuatKho', N'U') IS NULL
CREATE TABLE dbo.PhieuXuatKho (
    MaPhieuXuat   INT IDENTITY(1,1) PRIMARY KEY,
    MaNguoiTao    INT NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
    NgayTao       DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    TrangThai     VARCHAR(12) NOT NULL DEFAULT 'CHO_DUYET' CHECK (TrangThai IN ('CHO_DUYET','DA_XUAT','TU_CHOI')),
    MaNguoiDuyet  INT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
    NgayDuyet     DATETIME2 NULL,
    GhiChu        NVARCHAR(300) NULL,
    LyDoTuChoi    NVARCHAR(300) NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PhieuXuatKho_TrangThai')
    CREATE INDEX IX_PhieuXuatKho_TrangThai ON dbo.PhieuXuatKho(TrangThai, NgayTao DESC);
GO

IF OBJECT_ID(N'dbo.ChiTietPhieuXuat', N'U') IS NULL
CREATE TABLE dbo.ChiTietPhieuXuat (
    MaChiTietXuat  INT IDENTITY(1,1) PRIMARY KEY,
    MaPhieuXuat    INT NOT NULL REFERENCES dbo.PhieuXuatKho(MaPhieuXuat) ON DELETE CASCADE,
    MaNguyenLieu   INT NOT NULL REFERENCES dbo.NguyenLieu(MaNguyenLieu),
    SoLuong        DECIMAL(18,3) NOT NULL CHECK (SoLuong > 0)
);
GO

/* 3. BienDongKho: thêm cột phân biệt kho tổng / kho dùng hàng ngày + liên kết phiếu xuất kho,
      mở rộng CHECK cho 2 loại biến động mới (XUAT_KHO, NHAP_TU_XUAT) */
IF COL_LENGTH(N'dbo.BienDongKho', N'ThuocKho') IS NULL
    ALTER TABLE dbo.BienDongKho ADD ThuocKho VARCHAR(10) NOT NULL DEFAULT 'SU_DUNG';
GO
IF COL_LENGTH(N'dbo.BienDongKho', N'MaPhieuXuat') IS NULL
    ALTER TABLE dbo.BienDongKho ADD MaPhieuXuat INT NULL REFERENCES dbo.PhieuXuatKho(MaPhieuXuat);
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_BienDongKho_ThuocKho')
    ALTER TABLE dbo.BienDongKho ADD CONSTRAINT CK_BienDongKho_ThuocKho CHECK (ThuocKho IN ('TONG','SU_DUNG'));
GO

DECLARE @ten SYSNAME;
SELECT @ten = cc.name
FROM sys.check_constraints cc
JOIN sys.columns c ON c.object_id = cc.parent_object_id AND c.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.BienDongKho') AND c.name = N'LoaiBienDong';
IF @ten IS NOT NULL EXEC(N'ALTER TABLE dbo.BienDongKho DROP CONSTRAINT ' + N'[' + @ten + N']');
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_BienDongKho_Loai')
    ALTER TABLE dbo.BienDongKho ADD CONSTRAINT CK_BienDongKho_Loai
        CHECK (LoaiBienDong IN ('NHAP','XUAT_DON','DIEU_CHINH','HOAN_KHO','XUAT_KHO','NHAP_TU_XUAT'));
GO

SELECT N'Da cap nhat xong CSDL cho kho 2 tang (kho tong + kho dung hang ngay + phieu xuat kho)' AS KetQua;
GO
