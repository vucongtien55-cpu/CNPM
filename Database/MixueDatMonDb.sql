/* =====================================================================
   MIXUE DAT MON - SQL Server (chuẩn 3NF) - tên bảng/cột tiếng Việt không dấu
   Chạy toàn bộ script trong SSMS / Azure Data Studio.
   Tài khoản mẫu:  quanly / Admin@123    |    thungan1 / Thungan@123
   Mật khẩu lưu dạng: SHA-256( MuoiMatKhau + MậtKhẩu ), muối 32 ký tự hex / mỗi người dùng.
   ===================================================================== */
IF DB_ID(N'MixueDatMonDb') IS NULL CREATE DATABASE MixueDatMonDb;
GO
USE MixueDatMonDb;
GO

/* ---------- 1. PHÂN QUYỀN & NHÂN SỰ ---------- */
CREATE TABLE VaiTro (
    MaVaiTro   INT IDENTITY(1,1) PRIMARY KEY,
    TenVaiTro  NVARCHAR(30) NOT NULL UNIQUE          -- QuanLy | ThuNgan
);

CREATE TABLE NguoiDung (
    MaNguoiDung      INT IDENTITY(1,1) PRIMARY KEY,
    TenDangNhap      VARCHAR(50)   NOT NULL UNIQUE,
    MatKhauHash      CHAR(64)      NOT NULL,          -- SHA-256 dạng hex
    MuoiMatKhau      CHAR(32)      NOT NULL,          -- 16 byte ngẫu nhiên, hex
    HoTen            NVARCHAR(100) NOT NULL,
    SoDienThoai      VARCHAR(20)   NULL,
    MaVaiTro         INT           NOT NULL REFERENCES VaiTro(MaVaiTro),
    DangHoatDong     BIT           NOT NULL DEFAULT 1,
    NgayTao          DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
    LanDangNhapCuoi  DATETIME2     NULL
);

/* ---------- 2. THỰC ĐƠN ---------- */
CREATE TABLE NhomMon (
    MaNhomMon        INT IDENTITY(1,1) PRIMARY KEY,
    TenNhomMon       NVARCHAR(100) NOT NULL UNIQUE,
    BieuTuong        NVARCHAR(10)  NULL,               -- emoji hiển thị khi món chưa có ảnh
    ThuTuHienThi     INT           NOT NULL DEFAULT 0,
    CoTuyChonDoUong  BIT           NOT NULL DEFAULT 1, -- 1 = cho chọn lượng đá / đường / topping
    DangHoatDong     BIT           NOT NULL DEFAULT 1
);

CREATE TABLE MonAn (
    MaMonAn       INT IDENTITY(1,1) PRIMARY KEY,
    MaNhomMon     INT           NOT NULL REFERENCES NhomMon(MaNhomMon),
    TenMon        NVARCHAR(150) NOT NULL,
    TenTiengAnh   NVARCHAR(150) NULL,
    MoTa          NVARCHAR(500) NULL,
    Gia           DECIMAL(12,0) NOT NULL CHECK (Gia >= 0),
    DuongDanAnh   NVARCHAR(300) NULL,
    LaBanChay     BIT           NOT NULL DEFAULT 0,
    DangBan       BIT           NOT NULL DEFAULT 1,
    NgayTao       DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_MonAn_Nhom_Ten UNIQUE (MaNhomMon, TenMon)
);

CREATE TABLE Topping (
    MaTopping   INT IDENTITY(1,1) PRIMARY KEY,
    TenTopping  NVARCHAR(100) NOT NULL UNIQUE,
    Gia         DECIMAL(12,0) NOT NULL CHECK (Gia >= 0),
    DangBan     BIT NOT NULL DEFAULT 1
);

CREATE TABLE MucDa    (MaMucDa    INT IDENTITY(1,1) PRIMARY KEY, TenMuc NVARCHAR(50) NOT NULL UNIQUE);
CREATE TABLE MucDuong (MaMucDuong INT IDENTITY(1,1) PRIMARY KEY, TenMuc NVARCHAR(50) NOT NULL UNIQUE);

