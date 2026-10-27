/* =====================================================================
   CẬP NHẬT: size (M/L/XL) cho món - mỗi size có giá riêng và công thức riêng.
   Chạy 1 lần trong SSMS (chạy lại nhiều lần không lỗi, không mất dữ liệu).
   Món không khai báo size nào ở bảng MonAnKichCo thì vẫn bán theo giá/công thức
   mặc định như cũ (không bắt buộc phải có size).
   ===================================================================== */
USE MixueDatMonDb;
GO

/* 1. Danh mục size dùng chung cho mọi món */
IF OBJECT_ID(N'dbo.KichCo', N'U') IS NULL
CREATE TABLE dbo.KichCo (
    MaKichCo   INT IDENTITY(1,1) PRIMARY KEY,
    TenKichCo  NVARCHAR(10) NOT NULL UNIQUE,
    ThuTu      INT NOT NULL DEFAULT 0
);
GO
IF NOT EXISTS (SELECT 1 FROM dbo.KichCo)
INSERT dbo.KichCo (TenKichCo, ThuTu) VALUES (N'M',1),(N'L',2),(N'XL',3);
GO

/* 2. Giá riêng của từng size cho từng món (chỉ món có khai báo ở đây mới hiện lựa chọn size) */
IF OBJECT_ID(N'dbo.MonAnKichCo', N'U') IS NULL
CREATE TABLE dbo.MonAnKichCo (
    MaMonAn   INT NOT NULL REFERENCES dbo.MonAn(MaMonAn) ON DELETE CASCADE,
    MaKichCo  INT NOT NULL REFERENCES dbo.KichCo(MaKichCo),
    Gia       DECIMAL(12,0) NOT NULL CHECK (Gia >= 0),
    PRIMARY KEY (MaMonAn, MaKichCo)
);
GO

/* 3. Công thức theo size: thêm cột MaKichCo (NULL = công thức mặc định, dùng khi món không phân size) */
IF COL_LENGTH(N'dbo.CongThucMon', N'MaKichCo') IS NULL
    ALTER TABLE dbo.CongThucMon ADD MaKichCo INT NULL REFERENCES dbo.KichCo(MaKichCo);
GO

/* Đổi khóa chính (MaMonAn, MaNguyenLieu) cũ sang khóa thay thế, để 1 món có thể có nhiều dòng
   công thức khác nhau cho từng size (MaMonAn, MaKichCo, MaNguyenLieu không còn là duy nhất bắt buộc
   qua PK nữa, ứng dụng tự kiểm soát không trùng khi lưu). */
IF COL_LENGTH(N'dbo.CongThucMon', N'MaCongThucMon') IS NULL
BEGIN
    DECLARE @tenPkCu sysname = (SELECT kc.name FROM sys.key_constraints kc
                                 WHERE kc.type = 'PK' AND kc.parent_object_id = OBJECT_ID(N'dbo.CongThucMon'));
    IF @tenPkCu IS NOT NULL EXEC('ALTER TABLE dbo.CongThucMon DROP CONSTRAINT ' + @tenPkCu);

    ALTER TABLE dbo.CongThucMon ADD MaCongThucMon INT IDENTITY(1,1);
    ALTER TABLE dbo.CongThucMon ADD CONSTRAINT PK_CongThucMon PRIMARY KEY (MaCongThucMon);
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CongThucMon_Mon_Size' AND object_id = OBJECT_ID(N'dbo.CongThucMon'))
    CREATE INDEX IX_CongThucMon_Mon_Size ON dbo.CongThucMon(MaMonAn, MaKichCo);
GO

/* 4. Ghi nhận size khách đã chọn trong từng dòng đơn hàng (để bếp biết pha size nào + trừ đúng kho) */
IF COL_LENGTH(N'dbo.ChiTietDonHang', N'MaKichCo') IS NULL
    ALTER TABLE dbo.ChiTietDonHang ADD MaKichCo INT NULL REFERENCES dbo.KichCo(MaKichCo);
GO

SELECT N'Đã thêm xong size (M/L/XL) cho món: bảng KichCo, MonAnKichCo, cột MaKichCo trong CongThucMon và ChiTietDonHang.' AS KetQua;
GO
