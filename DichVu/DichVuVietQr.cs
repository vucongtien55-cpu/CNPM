using System.Globalization;
using System.Text;
using QRCoder;

namespace MixueDatMon.DichVu;

/// <summary>
/// Tạo mã VietQR động (chuẩn NAPAS 247 / EMVCo): mỗi mã đã có sẵn số tài khoản, SỐ TIỀN và NỘI DUNG chuyển khoản.
/// Khách mở app ngân hàng bất kỳ, quét mã là các thông tin được điền sẵn.
/// Cấu hình trong appsettings.json, mục "NganHang".
/// </summary>
public class DichVuVietQr
{
    public string MaBin { get; }
    public string TenNganHang { get; }
    public string SoTaiKhoan { get; }
    public string TenChuTaiKhoan { get; }

    /// <summary>Chỉ tạo được mã khi đã nhập đủ mã BIN ngân hàng và số tài khoản.</summary>
    public bool DaCauHinh => !string.IsNullOrWhiteSpace(MaBin) && !string.IsNullOrWhiteSpace(SoTaiKhoan);

    public DichVuVietQr(IConfiguration cauHinh)
    {
        MaBin = cauHinh["NganHang:MaBin"]?.Trim() ?? "";
        TenNganHang = cauHinh["NganHang:TenNganHang"]?.Trim() ?? "";
        SoTaiKhoan = cauHinh["NganHang:SoTaiKhoan"]?.Trim() ?? "";
        TenChuTaiKhoan = cauHinh["NganHang:TenChuTaiKhoan"]?.Trim() ?? "";
    }

    /// <summary>Nội dung chuyển khoản cho một đơn: mã đơn bỏ dấu gạch, ví dụ MX260920-K7Q2 -> MX260920K7Q2.</summary>
    public string NoiDungChoDon(string maTheoDoi) => LamSachNoiDung(maTheoDoi.Replace("-", ""));

    /// <summary>Nội dung chuyển khoản khi thu cả bàn, ví dụ "BAN 3 2009" (tên bàn không dấu + ngày tháng).</summary>
    public string NoiDungChoBan(string tenBan) => LamSachNoiDung(BoDau(tenBan) + " " + DateTime.Now.ToString("ddMM"));

    public string TaoChuoiVietQr(decimal soTien, string noiDung)
    {
        // Tài khoản thụ hưởng: mã BIN ngân hàng + số tài khoản
        var taiKhoan = Truong("00", MaBin) + Truong("01", SoTaiKhoan);
        // Thông tin dịch vụ NAPAS: chuyển nhanh 24/7 đến tài khoản (QRIBFTTA)
        var napas = Truong("00", "A000000727") + Truong("01", taiKhoan) + Truong("02", "QRIBFTTA");

        var chuoi = "000201"                                                  // phiên bản định dạng
                  + "010212"                                                  // 12 = mã QR động (có số tiền)
                  + Truong("38", napas)
                  + "5303704"                                                 // tiền tệ VND
                  + Truong("54", ((long)soTien).ToString(CultureInfo.InvariantCulture))
                  + "5802VN"
                  + Truong("62", Truong("08", LamSachNoiDung(noiDung)))       // nội dung chuyển khoản
                  + "6304";                                                   // mã kiểm tra CRC nối vào cuối
        return chuoi + TinhCrc16(chuoi);
    }

    public byte[] TaoAnhPng(string chuoiVietQr)
    {
        using var boTao = new QRCodeGenerator();
        using var duLieu = boTao.CreateQrCode(chuoiVietQr, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(duLieu).GetGraphic(10);
    }

    // ----- Hàm hỗ trợ -----
    private static string Truong(string ma, string giaTri) => ma + giaTri.Length.ToString("00", CultureInfo.InvariantCulture) + giaTri;

    /// <summary>CRC-16/CCITT-FALSE (đa thức 0x1021, giá trị đầu 0xFFFF) trên toàn bộ chuỗi, trả về 4 ký tự hex in hoa.</summary>
    private static string TinhCrc16(string chuoi)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(chuoi))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }

    /// <summary>Chỉ giữ chữ, số, khoảng trắng (không dấu, in hoa), tối đa 23 ký tự để mọi ngân hàng đều nhận.</summary>
    private static string LamSachNoiDung(string chuoi)
    {
        var sach = new string(BoDau(chuoi).ToUpperInvariant().Where(k => (k < 128 && char.IsLetterOrDigit(k)) || k == ' ').ToArray()).Trim();
        return sach.Length > 23 ? sach[..23] : sach;
    }

    private static string BoDau(string chuoi)
    {
        var tach = chuoi.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        return new string(tach.Where(k => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(k) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC);
    }
}