/* ---------- 3. BÀN & QR ---------- */
CREATE TABLE BanAn (
    MaBan         INT IDENTITY(1,1) PRIMARY KEY,
    TenBan        NVARCHAR(30) NOT NULL UNIQUE,
    MaQr          CHAR(32)     NOT NULL UNIQUE,      -- chuỗi ngẫu nhiên nhúng trong mã QR
    DangHoatDong  BIT NOT NULL DEFAULT 1
);

/* ---------- 4. ĐƠN HÀNG ---------- */
CREATE TABLE TrangThaiDon        (MaTrangThai  INT PRIMARY KEY, TenTrangThai  NVARCHAR(50) NOT NULL UNIQUE);
CREATE TABLE PhuongThucThanhToan (MaPhuongThuc INT PRIMARY KEY, TenPhuongThuc NVARCHAR(50) NOT NULL UNIQUE);

CREATE TABLE DonHang (
    MaDonHang      INT IDENTITY(1,1) PRIMARY KEY,
    MaTheoDoi      VARCHAR(20)   NOT NULL UNIQUE,      -- mã khách dùng để theo dõi đơn
    MaBan          INT           NULL REFERENCES BanAn(MaBan),   -- NULL = mang đi
    MaTrangThai    INT           NOT NULL DEFAULT 1 REFERENCES TrangThaiDon(MaTrangThai),
    TenKhach       NVARCHAR(100) NULL,
    GhiChuKhach    NVARCHAR(300) NULL,
    MaPhuongThuc   INT           NULL REFERENCES PhuongThucThanhToan(MaPhuongThuc),
    NgayThanhToan  DATETIME2     NULL,
    MaNguoiXuLy    INT           NULL REFERENCES NguoiDung(MaNguoiDung),  -- thu ngân nhận đơn
    MaNguoiThuTien INT           NULL REFERENCES NguoiDung(MaNguoiDung),  -- người thu tiền (để đối soát cuối ngày)
    LyDoHuy        NVARCHAR(300) NULL,                                    -- lý do hủy đơn
    NgayHuy        DATETIME2     NULL,
    MaNguoiHuy     INT           NULL REFERENCES NguoiDung(MaNguoiDung),  -- NULL nếu hệ thống tự hủy
    NgayHoanTien   DATETIME2     NULL,                                    -- có giá trị = đã hoàn tiền cho khách
    NgayTao        DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
    NgayCapNhat    DATETIME2     NULL,
    CONSTRAINT CK_DonHang_ThanhToan CHECK ((NgayThanhToan IS NULL AND MaPhuongThuc IS NULL)
                                        OR (NgayThanhToan IS NOT NULL AND MaPhuongThuc IS NOT NULL))
);
CREATE INDEX IX_DonHang_TrangThai_NgayTao ON DonHang(MaTrangThai, NgayTao);
CREATE INDEX IX_DonHang_NgayThanhToan     ON DonHang(NgayThanhToan);

CREATE TABLE ChiTietDonHang (
    MaChiTiet   INT IDENTITY(1,1) PRIMARY KEY,
    MaDonHang   INT NOT NULL REFERENCES DonHang(MaDonHang) ON DELETE CASCADE,
    MaMonAn     INT NOT NULL REFERENCES MonAn(MaMonAn),
    SoLuong     INT NOT NULL CHECK (SoLuong > 0),
    DonGia      DECIMAL(12,0) NOT NULL,                -- giá tại thời điểm đặt (lưu lịch sử)
    MaMucDa     INT NULL REFERENCES MucDa(MaMucDa),
    MaMucDuong  INT NULL REFERENCES MucDuong(MaMucDuong),
    GhiChu      NVARCHAR(200) NULL
);
CREATE INDEX IX_ChiTietDonHang_DonHang ON ChiTietDonHang(MaDonHang);

CREATE TABLE ChiTietTopping (
    MaChiTiet  INT NOT NULL REFERENCES ChiTietDonHang(MaChiTiet) ON DELETE CASCADE,
    MaTopping  INT NOT NULL REFERENCES Topping(MaTopping),
    DonGia     DECIMAL(12,0) NOT NULL,                 -- giá topping tại thời điểm đặt
    PRIMARY KEY (MaChiTiet, MaTopping)
);

