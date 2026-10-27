/* =====================================================================
   CẬP NHẬT: mã giảm giá + tích điểm theo số điện thoại + giá vốn/lãi gộp.
   Chạy 1 lần trong SSMS (chạy lại nhiều lần không lỗi, không mất dữ liệu).
   ===================================================================== */
USE MixueDatMonDb;
GO

/* 1. Cột tích điểm trong cài đặt quán */
IF COL_LENGTH(N'dbo.CaiDatCuaHang', N'SoDongTichMotDiem') IS NULL
    ALTER TABLE dbo.CaiDatCuaHang ADD SoDongTichMotDiem INT NOT NULL
        CONSTRAINT DF_CaiDatCuaHang_SoDongTichMotDiem DEFAULT 10000
        CONSTRAINT CK_CaiDatCuaHang_SoDongTichMotDiem CHECK (SoDongTichMotDiem > 0);
GO

/* 2. Cột mã giảm giá / SĐT khách / điểm đã tích trong đơn hàng */
IF COL_LENGTH(N'dbo.DonHang', N'SoDienThoaiKhach') IS NULL
    ALTER TABLE dbo.DonHang ADD SoDienThoaiKhach VARCHAR(20) NULL;
GO
IF COL_LENGTH(N'dbo.DonHang', N'MaGiamGiaApDung') IS NULL
    ALTER TABLE dbo.DonHang ADD MaGiamGiaApDung INT NULL;
GO
IF COL_LENGTH(N'dbo.DonHang', N'SoTienGiam') IS NULL
    ALTER TABLE dbo.DonHang ADD SoTienGiam DECIMAL(12,0) NOT NULL
        CONSTRAINT DF_DonHang_SoTienGiam DEFAULT 0
        CONSTRAINT CK_DonHang_SoTienGiam CHECK (SoTienGiam >= 0);
GO
IF COL_LENGTH(N'dbo.DonHang', N'DiemDaTich') IS NULL
    ALTER TABLE dbo.DonHang ADD DiemDaTich INT NOT NULL
        CONSTRAINT DF_DonHang_DiemDaTich DEFAULT 0
        CONSTRAINT CK_DonHang_DiemDaTich CHECK (DiemDaTich >= 0);
GO

/* 3. View tổng tiền: trừ số tiền giảm giá (đơn cũ có SoTienGiam = 0 nên không đổi kết quả) */
CREATE OR ALTER VIEW dbo.vw_TongTienDonHang AS
SELECT x.MaDonHang,
       CAST(CASE WHEN x.TongGoc - x.SoTienGiam < 0 THEN 0 ELSE x.TongGoc - x.SoTienGiam END AS DECIMAL(12,0)) AS TongTien,
       CAST(x.TongGoc AS DECIMAL(12,0)) AS TongTienGoc
FROM (
    SELECT d.MaDonHang, d.SoTienGiam,
           ISNULL(SUM((ct.DonGia + ISNULL(tp.TongTopping, 0)) * ct.SoLuong), 0) AS TongGoc
    FROM dbo.DonHang d
    LEFT JOIN dbo.ChiTietDonHang ct ON ct.MaDonHang = d.MaDonHang
    LEFT JOIN (SELECT MaChiTiet, SUM(DonGia) AS TongTopping
               FROM dbo.ChiTietTopping GROUP BY MaChiTiet) tp ON tp.MaChiTiet = ct.MaChiTiet
    GROUP BY d.MaDonHang, d.SoTienGiam
) x;
GO

/* 4. Bảng khách hàng thân thiết */
IF OBJECT_ID(N'dbo.KhachHang', N'U') IS NULL
CREATE TABLE dbo.KhachHang (
    SoDienThoai    VARCHAR(20)   NOT NULL PRIMARY KEY,
    HoTen          NVARCHAR(100) NULL,
    DiemTichLuy    INT           NOT NULL DEFAULT 0 CHECK (DiemTichLuy >= 0),
    TongChiTieu    DECIMAL(14,0) NOT NULL DEFAULT 0,
    NgayTao        DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
    LanCuoiMua     DATETIME2     NULL
);
GO

