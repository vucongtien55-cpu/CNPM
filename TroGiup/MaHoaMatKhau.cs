using System.Security.Cryptography;
using System.Text;

namespace MixueDatMon.TroGiup;

/// <summary>
/// Băm mật khẩu bằng SHA-256 kèm "muối" ngẫu nhiên riêng cho từng người dùng.
/// Hash = HEX( SHA256( UTF8( muoi + matKhau ) ) )  -> 64 ký tự.
/// </summary>
public static class MaHoaMatKhau
{
    public static string TaoMuoi() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));   // 32 ký tự

    public static string Bam(string matKhau, string muoi)
    {
        var byteBam = SHA256.HashData(Encoding.UTF8.GetBytes(muoi + matKhau));
        return Convert.ToHexString(byteBam);
    }

    public static bool KiemTra(string matKhau, string muoi, string hashDaLuu)
    {
        var thucTe = Encoding.ASCII.GetBytes(Bam(matKhau, muoi.Trim()));
        var mongDoi = Encoding.ASCII.GetBytes(hashDaLuu.Trim().ToUpperInvariant());
        return thucTe.Length == mongDoi.Length && CryptographicOperations.FixedTimeEquals(thucTe, mongDoi);
    }
}