/* ---------- 5. KHO ---------- */
CREATE TABLE NguyenLieu (
    MaNguyenLieu     INT IDENTITY(1,1) PRIMARY KEY,
    TenNguyenLieu    NVARCHAR(150) NOT NULL UNIQUE,
    DonViTinh        NVARCHAR(20)  NOT NULL,           -- g, ml, cái, quả...
    SoLuongTon       DECIMAL(18,3) NOT NULL DEFAULT 0,
    SoLuongToiThieu  DECIMAL(18,3) NOT NULL DEFAULT 0, -- dưới mức này sẽ cảnh báo
    DangHoatDong     BIT NOT NULL DEFAULT 1
);

CREATE TABLE CongThucMon (                             -- định mức nguyên liệu cho mỗi món
    MaMonAn       INT NOT NULL REFERENCES MonAn(MaMonAn) ON DELETE CASCADE,
    MaNguyenLieu  INT NOT NULL REFERENCES NguyenLieu(MaNguyenLieu),
    SoLuongDung   DECIMAL(18,3) NOT NULL CHECK (SoLuongDung > 0),
    PRIMARY KEY (MaMonAn, MaNguyenLieu)
);

CREATE TABLE CongThucTopping (
    MaTopping     INT NOT NULL REFERENCES Topping(MaTopping) ON DELETE CASCADE,
    MaNguyenLieu  INT NOT NULL REFERENCES NguyenLieu(MaNguyenLieu),
    SoLuongDung   DECIMAL(18,3) NOT NULL CHECK (SoLuongDung > 0),
    PRIMARY KEY (MaTopping, MaNguyenLieu)
);

CREATE TABLE NhaCungCap (
    MaNhaCungCap   INT IDENTITY(1,1) PRIMARY KEY,
    TenNhaCungCap  NVARCHAR(150) NOT NULL UNIQUE,
    SoDienThoai    VARCHAR(20)   NULL,
    DiaChi         NVARCHAR(250) NULL
);

CREATE TABLE PhieuNhap (
    MaPhieuNhap   INT IDENTITY(1,1) PRIMARY KEY,
    MaNhaCungCap  INT NULL REFERENCES NhaCungCap(MaNhaCungCap),
    MaNguoiNhap   INT NOT NULL REFERENCES NguoiDung(MaNguoiDung),
    NgayNhap      DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    GhiChu        NVARCHAR(300) NULL
);

CREATE TABLE ChiTietPhieuNhap (
    MaChiTietNhap  INT IDENTITY(1,1) PRIMARY KEY,
    MaPhieuNhap    INT NOT NULL REFERENCES PhieuNhap(MaPhieuNhap) ON DELETE CASCADE,
    MaNguyenLieu   INT NOT NULL REFERENCES NguyenLieu(MaNguyenLieu),
    SoLuong        DECIMAL(18,3) NOT NULL CHECK (SoLuong > 0),
    DonGiaNhap     DECIMAL(12,0) NOT NULL CHECK (DonGiaNhap >= 0)
);

CREATE TABLE BienDongKho (                             -- sổ kho: mọi lần tăng / giảm tồn
    MaBienDong        BIGINT IDENTITY(1,1) PRIMARY KEY,
    MaNguyenLieu      INT NOT NULL REFERENCES NguyenLieu(MaNguyenLieu),
    SoLuongThayDoi    DECIMAL(18,3) NOT NULL,          -- dương = nhập, âm = xuất
    LoaiBienDong      VARCHAR(12)   NOT NULL CHECK (LoaiBienDong IN ('NHAP','XUAT_DON','DIEU_CHINH','HOAN_KHO')),
    MaDonHang         INT NULL REFERENCES DonHang(MaDonHang),
    MaPhieuNhap       INT NULL REFERENCES PhieuNhap(MaPhieuNhap),
    MaNguoiThucHien   INT NULL REFERENCES NguoiDung(MaNguoiDung),
    NgayTao           DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    GhiChu            NVARCHAR(200) NULL
);
CREATE INDEX IX_BienDongKho_NguyenLieu ON BienDongKho(MaNguyenLieu, NgayTao DESC);
GO