/* 5. Bảng mã giảm giá */
IF OBJECT_ID(N'dbo.MaGiamGia', N'U') IS NULL
CREATE TABLE dbo.MaGiamGia (
    MaMaGiamGia     INT IDENTITY(1,1) PRIMARY KEY,
    MaCode          VARCHAR(30)   NOT NULL UNIQUE,
    MoTa            NVARCHAR(200) NULL,
    LoaiGiam        VARCHAR(10)   NOT NULL CHECK (LoaiGiam IN ('PHAN_TRAM','SO_TIEN')),
    GiaTriGiam      DECIMAL(12,0) NOT NULL CHECK (GiaTriGiam > 0),
    GiamToiDa       DECIMAL(12,0) NULL,
    DonHangToiThieu DECIMAL(12,0) NOT NULL DEFAULT 0,
    SoLuotToiDa     INT           NULL,
    SoLuotDaDung    INT           NOT NULL DEFAULT 0,
    NgayBatDau      DATETIME2     NULL,
    NgayKetThuc     DATETIME2     NULL,
    DangHoatDong    BIT           NOT NULL DEFAULT 1,
    NgayTao         DATETIME2     NOT NULL DEFAULT SYSDATETIME()
);
GO

/* 6. Khóa ngoại DonHang -> MaGiamGia */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DonHang_MaGiamGia')
    ALTER TABLE dbo.DonHang ADD CONSTRAINT FK_DonHang_MaGiamGia FOREIGN KEY (MaGiamGiaApDung) REFERENCES dbo.MaGiamGia(MaMaGiamGia);
GO

/* 7. Mã giảm giá mẫu (chỉ thêm nếu bảng đang trống, để không tạo trùng khi chạy lại) */
IF NOT EXISTS (SELECT 1 FROM dbo.MaGiamGia)
INSERT dbo.MaGiamGia (MaCode, MoTa, LoaiGiam, GiaTriGiam, GiamToiDa, DonHangToiThieu, SoLuotToiDa, NgayKetThuc) VALUES
 (N'MIXUE10', N'Giảm 10%, tối đa 15.000đ cho đơn từ 30.000đ', N'PHAN_TRAM', 10, 15000, 30000, NULL, NULL),
 (N'CHAOMOI',  N'Giảm thẳng 10.000đ cho khách mới, không cần đơn tối thiểu', N'SO_TIEN', 10000, NULL, 0, NULL, NULL);
GO

/* 8. Giá nhập mẫu cho báo cáo lãi gộp có dữ liệu demo ngay (bỏ qua nếu đã từng nhập kho thật) */
IF NOT EXISTS (SELECT 1 FROM dbo.ChiTietPhieuNhap)
BEGIN
    INSERT dbo.PhieuNhap (MaNguoiNhap, GhiChu)
    SELECT TOP 1 MaNguoiDung, N'Phiếu nhập mẫu để có dữ liệu tính giá vốn' FROM dbo.NguoiDung ORDER BY MaNguoiDung;

    INSERT dbo.ChiTietPhieuNhap (MaPhieuNhap, MaNguyenLieu, SoLuong, DonGiaNhap)
    SELECT SCOPE_IDENTITY(), nl.MaNguyenLieu, nl.SoLuongTon, gia.GiaNhap
    FROM dbo.NguyenLieu nl
    JOIN (VALUES
        (N'Hồng trà',60),(N'Trà ô long',80),(N'Bột sữa',45),(N'Đường trắng',18),(N'Sữa tươi',22),(N'Kem base',35),
        (N'Siro & mứt trái cây',90),(N'Chanh tươi',3000),(N'Ốc quế',800),(N'Ly nhựa',600),(N'Ống hút',150),
        (N'Trân châu đen',50),(N'Thạch dừa',70),(N'Lô hội',70),(N'Pudding',80)) AS gia(Ten, GiaNhap)
         ON gia.Ten = nl.TenNguyenLieu;

    INSERT dbo.BienDongKho (MaNguyenLieu, SoLuongThayDoi, LoaiBienDong, MaPhieuNhap, MaNguoiThucHien, GhiChu)
    SELECT c.MaNguyenLieu, c.SoLuong, N'NHAP', c.MaPhieuNhap, p.MaNguoiNhap, N'Nhập kho đầu kỳ (dữ liệu mẫu)'
    FROM dbo.ChiTietPhieuNhap c
    JOIN dbo.PhieuNhap p ON p.MaPhieuNhap = c.MaPhieuNhap
    WHERE c.MaPhieuNhap = (SELECT MAX(MaPhieuNhap) FROM dbo.PhieuNhap);
END
GO

SELECT N'Đã thêm xong bảng KhachHang, MaGiamGia và các cột liên quan' AS KetQua;
GO
