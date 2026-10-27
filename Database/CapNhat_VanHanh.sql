/* =====================================================================
   CẬP NHẬT: vận hành thực tế (giờ mở cửa, tự hủy đơn, hủy có lý do và hoàn tiền,
   hoàn kho, nhật ký thao tác, yêu cầu từ bàn).
   Chạy 1 lần trong SSMS (chạy lại nhiều lần không lỗi, không mất dữ liệu).
   ===================================================================== */
USE MixueDatMonDb;
GO

/* 1. Đơn hàng: lý do hủy, ngày hủy, người hủy, ngày hoàn tiền */
IF COL_LENGTH(N'dbo.DonHang', N'LyDoHuy') IS NULL
    ALTER TABLE dbo.DonHang ADD LyDoHuy NVARCHAR(300) NULL, NgayHuy DATETIME2 NULL, NgayHoanTien DATETIME2 NULL;
GO
IF COL_LENGTH(N'dbo.DonHang', N'MaNguoiHuy') IS NULL
    ALTER TABLE dbo.DonHang ADD MaNguoiHuy INT NULL CONSTRAINT FK_DonHang_NguoiHuy REFERENCES dbo.NguoiDung(MaNguoiDung);
GO

/* 2. Sổ kho: cho phép loại biến động HOAN_KHO (đổi ràng buộc CHECK cũ, không phụ thuộc tên tự sinh) */
DECLARE @ten SYSNAME;
SELECT @ten = cc.name
FROM sys.check_constraints cc
JOIN sys.columns c ON c.object_id = cc.parent_object_id AND c.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.BienDongKho') AND c.name = N'LoaiBienDong';
IF @ten IS NOT NULL EXEC(N'ALTER TABLE dbo.BienDongKho DROP CONSTRAINT ' + N'[' + @ten + N']');
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_BienDongKho_Loai')
    ALTER TABLE dbo.BienDongKho ADD CONSTRAINT CK_BienDongKho_Loai
        CHECK (LoaiBienDong IN ('NHAP','XUAT_DON','DIEU_CHINH','HOAN_KHO'));
GO

/* 3. Cài đặt quán (1 dòng duy nhất) */
IF OBJECT_ID(N'dbo.CaiDatCuaHang', N'U') IS NULL
CREATE TABLE dbo.CaiDatCuaHang (
    MaCaiDat              INT  NOT NULL PRIMARY KEY CHECK (MaCaiDat = 1),
    GioMo                 TIME(0) NOT NULL DEFAULT '08:00',
    GioDong               TIME(0) NOT NULL DEFAULT '22:00',
    TamNgungNhanDon       BIT  NOT NULL DEFAULT 0,
    SoPhutTuHuyDonMangDi  INT  NOT NULL DEFAULT 15 CHECK (SoPhutTuHuyDonMangDi BETWEEN 0 AND 240),
    TuAnMonHetNguyenLieu  BIT  NOT NULL DEFAULT 1
);
GO
IF NOT EXISTS (SELECT 1 FROM dbo.CaiDatCuaHang) INSERT dbo.CaiDatCuaHang (MaCaiDat) VALUES (1);
GO

/* 4. Nhật ký thao tác */
IF OBJECT_ID(N'dbo.NhatKyThaoTac', N'U') IS NULL
CREATE TABLE dbo.NhatKyThaoTac (
    MaNhatKy         BIGINT IDENTITY(1,1) PRIMARY KEY,
    NgayTao          DATETIME2    NOT NULL DEFAULT SYSDATETIME(),
    MaNguoiThucHien  INT          NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
    HanhDong         VARCHAR(30)  NOT NULL,
    MoTa             NVARCHAR(500) NOT NULL,
    MaDonHang        INT          NULL REFERENCES dbo.DonHang(MaDonHang)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_NhatKyThaoTac_NgayTao')
    CREATE INDEX IX_NhatKyThaoTac_NgayTao ON dbo.NhatKyThaoTac(NgayTao DESC);
GO

/* 5. Yêu cầu từ bàn (gọi nhân viên, xin thanh toán) */
IF OBJECT_ID(N'dbo.YeuCauTaiBan', N'U') IS NULL
CREATE TABLE dbo.YeuCauTaiBan (
    MaYeuCau      INT IDENTITY(1,1) PRIMARY KEY,
    MaBan         INT         NOT NULL REFERENCES dbo.BanAn(MaBan),
    LoaiYeuCau    VARCHAR(20) NOT NULL CHECK (LoaiYeuCau IN ('GOI_NHAN_VIEN','XIN_THANH_TOAN')),
    NgayTao       DATETIME2   NOT NULL DEFAULT SYSDATETIME(),
    DaXuLy        BIT         NOT NULL DEFAULT 0,
    NgayXuLy      DATETIME2   NULL,
    MaNguoiXuLy   INT         NULL REFERENCES dbo.NguoiDung(MaNguoiDung)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_YeuCauTaiBan_ChuaXuLy')
    CREATE INDEX IX_YeuCauTaiBan_ChuaXuLy ON dbo.YeuCauTaiBan(DaXuLy, NgayTao);
GO

SELECT N'Đã cập nhật xong CSDL cho vận hành thực tế' AS KetQua;
GO