/* ---------- 6. VIEW: tổng tiền mỗi đơn (dữ liệu suy diễn nên không lưu thành cột -> giữ 3NF) ---------- */
CREATE VIEW dbo.vw_TongTienDonHang AS
SELECT d.MaDonHang,
       CAST(ISNULL(SUM((ct.DonGia + ISNULL(tp.TongTopping, 0)) * ct.SoLuong), 0) AS DECIMAL(12,0)) AS TongTien
FROM DonHang d
LEFT JOIN ChiTietDonHang ct ON ct.MaDonHang = d.MaDonHang
LEFT JOIN (SELECT MaChiTiet, SUM(DonGia) AS TongTopping
           FROM ChiTietTopping GROUP BY MaChiTiet) tp ON tp.MaChiTiet = ct.MaChiTiet
GROUP BY d.MaDonHang;
GO

/* ---------- 7. ĐÁNH GIÁ ---------- */
CREATE TABLE DanhGiaMon (                              -- khách chấm sao cho từng dòng món trong đơn của mình
    MaDanhGiaMon  INT IDENTITY(1,1) PRIMARY KEY,
    MaChiTiet     INT NOT NULL UNIQUE                  -- UNIQUE: mỗi dòng món chỉ được đánh giá 1 lần
                  REFERENCES ChiTietDonHang(MaChiTiet) ON DELETE CASCADE,
    SoSao         INT NOT NULL CHECK (SoSao BETWEEN 1 AND 5),
    NhanXet       NVARCHAR(500) NULL,
    NgayTao       DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    DaAn          BIT NOT NULL DEFAULT 0               -- quản lý có thể ẩn nhận xét không phù hợp
);

CREATE TABLE DanhGiaDonHang (                          -- góp ý chung về quán / dịch vụ cho cả đơn
    MaDanhGiaDon  INT IDENTITY(1,1) PRIMARY KEY,
    MaDonHang     INT NOT NULL UNIQUE
                  REFERENCES DonHang(MaDonHang) ON DELETE CASCADE,
    SoSao         INT NOT NULL CHECK (SoSao BETWEEN 1 AND 5),
    GopY          NVARCHAR(1000) NULL,
    NgayTao       DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    DaXem         BIT NOT NULL DEFAULT 0
);
GO

/* Số lượt và điểm trung bình mỗi món là dữ liệu suy diễn -> dùng view, không lưu cột (giữ 3NF).
   Món của một đánh giá lấy qua ChiTietDonHang nên không lặp lại MaMonAn trong DanhGiaMon. */
CREATE VIEW dbo.vw_ThongKeDanhGiaMon AS
SELECT ct.MaMonAn,
       COUNT(*) AS SoLuot,
       CAST(AVG(CAST(dg.SoSao AS DECIMAL(5,2))) AS DECIMAL(3,2)) AS DiemTrungBinh
FROM DanhGiaMon dg
JOIN ChiTietDonHang ct ON ct.MaChiTiet = dg.MaChiTiet
WHERE dg.DaAn = 0
GROUP BY ct.MaMonAn;
GO

/* ---------- 8. ĐỐI SOÁT TIỀN CUỐI NGÀY ---------- */
CREATE TABLE DoiSoatCuoiNgay (
    MaDoiSoat        INT IDENTITY(1,1) PRIMARY KEY,
    NgayDoiSoat      DATE          NOT NULL,           -- ngày của doanh thu được đối soát
    MaNguoiThucHien  INT           NOT NULL REFERENCES NguoiDung(MaNguoiDung),
    TienDauCa        DECIMAL(12,0) NOT NULL DEFAULT 0 CHECK (TienDauCa >= 0),   -- tiền lẻ để sẵn trong két
    TienMatHeThong   DECIMAL(12,0) NOT NULL,           -- ảnh chụp tiền mặt hệ thống tính được lúc đối soát
    TienMatThucDem   DECIMAL(12,0) NOT NULL CHECK (TienMatThucDem >= 0),
    GhiChu           NVARCHAR(500) NULL,
    NgayTao          DATETIME2     NOT NULL DEFAULT SYSDATETIME()
    -- Chênh lệch = TienMatThucDem - (TienDauCa + TienMatHeThong): dữ liệu suy diễn nên không lưu cột
);
CREATE INDEX IX_DoiSoatCuoiNgay_Ngay ON DoiSoatCuoiNgay(NgayDoiSoat);
GO

