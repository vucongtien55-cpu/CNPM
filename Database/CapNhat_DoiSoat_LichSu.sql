/* =====================================================================
   CẬP NHẬT: Đối soát tiền cuối ngày cho thu ngân.
   Chạy 1 lần trong SSMS (chạy lại nhiều lần không lỗi, không mất dữ liệu).
   ===================================================================== */
USE MixueDatMonDb;
GO

IF COL_LENGTH(N'dbo.DonHang', N'MaNguoiThuTien') IS NULL
    ALTER TABLE dbo.DonHang ADD MaNguoiThuTien INT NULL
        CONSTRAINT FK_DonHang_NguoiThuTien REFERENCES dbo.NguoiDung(MaNguoiDung);
GO

/* Đơn đã thu tiền từ trước: giả định người xử lý đơn cũng là người thu */
UPDATE dbo.DonHang SET MaNguoiThuTien = MaNguoiXuLy
WHERE NgayThanhToan IS NOT NULL AND MaNguoiThuTien IS NULL AND MaNguoiXuLy IS NOT NULL;
GO

IF OBJECT_ID(N'dbo.DoiSoatCuoiNgay', N'U') IS NULL
CREATE TABLE dbo.DoiSoatCuoiNgay (
    MaDoiSoat        INT IDENTITY(1,1) PRIMARY KEY,
    NgayDoiSoat      DATE          NOT NULL,
    MaNguoiThucHien  INT           NOT NULL REFERENCES dbo.NguoiDung(MaNguoiDung),
    TienDauCa        DECIMAL(12,0) NOT NULL DEFAULT 0 CHECK (TienDauCa >= 0),
    TienMatHeThong   DECIMAL(12,0) NOT NULL,
    TienMatThucDem   DECIMAL(12,0) NOT NULL CHECK (TienMatThucDem >= 0),
    GhiChu           NVARCHAR(500) NULL,
    NgayTao          DATETIME2     NOT NULL DEFAULT SYSDATETIME()
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DoiSoatCuoiNgay_Ngay')
    CREATE INDEX IX_DoiSoatCuoiNgay_Ngay ON dbo.DoiSoatCuoiNgay(NgayDoiSoat);
GO

SELECT N'Đã thêm cột DonHang.MaNguoiThuTien và bảng DoiSoatCuoiNgay' AS KetQua;
GO
