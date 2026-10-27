/* =====================================================================
   CẬP NHẬT: thêm chức năng ĐÁNH GIÁ vào CSDL MixueDatMonDb đã có sẵn.
   Chạy 1 lần trong SSMS (chạy lại nhiều lần cũng không lỗi, không mất dữ liệu).
   ===================================================================== */
USE MixueDatMonDb;
GO

IF OBJECT_ID(N'dbo.DanhGiaMon', N'U') IS NULL
CREATE TABLE DanhGiaMon (
    MaDanhGiaMon  INT IDENTITY(1,1) PRIMARY KEY,
    MaChiTiet     INT NOT NULL UNIQUE
                  REFERENCES ChiTietDonHang(MaChiTiet) ON DELETE CASCADE,
    SoSao         INT NOT NULL CHECK (SoSao BETWEEN 1 AND 5),
    NhanXet       NVARCHAR(500) NULL,
    NgayTao       DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    DaAn          BIT NOT NULL DEFAULT 0
);
GO

IF OBJECT_ID(N'dbo.DanhGiaDonHang', N'U') IS NULL
CREATE TABLE DanhGiaDonHang (
    MaDanhGiaDon  INT IDENTITY(1,1) PRIMARY KEY,
    MaDonHang     INT NOT NULL UNIQUE
                  REFERENCES DonHang(MaDonHang) ON DELETE CASCADE,
    SoSao         INT NOT NULL CHECK (SoSao BETWEEN 1 AND 5),
    GopY          NVARCHAR(1000) NULL,
    NgayTao       DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    DaXem         BIT NOT NULL DEFAULT 0
);
GO

CREATE OR ALTER VIEW dbo.vw_ThongKeDanhGiaMon AS
SELECT ct.MaMonAn,
       COUNT(*) AS SoLuot,
       CAST(AVG(CAST(dg.SoSao AS DECIMAL(5,2))) AS DECIMAL(3,2)) AS DiemTrungBinh
FROM DanhGiaMon dg
JOIN ChiTietDonHang ct ON ct.MaChiTiet = dg.MaChiTiet
WHERE dg.DaAn = 0
GROUP BY ct.MaMonAn;
GO

SELECT N'Đã thêm xong bảng DanhGiaMon, DanhGiaDonHang và view vw_ThongKeDanhGiaMon' AS KetQua;
GO