/* ---------- 9. VẬN HÀNH: cài đặt quán, nhật ký thao tác, yêu cầu từ bàn ---------- */
CREATE TABLE CaiDatCuaHang (                           -- đúng 1 dòng duy nhất (MaCaiDat = 1)
    MaCaiDat              INT  NOT NULL PRIMARY KEY CHECK (MaCaiDat = 1),
    GioMo                 TIME(0) NOT NULL DEFAULT '08:00',
    GioDong               TIME(0) NOT NULL DEFAULT '22:00',      -- GioDong < GioMo nghĩa là quán mở qua nửa đêm
    TamNgungNhanDon       BIT  NOT NULL DEFAULT 0,               -- tạm ngưng nhận đơn trên web
    SoPhutTuHuyDonMangDi  INT  NOT NULL DEFAULT 15 CHECK (SoPhutTuHuyDonMangDi BETWEEN 0 AND 240),  -- 0 = không tự hủy
    TuAnMonHetNguyenLieu  BIT  NOT NULL DEFAULT 1
);
INSERT CaiDatCuaHang (MaCaiDat) VALUES (1);

CREATE TABLE NhatKyThaoTac (                           -- ai làm gì, lúc nào (hủy đơn, hoàn tiền, sửa giá...)
    MaNhatKy         BIGINT IDENTITY(1,1) PRIMARY KEY,
    NgayTao          DATETIME2    NOT NULL DEFAULT SYSDATETIME(),
    MaNguoiThucHien  INT          NULL REFERENCES NguoiDung(MaNguoiDung),   -- NULL = hệ thống tự làm
    HanhDong         VARCHAR(30)  NOT NULL,
    MoTa             NVARCHAR(500) NOT NULL,
    MaDonHang        INT          NULL REFERENCES DonHang(MaDonHang)
);
CREATE INDEX IX_NhatKyThaoTac_NgayTao ON NhatKyThaoTac(NgayTao DESC);

CREATE TABLE YeuCauTaiBan (                            -- khách bấm "Gọi nhân viên" / "Xin thanh toán" trên bàn
    MaYeuCau      INT IDENTITY(1,1) PRIMARY KEY,
    MaBan         INT         NOT NULL REFERENCES BanAn(MaBan),
    LoaiYeuCau    VARCHAR(20) NOT NULL CHECK (LoaiYeuCau IN ('GOI_NHAN_VIEN','XIN_THANH_TOAN')),
    NgayTao       DATETIME2   NOT NULL DEFAULT SYSDATETIME(),
    DaXuLy        BIT         NOT NULL DEFAULT 0,
    NgayXuLy      DATETIME2   NULL,
    MaNguoiXuLy   INT         NULL REFERENCES NguoiDung(MaNguoiDung)
);
CREATE INDEX IX_YeuCauTaiBan_ChuaXuLy ON YeuCauTaiBan(DaXuLy, NgayTao);
GO

/* ===================== DỮ LIỆU MẪU ===================== */
INSERT VaiTro (TenVaiTro) VALUES (N'QuanLy'), (N'ThuNgan');

INSERT NguoiDung (TenDangNhap, MatKhauHash, MuoiMatKhau, HoTen, SoDienThoai, MaVaiTro) VALUES
 ('quanly',   '38ED4BB0A8ABDC131DC6AC277C31EB724200AEE12C2A6FD216EB85890FA04CE9', 'A1B2C3D4E5F60718293A4B5C6D7E8F90', N'Quản lý cửa hàng', '0900000001', 1),
 ('thungan1', '5C9AF64D305BE6EEFBC8D4847EF320EB0F79ACF6C4D5B9073954B9A76B434E70', '0F1E2D3C4B5A69788796A5B4C3D2E1F0', N'Thu ngân 1',       '0900000002', 2);

