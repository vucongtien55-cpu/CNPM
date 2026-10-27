/* =====================================================================
   CẬP NHẬT: Chi phí vận hành hàng tháng (tiền nhà, lương nhân viên, điện nước...)
   để tính Lãi ròng = Lãi gộp (doanh thu - giá vốn) - Tổng chi phí vận hành trong tháng.
   Chạy 1 lần trong SSMS (chạy lại nhiều lần không lỗi, không mất dữ liệu).
   ===================================================================== */
USE MixueDatMonDb;
GO

IF OBJECT_ID(N'dbo.ChiPhiVanHanh', N'U') IS NULL
CREATE TABLE dbo.ChiPhiVanHanh (
    MaChiPhi     INT IDENTITY(1,1) PRIMARY KEY,
    Thang        INT NOT NULL CHECK (Thang BETWEEN 1 AND 12),
    Nam          INT NOT NULL CHECK (Nam BETWEEN 2000 AND 2100),
    LoaiChiPhi   VARCHAR(30) NOT NULL CHECK (LoaiChiPhi IN ('TIEN_NHA','LUONG_NHAN_VIEN','DIEN_NUOC','MARKETING','BAO_TRI','KHAC')),
    SoTien       DECIMAL(18,2) NOT NULL CHECK (SoTien >= 0),
    GhiChu       NVARCHAR(300) NULL,
    NgayTao      DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    MaNguoiTao   INT NULL REFERENCES dbo.NguoiDung(MaNguoiDung)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChiPhiVanHanh_NamThang')
    CREATE INDEX IX_ChiPhiVanHanh_NamThang ON dbo.ChiPhiVanHanh(Nam, Thang);
GO

SELECT N'Đã cập nhật xong CSDL cho chi phí vận hành / lãi ròng' AS KetQua;
GO