INSERT TrangThaiDon VALUES (1,N'Chờ xác nhận'),(2,N'Đang làm'),(3,N'Sẵn sàng'),(4,N'Hoàn thành'),(5,N'Đã hủy');
INSERT PhuongThucThanhToan VALUES (1,N'Tiền mặt'),(2,N'Chuyển khoản');
INSERT MucDa    (TenMuc) VALUES (N'Đá bình thường'),(N'Ít đá'),(N'Không đá');
INSERT MucDuong (TenMuc) VALUES (N'Đường bình thường'),(N'Ít đường'),(N'Không đường');

INSERT NhomMon (TenNhomMon, BieuTuong, ThuTuHienThi, CoTuyChonDoUong) VALUES
 (N'Trà sữa',     N'🧋', 1, 1),   -- Mã 1
 (N'Trà hoa quả', N'🍑', 2, 1),   -- Mã 2
 (N'Trà kem',     N'🥤', 3, 1),   -- Mã 3
 (N'Dòng kem',    N'🍦', 4, 0);   -- Mã 4

INSERT MonAn (MaNhomMon, TenMon, TenTiengAnh, Gia, LaBanChay) VALUES
 (1,N'Trà sữa trân châu đường đen',N'Brown Sugar Pearl Milk Tea',25000,1),
 (1,N'Sữa tươi trân châu đường đen',N'Brown Sugar Pearl & Fresh Milk',25000,0),
 (1,N'Trà sữa 3Q',N'Milk Tea with 3 Toppings',25000,0),
 (1,N'Trà sữa Bá vương',N'King Milk Tea',30000,0),
 (1,N'Trà sữa socola',N'Chocolate Milk Tea',25000,0),
 (1,N'Trà sữa tươi bí đao',N'Winter Melon Fresh Milk Tea',25000,0),
 (1,N'Trà sữa nướng',N'Roasted Pearl Milk Tea',25000,0),
 (1,N'Trà sữa 2J',N'Milk Tea With 2 Toppings',25000,0),
 (1,N'Trà sữa thạch dừa',N'Coconut Jelly Milk Tea',25000,0),
 (1,N'Trà sữa trân châu',N'Pearl Milk Tea',25000,0),
 (2,N'Trà đào bốn mùa',N'Peach Oolong Tea',22000,1),
 (2,N'Trà mâm xôi',N'Raspberry Tea',22000,0),
 (2,N'Nước chanh tươi lạnh',N'Fresh-Squeezed Lemonade',15000,0),
 (2,N'Trà olong Kiwi',N'Kiwi Oolong Tea',20000,0),
 (2,N'Trà đào bigsize',N'Peach Bigsize Tea',22000,0),
 (2,N'Dương chi cam lộ',N'Creamy Mango Boba',30000,0),
 (2,N'Trà chanh lô hội',N'Aloe Lemon Tea',17000,0),
 (2,N'Hồng trà mật ong',N'Original Earl Grey Tea',15000,0),
 (2,N'Trà đào tứ kỳ xuân',N'Four Seasons Peach Tea',20000,0),
 (2,N'Hồng trà chanh',N'Lemon Earl Grey Tea',15000,0),
 (2,N'Trà ô long bốn mùa',N'Original Oolong Tea',15000,0),
 (3,N'Trà kem (Hồng trà/Ô long)',N'Ice-cream Tea (Black/Oolong Tea)',25000,0),
 (3,N'Trà kem mâm xôi',N'Ice-cream Raspberry Tea',25000,0),
 (4,N'Kem ốc quế',N'MIXUE Ice-cream (Nguyên vị, socola, mix)',10000,0),
 (4,N'Super sundae trân châu đường đen',N'Brown Sugar Pearl Super Sundae',25000,1),
 (4,N'Super sundae xoài',N'Mango Super Sundae',25000,0),
 (4,N'Super sundae dâu tây',N'Strawberry Super Sundae',25000,0),
 (4,N'Super sundae socola',N'Chocolate Super Sundae',25000,0),
 (4,N'Super sundae mâm xôi',N'Raspberry Super Sundae',25000,0),
 (4,N'Super sundae lô hội',N'Aloe Super Sundae',25000,0),
 (4,N'Sữa kem lắc đào',N'Peach Milk Shake',25000,0),
 (4,N'Sữa kem lắc dâu tây',N'Strawberry Milk Shake',25000,0),
 (4,N'Sữa kem lắc mâm xôi',N'Raspberry Milk Shake',25000,0),
 (4,N'Sữa thạch Kiwi Kiwi',N'Kiwi Kiwi Jelly Milk',22000,0),
 (4,N'Sữa thạch dâu tây',N'Strawberry Jelly Milk',22000,0);

/* Topping 3K / phần (chỉnh lại tên cho khớp menu thật của quán bạn) */
INSERT Topping (TenTopping, Gia) VALUES (N'Trân châu đen',3000),(N'Thạch dừa',3000),(N'Lô hội',3000),(N'Pudding',3000);

INSERT BanAn (TenBan, MaQr) VALUES
 (N'Bàn 1', REPLACE(CONVERT(CHAR(36), NEWID()),'-','')), (N'Bàn 2', REPLACE(CONVERT(CHAR(36), NEWID()),'-','')),
 (N'Bàn 3', REPLACE(CONVERT(CHAR(36), NEWID()),'-','')), (N'Bàn 4', REPLACE(CONVERT(CHAR(36), NEWID()),'-','')),
 (N'Bàn 5', REPLACE(CONVERT(CHAR(36), NEWID()),'-','')), (N'Bàn 6', REPLACE(CONVERT(CHAR(36), NEWID()),'-',''));

INSERT NguyenLieu (TenNguyenLieu, DonViTinh, SoLuongTon, SoLuongToiThieu) VALUES
 (N'Hồng trà',N'g',5000,500),(N'Trà ô long',N'g',5000,500),(N'Bột sữa',N'g',20000,3000),(N'Đường trắng',N'g',20000,3000),
 (N'Sữa tươi',N'ml',20000,3000),(N'Kem base',N'g',30000,5000),(N'Siro & mứt trái cây',N'ml',15000,2000),(N'Chanh tươi',N'quả',300,50),
 (N'Ốc quế',N'cái',500,100),(N'Ly nhựa',N'cái',2000,300),(N'Ống hút',N'cái',2000,300),
 (N'Trân châu đen',N'g',10000,1500),(N'Thạch dừa',N'g',8000,1000),(N'Lô hội',N'g',8000,1000),(N'Pudding',N'g',8000,1000);
/* Mã: 1 Hồng trà 2 Ô long 3 Bột sữa 4 Đường 5 Sữa tươi 6 Kem base 7 Siro 8 Chanh 9 Ốc quế 10 Ly 11 Ống hút 12 Trân châu 13 Thạch dừa 14 Lô hội 15 Pudding */

/* Công thức mặc định theo nhóm món - quản lý chỉnh chi tiết trong trang "Công thức" */
INSERT CongThucMon (MaMonAn, MaNguyenLieu, SoLuongDung)
SELECT m.MaMonAn, r.MaNguyenLieu, r.SoLuong
FROM MonAn m
JOIN (VALUES (1,1,6),(1,3,30),(1,4,15),(1,10,1),(1,11,1),
             (2,2,6),(2,7,40),(2,4,10),(2,10,1),(2,11,1),
             (3,1,6),(3,6,80),(3,4,10),(3,10,1),(3,11,1),
             (4,6,150),(4,7,20),(4,10,1)) AS r(MaNhomMon, MaNguyenLieu, SoLuong)
     ON r.MaNhomMon = m.MaNhomMon
WHERE m.TenMon <> N'Kem ốc quế';

INSERT CongThucMon (MaMonAn, MaNguyenLieu, SoLuongDung)
SELECT m.MaMonAn, v.MaNguyenLieu, v.SoLuong FROM MonAn m CROSS JOIN (VALUES (6,100),(9,1)) AS v(MaNguyenLieu, SoLuong)
WHERE m.TenMon = N'Kem ốc quế';

INSERT CongThucTopping (MaTopping, MaNguyenLieu, SoLuongDung) VALUES (1,12,50),(2,13,40),(3,14,40),(4,15,40);

INSERT NhaCungCap (TenNhaCungCap, SoDienThoai) VALUES (N'Nhà cung cấp mẫu', '0900000009');
GO
